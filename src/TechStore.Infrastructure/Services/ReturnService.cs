using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Inventory;
using TechStore.Application.Features.Orders;
using TechStore.Application.Features.Returns;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class ReturnService : IReturnService
{
    private readonly ApplicationDbContext _db;
    private readonly IInventoryService _inventory;

    public ReturnService(ApplicationDbContext db, IInventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public async Task<Result<ReturnDetailsDto>> CreateAsync(
        CreateReturnRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Result.Failure<ReturnDetailsDto>("Return must contain at least one item.");
        }

        if (request.OrderId <= 0)
        {
            return Result.Failure<ReturnDetailsDto>("OrderId is required.");
        }

        var aggregated = request.Items
            .GroupBy(i => i.OrderItemId)
            .Select(g => new
            {
                OrderItemId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                ReasonCode = g.Select(x => x.ReasonCode).LastOrDefault(r => !string.IsNullOrWhiteSpace(r)),
                Disposition = g.Last().Disposition
            })
            .ToList();

        if (aggregated.Any(x => x.OrderItemId <= 0))
        {
            return Result.Failure<ReturnDetailsDto>("Invalid order item id.");
        }

        if (aggregated.Any(x => x.Quantity <= 0))
        {
            return Result.Failure<ReturnDetailsDto>("Item quantity must be greater than zero.");
        }

        foreach (var line in aggregated)
        {
            if (!Enum.IsDefined(typeof(ReturnDisposition), line.Disposition))
            {
                return Result.Failure<ReturnDetailsDto>("Invalid return disposition.");
            }
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var order = await _db.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
                if (order is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<ReturnDetailsDto>("Order not found.");
                }

                if (!await CanAccessOrderAsync(order.CustomerId, actor, cancellationToken))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<ReturnDetailsDto>("Not authorized to create a return for this order.", ResultCodes.Forbidden);
                }

                var orderItemMap = order.Items.ToDictionary(i => i.Id);
                foreach (var line in aggregated)
                {
                    if (!orderItemMap.TryGetValue(line.OrderItemId, out var orderItem))
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.NotFound<ReturnDetailsDto>(
                            $"Order item {line.OrderItemId} was not found on this order.");
                    }

                    var alreadyReturned = await GetAlreadyReturnedQuantityAsync(line.OrderItemId, cancellationToken);
                    var eligible = orderItem.Quantity - alreadyReturned;
                    if (line.Quantity > eligible)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Conflict<ReturnDetailsDto>(
                            $"Return quantity for order item {line.OrderItemId} exceeds eligible quantity ({eligible}).",
                            ResultCodes.Conflict);
                    }
                }

                var now = DateTime.UtcNow;
                var returnNumber = $"RET-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..50];
                var comment = Normalize(request.Comment);

                var entity = new ReturnRequest
                {
                    ReturnNumber = returnNumber,
                    OrderId = order.Id,
                    CustomerId = order.CustomerId,
                    WarehouseId = order.WarehouseId,
                    Status = ReturnRequestStatus.Requested,
                    CustomerComment = comment,
                    RequestedByUserId = actor.UserId,
                    InventoryApplied = false,
                    CreatedAt = now
                };

                foreach (var line in aggregated)
                {
                    var orderItem = orderItemMap[line.OrderItemId];
                    var unitPrice = orderItem.UnitPrice;
                    entity.Items.Add(new ReturnItem
                    {
                        OrderItemId = orderItem.Id,
                        VariantId = orderItem.VariantId,
                        SkuSnapshot = orderItem.Sku,
                        ProductNameSnapshot = string.IsNullOrWhiteSpace(orderItem.VariantName)
                            ? orderItem.ProductName
                            : $"{orderItem.ProductName} ({orderItem.VariantName})",
                        Quantity = line.Quantity,
                        UnitPriceSnapshot = unitPrice,
                        LineTotal = unitPrice * line.Quantity,
                        ReasonCode = Normalize(line.ReasonCode),
                        Disposition = line.Disposition
                    });
                }

                AddHistory(entity, null, ReturnRequestStatus.Requested, actor.UserId, comment ?? "Return requested", now);

                _db.ReturnRequests.Add(entity);
                await _db.SaveChangesAsync(cancellationToken);

                await WriteAuditAsync(actor.UserId, "ReturnRequest", entity.Id.ToString(), AuditAction.Create,
                    $"Created {entity.ReturnNumber} for order {order.OrderNumber}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);
                return Result.Success((await LoadDetailsAsync(entity.Id, cancellationToken))!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<PagedResult<ReturnSummaryDto>> GetPagedAsync(
        ReturnQuery query,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var returns = _db.ReturnRequests.AsNoTracking().AsQueryable();

        if (actor.IsCustomer && !actor.IsStaff)
        {
            var customerId = await GetCustomerIdForUserAsync(actor.UserId, cancellationToken);
            if (customerId is null)
            {
                return new PagedResult<ReturnSummaryDto> { Page = page, PageSize = pageSize, TotalCount = 0, Items = [] };
            }

            returns = returns.Where(r => r.CustomerId == customerId.Value);
        }
        else if (query.CustomerId.HasValue)
        {
            returns = returns.Where(r => r.CustomerId == query.CustomerId.Value);
        }

        if (query.Status.HasValue)
        {
            returns = returns.Where(r => r.Status == query.Status.Value);
        }

        if (query.OrderId.HasValue)
        {
            returns = returns.Where(r => r.OrderId == query.OrderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ReturnNumber))
        {
            var number = query.ReturnNumber.Trim();
            returns = returns.Where(r => r.ReturnNumber.Contains(number));
        }

        if (query.FromUtc.HasValue)
        {
            returns = returns.Where(r => r.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            returns = returns.Where(r => r.CreatedAt <= query.ToUtc.Value);
        }

        returns = (query.SortBy?.ToLowerInvariant()) switch
        {
            "returnnumber" => query.SortDesc
                ? returns.OrderByDescending(r => r.ReturnNumber).ThenByDescending(r => r.Id)
                : returns.OrderBy(r => r.ReturnNumber).ThenBy(r => r.Id),
            "status" => query.SortDesc
                ? returns.OrderByDescending(r => r.Status).ThenByDescending(r => r.Id)
                : returns.OrderBy(r => r.Status).ThenBy(r => r.Id),
            _ => query.SortDesc
                ? returns.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                : returns.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
        };

        var total = await returns.CountAsync(cancellationToken);
        var items = await returns
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReturnSummaryDto(
                r.Id,
                r.ReturnNumber,
                r.OrderId,
                r.Order.OrderNumber,
                r.CustomerId,
                (r.Customer.FirstName + " " + r.Customer.LastName).Trim(),
                r.WarehouseId,
                r.Status,
                r.InventoryApplied,
                r.Items.Count,
                r.Items.Sum(i => i.LineTotal),
                r.CreatedAt,
                r.ApprovedAt,
                r.ReceivedAt,
                r.ClosedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ReturnSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<ReturnDetailsDto?> GetByIdAsync(
        int id,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var details = await LoadDetailsAsync(id, cancellationToken);
        if (details is null)
        {
            return null;
        }

        if (!await CanAccessOrderAsync(details.CustomerId, actor, cancellationToken))
        {
            return null;
        }

        return details;
    }

    public async Task<IReadOnlyList<ReturnStatusHistoryDto>> GetHistoryAsync(
        int id,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ReturnRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null || !await CanAccessOrderAsync(entity.CustomerId, actor, cancellationToken))
        {
            return [];
        }

        return await _db.ReturnStatusHistories.AsNoTracking()
            .Where(h => h.ReturnRequestId == id)
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new ReturnStatusHistoryDto(
                h.Id,
                h.OldStatus,
                h.NewStatus,
                h.ChangedBy,
                h.Comment,
                h.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<Result<ReturnDetailsDto>> ApproveAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default)
        => TransitionStaffAsync(
            id,
            actor,
            ReturnRequestStatus.Approved,
            comment,
            (entity, now) =>
            {
                entity.ApprovedAt = now;
                entity.ReviewedByUserId = actor.UserId;
            },
            cancellationToken);

    public Task<Result<ReturnDetailsDto>> RejectAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default)
        => TransitionStaffAsync(
            id,
            actor,
            ReturnRequestStatus.Rejected,
            comment,
            (entity, now) =>
            {
                entity.RejectedAt = now;
                entity.ReviewedByUserId = actor.UserId;
            },
            cancellationToken);

    public Task<Result<ReturnDetailsDto>> MarkReceivedAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default)
        => TransitionStaffAsync(
            id,
            actor,
            ReturnRequestStatus.Received,
            comment,
            (entity, now) => entity.ReceivedAt = now,
            cancellationToken);

    public async Task<Result<ReturnDetailsDto>> ApplyInventoryAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsStaff)
        {
            return Result.Failure<ReturnDetailsDto>("Only staff can apply return inventory.", ResultCodes.Forbidden);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var entity = await _db.ReturnRequests
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
                if (entity is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<ReturnDetailsDto>("Return request not found.");
                }

                if (entity.InventoryApplied)
                {
                    await tx.CommitAsync(cancellationToken);
                    return Result.Success((await LoadDetailsAsync(entity.Id, cancellationToken))!);
                }

                if (ReturnTransitionRules.IsTerminal(entity.Status))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<ReturnDetailsDto>(
                        $"Cannot modify a return in status '{entity.Status}'.",
                        ResultCodes.InvalidTransition);
                }

                if (!ReturnTransitionRules.CanTransition(entity.Status, ReturnRequestStatus.Restocked))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<ReturnDetailsDto>(
                        $"Cannot apply inventory from status '{entity.Status}'.",
                        ResultCodes.InvalidTransition);
                }

                var now = DateTime.UtcNow;
                var staffComment = Normalize(comment);

                foreach (var item in entity.Items)
                {
                    if (item.Disposition != ReturnDisposition.RestockSellable)
                    {
                        continue;
                    }

                    if (!item.VariantId.HasValue)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<ReturnDetailsDto>(
                            $"Return item {item.Id} is missing variant reference and cannot be restocked.");
                    }

                    var receipt = await _inventory.ApplyReceiptInAmbientTransactionAsync(
                        entity.WarehouseId,
                        item.VariantId.Value,
                        item.Quantity,
                        unitCost: null,
                        referenceType: "ReturnRequest",
                        referenceId: entity.Id,
                        userId: actor.UserId,
                        comment: staffComment ?? $"Return restock {entity.ReturnNumber} item {item.Id}",
                        cancellationToken,
                        movementType: StockMovementType.Return);

                    if (receipt.IsFailure)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<ReturnDetailsDto>(receipt.Error!, receipt.Code);
                    }
                }

                var oldStatus = entity.Status;
                entity.Status = ReturnRequestStatus.Restocked;
                entity.InventoryApplied = true;
                entity.UpdatedAt = now;
                if (!string.IsNullOrWhiteSpace(staffComment))
                {
                    entity.StaffComment = staffComment;
                }

                AddHistory(entity, oldStatus, ReturnRequestStatus.Restocked, actor.UserId,
                    staffComment ?? "Inventory applied", now);
                await WriteAuditAsync(actor.UserId, "ReturnRequest", entity.Id.ToString(), AuditAction.Update,
                    $"Applied inventory {entity.ReturnNumber}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return Result.Success((await LoadDetailsAsync(entity.Id, cancellationToken))!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<Result<ReturnDetailsDto>> CloseAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsStaff)
        {
            return Result.Failure<ReturnDetailsDto>("Only staff can close returns.", ResultCodes.Forbidden);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var entity = await _db.ReturnRequests
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
                if (entity is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<ReturnDetailsDto>("Return request not found.");
                }

                if (ReturnTransitionRules.IsTerminal(entity.Status))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<ReturnDetailsDto>(
                        $"Cannot modify a return in status '{entity.Status}'.",
                        ResultCodes.InvalidTransition);
                }

                if (entity.Status == ReturnRequestStatus.Received)
                {
                    var hasRestockable = entity.Items.Any(i => i.Disposition == ReturnDisposition.RestockSellable);
                    if (hasRestockable)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Conflict<ReturnDetailsDto>(
                            "Cannot close a received return that still has RestockSellable items; apply inventory first.",
                            ResultCodes.InvalidTransition);
                    }
                }

                if (!ReturnTransitionRules.CanTransition(entity.Status, ReturnRequestStatus.Closed))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<ReturnDetailsDto>(
                        $"Cannot close return from status '{entity.Status}'.",
                        ResultCodes.InvalidTransition);
                }

                var now = DateTime.UtcNow;
                var staffComment = Normalize(comment);
                var oldStatus = entity.Status;
                entity.Status = ReturnRequestStatus.Closed;
                entity.ClosedAt = now;
                entity.UpdatedAt = now;
                if (!string.IsNullOrWhiteSpace(staffComment))
                {
                    entity.StaffComment = staffComment;
                }

                AddHistory(entity, oldStatus, ReturnRequestStatus.Closed, actor.UserId,
                    staffComment ?? "Closed", now);
                await WriteAuditAsync(actor.UserId, "ReturnRequest", entity.Id.ToString(), AuditAction.Update,
                    $"Closed {entity.ReturnNumber}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return Result.Success((await LoadDetailsAsync(entity.Id, cancellationToken))!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<Result<ReturnDetailsDto>> TransitionStaffAsync(
        int id,
        ActorContext actor,
        ReturnRequestStatus target,
        string? comment,
        Action<ReturnRequest, DateTime> onSuccess,
        CancellationToken cancellationToken)
    {
        if (!actor.IsStaff)
        {
            return Result.Failure<ReturnDetailsDto>("Only staff can perform this action.", ResultCodes.Forbidden);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var entity = await _db.ReturnRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
                if (entity is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<ReturnDetailsDto>("Return request not found.");
                }

                if (ReturnTransitionRules.IsTerminal(entity.Status))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<ReturnDetailsDto>(
                        $"Cannot modify a return in status '{entity.Status}'.",
                        ResultCodes.InvalidTransition);
                }

                if (!ReturnTransitionRules.CanTransition(entity.Status, target))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<ReturnDetailsDto>(
                        $"Cannot transition return from '{entity.Status}' to '{target}'.",
                        ResultCodes.InvalidTransition);
                }

                var now = DateTime.UtcNow;
                var staffComment = Normalize(comment);
                var oldStatus = entity.Status;
                entity.Status = target;
                entity.UpdatedAt = now;
                if (!string.IsNullOrWhiteSpace(staffComment))
                {
                    entity.StaffComment = staffComment;
                }

                onSuccess(entity, now);
                AddHistory(entity, oldStatus, target, actor.UserId, staffComment ?? target.ToString(), now);
                await WriteAuditAsync(actor.UserId, "ReturnRequest", entity.Id.ToString(), AuditAction.Update,
                    $"{target} {entity.ReturnNumber}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return Result.Success((await LoadDetailsAsync(entity.Id, cancellationToken))!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<int> GetAlreadyReturnedQuantityAsync(int orderItemId, CancellationToken cancellationToken)
    {
        return await _db.ReturnItems
            .Where(ri => ri.OrderItemId == orderItemId
                         && ri.ReturnRequest.Status != ReturnRequestStatus.Rejected)
            .SumAsync(ri => (int?)ri.Quantity, cancellationToken) ?? 0;
    }

    private async Task<bool> CanAccessOrderAsync(int customerId, ActorContext actor, CancellationToken cancellationToken)
    {
        if (actor.IsStaff)
        {
            return true;
        }

        var linkedId = await GetCustomerIdForUserAsync(actor.UserId, cancellationToken);
        return linkedId == customerId;
    }

    private async Task<int?> GetCustomerIdForUserAsync(int userId, CancellationToken cancellationToken)
        => await _db.Customers.AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ReturnDetailsDto?> LoadDetailsAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.ReturnRequests.AsNoTracking()
            .Include(r => r.Order)
            .Include(r => r.Customer)
            .Include(r => r.Items)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var items = entity.Items
            .OrderBy(i => i.Id)
            .Select(i => new ReturnItemDto(
                i.Id,
                i.OrderItemId,
                i.VariantId,
                i.SkuSnapshot,
                i.ProductNameSnapshot,
                i.Quantity,
                i.UnitPriceSnapshot,
                i.LineTotal,
                i.ReasonCode,
                i.Disposition))
            .ToList();

        var history = entity.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new ReturnStatusHistoryDto(
                h.Id, h.OldStatus, h.NewStatus, h.ChangedBy, h.Comment, h.ChangedAt))
            .ToList();

        return new ReturnDetailsDto(
            entity.Id,
            entity.ReturnNumber,
            entity.OrderId,
            entity.Order.OrderNumber,
            entity.CustomerId,
            $"{entity.Customer.FirstName} {entity.Customer.LastName}".Trim(),
            entity.WarehouseId,
            entity.Status,
            entity.CustomerComment,
            entity.StaffComment,
            entity.RequestedByUserId,
            entity.ReviewedByUserId,
            entity.InventoryApplied,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ApprovedAt,
            entity.RejectedAt,
            entity.ReceivedAt,
            entity.ClosedAt,
            items.Sum(i => i.LineTotal),
            items,
            history);
    }

    private static void AddHistory(
        ReturnRequest entity,
        ReturnRequestStatus? oldStatus,
        ReturnRequestStatus newStatus,
        int? userId,
        string? comment,
        DateTime changedAt)
    {
        entity.StatusHistory.Add(new ReturnStatusHistory
        {
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedBy = userId,
            Comment = comment,
            ChangedAt = changedAt
        });
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

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
