using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Inventory;
using TechStore.Application.Features.Orders;
using TechStore.Application.Features.Purchasing;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly IInventoryService _inventory;

    public PurchaseOrderService(ApplicationDbContext db, IInventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public async Task<PagedResult<PurchaseOrderSummaryDto>> GetPagedAsync(
        PurchaseOrderQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var orders = _db.PurchaseOrders.AsNoTracking().AsQueryable();

        if (query.Status.HasValue)
        {
            orders = orders.Where(o => o.Status == query.Status.Value);
        }

        if (query.SupplierId.HasValue)
        {
            orders = orders.Where(o => o.SupplierId == query.SupplierId.Value);
        }

        if (query.WarehouseId.HasValue)
        {
            orders = orders.Where(o => o.WarehouseId == query.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.OrderNumber))
        {
            var number = query.OrderNumber.Trim();
            orders = orders.Where(o => o.OrderNumber.Contains(number));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            orders = orders.Where(o =>
                o.OrderNumber.Contains(search) ||
                o.Supplier.Name.Contains(search) ||
                o.Warehouse.Name.Contains(search));
        }

        if (query.FromUtc.HasValue)
        {
            orders = orders.Where(o => o.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            orders = orders.Where(o => o.CreatedAt <= query.ToUtc.Value);
        }

        var total = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new PurchaseOrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.SupplierId,
                o.Supplier.Name,
                o.WarehouseId,
                o.Warehouse.Name,
                o.Status,
                o.ExpectedDeliveryDate,
                o.Items.Count,
                o.Items.Sum(i => i.LineTotal),
                o.CreatedAt,
                o.ConfirmedAt,
                o.FullyReceivedAt,
                o.CancelledAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseOrderSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<PurchaseOrderDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await LoadDetailsAsync(id, cancellationToken);

    public async Task<IReadOnlyList<PurchaseOrderStatusHistoryDto>> GetHistoryAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.PurchaseOrders.AsNoTracking().AnyAsync(o => o.Id == id, cancellationToken);
        if (!exists)
        {
            return [];
        }

        return await _db.PurchaseOrderStatusHistories
            .AsNoTracking()
            .Where(h => h.PurchaseOrderId == id)
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new PurchaseOrderStatusHistoryDto(
                h.Id, h.OldStatus, h.NewStatus, h.ChangedBy, h.Comment, h.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<PurchaseOrderDetailsDto>> CreateDraftAsync(
        CreatePurchaseOrderRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (supplier is null || !supplier.IsActive)
        {
            return Result.NotFound<PurchaseOrderDetailsDto>("Supplier not found or inactive.");
        }

        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);
        if (warehouse is null || !warehouse.IsActive)
        {
            return Result.NotFound<PurchaseOrderDetailsDto>("Warehouse not found or inactive.");
        }

        var aggregated = AggregateItems(request.Items);
        if (aggregated.Count == 0 && request.Items.Count > 0)
        {
            return Result.Failure<PurchaseOrderDetailsDto>("All item quantities must be greater than zero.");
        }

        foreach (var line in aggregated)
        {
            if (line.Quantity <= 0)
            {
                return Result.Failure<PurchaseOrderDetailsDto>("Item quantity must be greater than zero.");
            }

            if (line.UnitCost is < 0)
            {
                return Result.Failure<PurchaseOrderDetailsDto>("Unit cost cannot be negative.");
            }
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var order = new PurchaseOrder
                {
                    OrderNumber = $"PO-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32],
                    SupplierId = request.SupplierId,
                    WarehouseId = request.WarehouseId,
                    Status = PurchaseOrderStatus.Draft,
                    ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                    Notes = Normalize(request.Notes),
                    CreatedByUserId = actor.UserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                if (aggregated.Count > 0)
                {
                    var build = await BuildItemsAsync(order.SupplierId, aggregated, cancellationToken);
                    if (build.IsFailure)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<PurchaseOrderDetailsDto>(build.Error!, build.Code);
                    }

                    foreach (var item in build.Value!)
                    {
                        order.Items.Add(item);
                    }
                }

                _db.PurchaseOrders.Add(order);
                AddHistory(order, null, PurchaseOrderStatus.Draft, actor.UserId, "Draft created", now);
                await _db.SaveChangesAsync(cancellationToken);

                await WriteAuditAsync(actor.UserId, "PurchaseOrder", order.Id.ToString(), AuditAction.Create,
                    $"Created draft {order.OrderNumber}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);
                var details = await LoadDetailsAsync(order.Id, cancellationToken);
                return Result.Success(details!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<Result<PurchaseOrderDetailsDto>> UpdateDraftAsync(
        int id,
        UpdatePurchaseOrderRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var order = await _db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return Result.NotFound<PurchaseOrderDetailsDto>("Purchase order not found.");
        }

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            return Result.Conflict<PurchaseOrderDetailsDto>(
                "Only draft purchase orders can be updated.",
                ResultCodes.InvalidTransition);
        }

        order.ExpectedDeliveryDate = request.ExpectedDeliveryDate;
        order.Notes = Normalize(request.Notes);
        order.UpdatedAt = DateTime.UtcNow;

        await WriteAuditAsync(actor.UserId, "PurchaseOrder", order.Id.ToString(), AuditAction.Update,
            "Updated draft header", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var details = await LoadDetailsAsync(order.Id, cancellationToken);
        return Result.Success(details!);
    }

    public async Task<Result<PurchaseOrderDetailsDto>> SetItemsAsync(
        int id,
        SetPurchaseOrderItemsRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var aggregated = AggregateItems(request.Items);
        foreach (var line in aggregated)
        {
            if (line.Quantity <= 0)
            {
                return Result.Failure<PurchaseOrderDetailsDto>("Item quantity must be greater than zero.");
            }

            if (line.UnitCost is < 0)
            {
                return Result.Failure<PurchaseOrderDetailsDto>("Unit cost cannot be negative.");
            }
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var order = await _db.PurchaseOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
                if (order is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<PurchaseOrderDetailsDto>("Purchase order not found.");
                }

                if (!PurchaseOrderTransitionRules.CanEditItems(order.Status))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<PurchaseOrderDetailsDto>(
                        "Items can only be edited while the purchase order is in Draft status.",
                        ResultCodes.InvalidTransition);
                }

                _db.PurchaseOrderItems.RemoveRange(order.Items);
                order.Items.Clear();

                if (aggregated.Count > 0)
                {
                    var build = await BuildItemsAsync(order.SupplierId, aggregated, cancellationToken);
                    if (build.IsFailure)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<PurchaseOrderDetailsDto>(build.Error!, build.Code);
                    }

                    foreach (var item in build.Value!)
                    {
                        order.Items.Add(item);
                    }
                }

                order.UpdatedAt = DateTime.UtcNow;
                await WriteAuditAsync(actor.UserId, "PurchaseOrder", order.Id.ToString(), AuditAction.Update,
                    $"Set items count={order.Items.Count}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                var details = await LoadDetailsAsync(order.Id, cancellationToken);
                return Result.Success(details!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<Result<PurchaseOrderDetailsDto>> ConfirmAsync(
        int id,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var order = await _db.PurchaseOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
                if (order is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<PurchaseOrderDetailsDto>("Purchase order not found.");
                }

                if (!PurchaseOrderTransitionRules.CanTransition(order.Status, PurchaseOrderStatus.Confirmed))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<PurchaseOrderDetailsDto>(
                        $"Cannot confirm purchase order from status '{order.Status}'.",
                        ResultCodes.InvalidTransition);
                }

                if (order.Items.Count == 0)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<PurchaseOrderDetailsDto>("Cannot confirm a purchase order with no items.");
                }

                var supplier = await _db.Suppliers.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == order.SupplierId, cancellationToken);
                if (supplier is null || !supplier.IsActive)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<PurchaseOrderDetailsDto>("Supplier is inactive or missing.");
                }

                var warehouse = await _db.Warehouses.AsNoTracking()
                    .FirstOrDefaultAsync(w => w.Id == order.WarehouseId, cancellationToken);
                if (warehouse is null || !warehouse.IsActive)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<PurchaseOrderDetailsDto>("Warehouse is inactive or missing.");
                }

                var variantIds = order.Items.Select(i => i.VariantId).Distinct().ToList();
                var supplyPrices = await _db.SupplierProducts.AsNoTracking()
                    .Where(sp => sp.SupplierId == order.SupplierId && variantIds.Contains(sp.VariantId))
                    .ToDictionaryAsync(sp => sp.VariantId, sp => sp.SupplyPrice, cancellationToken);
                var purchasePrices = await _db.ProductVariants.AsNoTracking()
                    .Where(v => variantIds.Contains(v.Id))
                    .ToDictionaryAsync(v => v.Id, v => v.PurchasePrice, cancellationToken);

                foreach (var item in order.Items)
                {
                    if (!purchasePrices.ContainsKey(item.VariantId))
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.NotFound<PurchaseOrderDetailsDto>(
                            $"Product variant {item.VariantId} was not found.");
                    }

                    var unitCost = supplyPrices.TryGetValue(item.VariantId, out var supply)
                        ? supply
                        : purchasePrices[item.VariantId];
                    item.UnitCost = unitCost;
                    item.LineTotal = decimal.Round(unitCost * item.OrderedQuantity, 2, MidpointRounding.AwayFromZero);
                }

                var now = DateTime.UtcNow;
                var oldStatus = order.Status;
                order.Status = PurchaseOrderStatus.Confirmed;
                order.ConfirmedAt = now;
                order.UpdatedAt = now;
                AddHistory(order, oldStatus, PurchaseOrderStatus.Confirmed, actor.UserId, "Confirmed", now);
                await WriteAuditAsync(actor.UserId, "PurchaseOrder", order.Id.ToString(), AuditAction.Update,
                    "Confirmed", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                var details = await LoadDetailsAsync(order.Id, cancellationToken);
                return Result.Success(details!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<Result<PurchaseOrderDetailsDto>> CancelAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var order = await _db.PurchaseOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
                if (order is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<PurchaseOrderDetailsDto>("Purchase order not found.");
                }

                if (order.Status == PurchaseOrderStatus.Cancelled)
                {
                    await tx.RollbackAsync(cancellationToken);
                    var existing = await LoadDetailsAsync(order.Id, cancellationToken);
                    return Result.Success(existing!);
                }

                if (!PurchaseOrderTransitionRules.CanCancel(order.Status) ||
                    !PurchaseOrderTransitionRules.CanTransition(order.Status, PurchaseOrderStatus.Cancelled))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<PurchaseOrderDetailsDto>(
                        $"Cannot cancel purchase order from status '{order.Status}'.",
                        ResultCodes.InvalidTransition);
                }

                if (order.Items.Any(i => i.ReceivedQuantity > 0))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<PurchaseOrderDetailsDto>(
                        "Cannot cancel a purchase order that already has received quantities.",
                        ResultCodes.InvalidTransition);
                }

                var now = DateTime.UtcNow;
                var oldStatus = order.Status;
                order.Status = PurchaseOrderStatus.Cancelled;
                order.CancelledAt = now;
                order.UpdatedAt = now;
                AddHistory(order, oldStatus, PurchaseOrderStatus.Cancelled, actor.UserId,
                    Normalize(comment) ?? "Cancelled", now);
                await WriteAuditAsync(actor.UserId, "PurchaseOrder", order.Id.ToString(), AuditAction.Update,
                    "Cancelled", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                var details = await LoadDetailsAsync(order.Id, cancellationToken);
                return Result.Success(details!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<Result<PurchaseOrderDetailsDto>> ReceiveAsync(
        int id,
        ReceivePurchaseOrderRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return Result.Failure<PurchaseOrderDetailsDto>("At least one receive line is required.");
        }

        var aggregatedLines = request.Lines
            .GroupBy(l => l.PurchaseOrderItemId)
            .Select(g => new ReceivePurchaseOrderLineRequest
            {
                PurchaseOrderItemId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        foreach (var line in aggregatedLines)
        {
            if (line.Quantity <= 0)
            {
                return Result.Failure<PurchaseOrderDetailsDto>("Receive quantity must be greater than zero.");
            }
        }

        string? key = null;
        string? hash = null;
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            key = request.IdempotencyKey.Trim();
            if (key.Length > 100)
            {
                return Result.Failure<PurchaseOrderDetailsDto>("Idempotency key must be at most 100 characters.");
            }

            hash = ComputeHash(new
            {
                PurchaseOrderId = id,
                Lines = aggregatedLines
                    .OrderBy(l => l.PurchaseOrderItemId)
                    .Select(l => new { l.PurchaseOrderItemId, l.Quantity }),
                request.Comment
            });
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            if (key is not null)
            {
                var existing = await _db.PurchaseReceiptIdempotencies
                    .FirstOrDefaultAsync(
                        x => x.UserId == actor.UserId && x.IdempotencyKey == key,
                        cancellationToken);
                if (existing is not null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    if (!string.Equals(existing.RequestHash, hash, StringComparison.Ordinal) ||
                        existing.PurchaseOrderId != id)
                    {
                        return Result.Conflict<PurchaseOrderDetailsDto>(
                            "Idempotency key was already used with a different request payload.",
                            ResultCodes.IdempotencyConflict);
                    }

                    var replayed = JsonSerializer.Deserialize<PurchaseOrderDetailsDto>(existing.ResponseJson, JsonOptions);
                    return replayed is null
                        ? Result.Conflict<PurchaseOrderDetailsDto>(
                            "Stored idempotent response could not be restored.",
                            ResultCodes.IdempotencyConflict)
                        : Result.Success(replayed with { ReplayFromIdempotency = true });
                }
            }

            try
            {
                var order = await _db.PurchaseOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
                if (order is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<PurchaseOrderDetailsDto>("Purchase order not found.");
                }

                if (!PurchaseOrderTransitionRules.CanReceive(order.Status))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<PurchaseOrderDetailsDto>(
                        $"Cannot receive against purchase order in status '{order.Status}'.",
                        ResultCodes.InvalidTransition);
                }

                var itemMap = order.Items.ToDictionary(i => i.Id);
                foreach (var line in aggregatedLines)
                {
                    if (!itemMap.TryGetValue(line.PurchaseOrderItemId, out var item))
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.NotFound<PurchaseOrderDetailsDto>(
                            $"Purchase order item {line.PurchaseOrderItemId} was not found on this order.");
                    }

                    var remaining = item.OrderedQuantity - item.ReceivedQuantity;
                    if (line.Quantity > remaining)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Conflict<PurchaseOrderDetailsDto>(
                            $"Receive quantity for item {item.Id} exceeds remaining quantity ({remaining}).",
                            ResultCodes.Conflict);
                    }
                }

                var now = DateTime.UtcNow;
                var comment = Normalize(request.Comment);

                foreach (var line in aggregatedLines)
                {
                    var item = itemMap[line.PurchaseOrderItemId];
                    var receipt = await _inventory.ApplyReceiptInAmbientTransactionAsync(
                        order.WarehouseId,
                        item.VariantId,
                        line.Quantity,
                        item.UnitCost,
                        "PurchaseOrder",
                        order.Id,
                        actor.UserId,
                        comment ?? $"PO receive {order.OrderNumber} item {item.Id}",
                        cancellationToken);

                    if (receipt.IsFailure)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<PurchaseOrderDetailsDto>(receipt.Error!, receipt.Code);
                    }

                    item.ReceivedQuantity += line.Quantity;
                }

                var oldStatus = order.Status;
                var allReceived = order.Items.All(i => i.ReceivedQuantity >= i.OrderedQuantity);
                var anyReceived = order.Items.Any(i => i.ReceivedQuantity > 0);
                var newStatus = allReceived
                    ? PurchaseOrderStatus.Received
                    : anyReceived
                        ? PurchaseOrderStatus.PartiallyReceived
                        : order.Status;

                if (newStatus != oldStatus)
                {
                    if (!PurchaseOrderTransitionRules.CanTransition(oldStatus, newStatus))
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Conflict<PurchaseOrderDetailsDto>(
                            $"Invalid status transition from '{oldStatus}' to '{newStatus}'.",
                            ResultCodes.InvalidTransition);
                    }

                    order.Status = newStatus;
                    AddHistory(order, oldStatus, newStatus, actor.UserId,
                        comment ?? $"Received {aggregatedLines.Sum(l => l.Quantity)} units", now);
                }
                else if (oldStatus == PurchaseOrderStatus.PartiallyReceived)
                {
                    // Same status but additional partial receive — still record history per rules allowing PartiallyReceived -> PartiallyReceived
                    AddHistory(order, oldStatus, PurchaseOrderStatus.PartiallyReceived, actor.UserId,
                        comment ?? $"Received {aggregatedLines.Sum(l => l.Quantity)} units", now);
                }

                if (allReceived)
                {
                    order.FullyReceivedAt = now;
                }

                order.UpdatedAt = now;

                await WriteAuditAsync(actor.UserId, "PurchaseOrder", order.Id.ToString(), AuditAction.Update,
                    $"Received lines={aggregatedLines.Count} status={order.Status}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                var details = await LoadDetailsAsync(order.Id, cancellationToken);
                if (details is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<PurchaseOrderDetailsDto>("Failed to load purchase order after receive.");
                }

                var payload = details with { ReplayFromIdempotency = false };
                if (key is not null && hash is not null)
                {
                    _db.PurchaseReceiptIdempotencies.Add(new PurchaseReceiptIdempotency
                    {
                        IdempotencyKey = key,
                        UserId = actor.UserId,
                        PurchaseOrderId = order.Id,
                        RequestHash = hash,
                        ResponseJson = JsonSerializer.Serialize(payload, JsonOptions),
                        CreatedAt = now
                    });
                    await _db.SaveChangesAsync(cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
                return Result.Success(payload);
            }
            catch (DbUpdateException) when (key is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                var raced = await _db.PurchaseReceiptIdempotencies.AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.UserId == actor.UserId && x.IdempotencyKey == key,
                        cancellationToken);
                if (raced is not null &&
                    string.Equals(raced.RequestHash, hash, StringComparison.Ordinal) &&
                    raced.PurchaseOrderId == id)
                {
                    var replayed = JsonSerializer.Deserialize<PurchaseOrderDetailsDto>(raced.ResponseJson, JsonOptions);
                    if (replayed is not null)
                    {
                        return Result.Success(replayed with { ReplayFromIdempotency = true });
                    }
                }

                return Result.Conflict<PurchaseOrderDetailsDto>("Idempotency key conflict.", ResultCodes.IdempotencyConflict);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<Result<List<PurchaseOrderItem>>> BuildItemsAsync(
        int supplierId,
        IReadOnlyList<(int VariantId, int Quantity, decimal? UnitCost)> lines,
        CancellationToken cancellationToken)
    {
        var variantIds = lines.Select(l => l.VariantId).ToList();
        var variants = await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync(cancellationToken);

        if (variants.Count != variantIds.Count)
        {
            return Result.NotFound<List<PurchaseOrderItem>>("One or more product variants were not found.");
        }

        var supplyPrices = await _db.SupplierProducts.AsNoTracking()
            .Where(sp => sp.SupplierId == supplierId && variantIds.Contains(sp.VariantId))
            .ToDictionaryAsync(sp => sp.VariantId, sp => sp.SupplyPrice, cancellationToken);

        var items = new List<PurchaseOrderItem>();
        foreach (var line in lines)
        {
            var variant = variants.First(v => v.Id == line.VariantId);
            if (!variant.IsActive)
            {
                return Result.Failure<List<PurchaseOrderItem>>($"Variant '{variant.Sku}' is inactive.");
            }

            var unitCost = line.UnitCost
                ?? (supplyPrices.TryGetValue(variant.Id, out var supply) ? supply : variant.PurchasePrice);

            if (unitCost < 0)
            {
                return Result.Failure<List<PurchaseOrderItem>>("Unit cost cannot be negative.");
            }

            items.Add(new PurchaseOrderItem
            {
                VariantId = variant.Id,
                SkuSnapshot = variant.Sku,
                ProductNameSnapshot = string.IsNullOrWhiteSpace(variant.VariantName)
                    ? variant.Product.Name
                    : $"{variant.Product.Name} / {variant.VariantName}",
                OrderedQuantity = line.Quantity,
                ReceivedQuantity = 0,
                UnitCost = unitCost,
                LineTotal = decimal.Round(unitCost * line.Quantity, 2, MidpointRounding.AwayFromZero)
            });
        }

        return Result.Success(items);
    }

    private async Task<PurchaseOrderDetailsDto?> LoadDetailsAsync(int id, CancellationToken cancellationToken)
    {
        var order = await _db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.Warehouse)
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order is null ? null : MapDetails(order, replay: false);
    }

    private static PurchaseOrderDetailsDto MapDetails(PurchaseOrder order, bool replay)
    {
        var items = order.Items
            .OrderBy(i => i.Id)
            .Select(i => new PurchaseOrderItemDto(
                i.Id,
                i.VariantId,
                i.SkuSnapshot,
                i.ProductNameSnapshot,
                i.OrderedQuantity,
                i.ReceivedQuantity,
                i.OrderedQuantity - i.ReceivedQuantity,
                i.UnitCost,
                i.LineTotal))
            .ToList();

        var history = (order.StatusHistory ?? [])
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new PurchaseOrderStatusHistoryDto(
                h.Id, h.OldStatus, h.NewStatus, h.ChangedBy, h.Comment, h.ChangedAt))
            .ToList();

        return new PurchaseOrderDetailsDto(
            order.Id,
            order.OrderNumber,
            order.SupplierId,
            order.Supplier?.Name ?? string.Empty,
            order.WarehouseId,
            order.Warehouse?.Name ?? string.Empty,
            order.Status,
            order.ExpectedDeliveryDate,
            order.Notes,
            order.CreatedByUserId,
            replay,
            order.CreatedAt,
            order.UpdatedAt,
            order.ConfirmedAt,
            order.FullyReceivedAt,
            order.CancelledAt,
            items.Sum(i => i.LineTotal),
            items,
            history);
    }

    private static void AddHistory(
        PurchaseOrder order,
        PurchaseOrderStatus? oldStatus,
        PurchaseOrderStatus newStatus,
        int? userId,
        string? comment,
        DateTime changedAt)
    {
        order.StatusHistory.Add(new PurchaseOrderStatusHistory
        {
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedBy = userId,
            Comment = comment,
            ChangedAt = changedAt
        });
    }

    private static List<(int VariantId, int Quantity, decimal? UnitCost)> AggregateItems(
        IEnumerable<CreatePurchaseOrderItemRequest> items)
    {
        return items
            .GroupBy(i => i.VariantId)
            .Select(g => (
                VariantId: g.Key,
                Quantity: g.Sum(x => x.Quantity),
                UnitCost: g.Select(x => x.UnitCost).LastOrDefault(c => c.HasValue)))
            .Where(x => x.Quantity > 0)
            .ToList();
    }

    private async Task WriteAuditAsync(
        int? userId,
        string entityType,
        string entityId,
        AuditAction action,
        string details,
        CancellationToken cancellationToken)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            NewValues = details,
            CreatedAt = DateTime.UtcNow
        });
        await Task.CompletedTask;
    }

    private static string ComputeHash(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize) => pageSize is < 1 or > 100 ? 20 : pageSize;
}
