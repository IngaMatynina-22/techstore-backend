using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Inventory;
using TechStore.Application.Features.Orders;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class OrderService : IOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly IInventoryService _inventory;

    public OrderService(ApplicationDbContext db, IInventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public async Task<Result<OrderDetailsDto>> CreateAsync(
        CreateOrderRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Result.Failure<OrderDetailsDto>("Order must contain at least one item.");
        }

        if (request.WarehouseId <= 0)
        {
            return Result.Failure<OrderDetailsDto>("WarehouseId is required.");
        }

        // Aggregate duplicate variant lines before stock checks.
        var aggregated = request.Items
            .GroupBy(i => i.ProductVariantId)
            .Select(g => new { VariantId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        if (aggregated.Any(x => x.VariantId <= 0))
        {
            return Result.Failure<OrderDetailsDto>("Invalid product variant id.");
        }

        if (aggregated.Any(x => x.Quantity <= 0))
        {
            return Result.Failure<OrderDetailsDto>("Item quantity must be greater than zero.");
        }

        string? key = null;
        string? hash = null;
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            key = request.IdempotencyKey.Trim();
            if (key.Length > 100)
            {
                return Result.Failure<OrderDetailsDto>("Idempotency key must be at most 100 characters.");
            }

            hash = ComputeHash(new
            {
                request.WarehouseId,
                request.ShippingAddressId,
                request.DeliveryMethod,
                request.CustomerComment,
                request.CustomerId,
                Items = aggregated.OrderBy(x => x.VariantId).Select(x => new { x.VariantId, x.Quantity })
            });
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                if (key is not null)
                {
                    var existing = await _db.OrderOperationIdempotencies
                        .FirstOrDefaultAsync(
                            x => x.UserId == actor.UserId && x.IdempotencyKey == key,
                            cancellationToken);
                    if (existing is not null)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        if (!string.Equals(existing.RequestHash, hash, StringComparison.Ordinal) ||
                            !string.Equals(existing.OperationType, "create", StringComparison.Ordinal))
                        {
                            return Result.Conflict<OrderDetailsDto>(
                                "Idempotency key was already used with a different request payload.",
                                ResultCodes.IdempotencyConflict);
                        }

                        var replayed = JsonSerializer.Deserialize<OrderDetailsDto>(existing.ResponseJson, JsonOptions);
                        return replayed is null
                            ? Result.Conflict<OrderDetailsDto>("Stored idempotent response could not be restored.", ResultCodes.IdempotencyConflict)
                            : Result.Success(replayed with { ReplayFromIdempotency = true });
                    }
                }

                var customerResult = await ResolveCustomerAsync(request, actor, cancellationToken);
                if (customerResult.IsFailure)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<OrderDetailsDto>(customerResult.Error!, customerResult.Code);
                }

                var customer = customerResult.Value!;

                var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);
                if (warehouse is null || !warehouse.IsActive)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<OrderDetailsDto>("Warehouse not found or inactive.", ResultCodes.NotFound);
                }

                string? deliveryAddress = null;
                if (request.ShippingAddressId.HasValue)
                {
                    var address = await _db.CustomerAddresses.FirstOrDefaultAsync(
                        a => a.Id == request.ShippingAddressId.Value, cancellationToken);
                    if (address is null || address.CustomerId != customer.Id)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<OrderDetailsDto>("Shipping address not found for this customer.", ResultCodes.NotFound);
                    }

                    deliveryAddress = FormatAddress(address);
                }

                var variantIds = aggregated.Select(x => x.VariantId).ToList();
                var variants = await _db.ProductVariants
                    .Include(v => v.Product)
                    .Where(v => variantIds.Contains(v.Id))
                    .ToListAsync(cancellationToken);

                if (variants.Count != variantIds.Count)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<OrderDetailsDto>("One or more product variants were not found.", ResultCodes.NotFound);
                }

                foreach (var line in aggregated)
                {
                    var variant = variants.First(v => v.Id == line.VariantId);
                    if (!variant.IsActive || !variant.Product.IsActive)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<OrderDetailsDto>($"Variant '{variant.Sku}' is not available for sale.");
                    }
                }

                var newStatus = await GetStatusAsync(OrderStatusCodes.New, cancellationToken);
                var now = DateTime.UtcNow;
                var orderNumber = $"ORD-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];

                var order = new Order
                {
                    OrderNumber = orderNumber,
                    CustomerId = customer.Id,
                    ManagerId = null,
                    StatusId = newStatus.Id,
                    WarehouseId = warehouse.Id,
                    ShippingAddressId = request.ShippingAddressId,
                    CustomerName = $"{customer.FirstName} {customer.LastName}".Trim(),
                    CustomerEmail = customer.Email,
                    CustomerPhone = customer.Phone,
                    DeliveryMethod = request.DeliveryMethod,
                    DeliveryAddress = deliveryAddress,
                    Comment = string.IsNullOrWhiteSpace(request.CustomerComment) ? null : request.CustomerComment.Trim(),
                    DiscountAmount = 0m,
                    DeliveryCost = 0m,
                    HasActiveReservation = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                decimal subtotal = 0m;
                foreach (var line in aggregated)
                {
                    var variant = variants.First(v => v.Id == line.VariantId);
                    var unit = variant.SellingPrice;
                    var lineSubtotal = decimal.Round(unit * line.Quantity, 2, MidpointRounding.AwayFromZero);
                    subtotal += lineSubtotal;
                    order.Items.Add(new OrderItem
                    {
                        VariantId = variant.Id,
                        ProductName = variant.Product.Name,
                        VariantName = variant.VariantName,
                        Sku = variant.Sku,
                        Quantity = line.Quantity,
                        UnitPrice = unit,
                        DiscountAmount = 0m,
                        Subtotal = lineSubtotal
                    });
                }

                order.Subtotal = subtotal;
                order.TotalAmount = decimal.Round(subtotal - order.DiscountAmount + order.DeliveryCost, 2, MidpointRounding.AwayFromZero);

                _db.Orders.Add(order);
                await _db.SaveChangesAsync(cancellationToken);

                _db.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    OldStatusId = null,
                    NewStatusId = newStatus.Id,
                    ChangedBy = actor.UserId,
                    Comment = "Order created",
                    ChangedAt = now
                });
                await _db.SaveChangesAsync(cancellationToken);

                var reservedLines = new List<(int VariantId, int Quantity)>();
                foreach (var line in aggregated)
                {
                    var reserve = await _inventory.ReserveStockAsync(
                        warehouse.Id, line.VariantId, line.Quantity, order.Id, actor.UserId, cancellationToken);
                    if (reserve.IsFailure)
                    {
                        // Compensate prior lines / draft order (InMemory ignores transactional rollback).
                        foreach (var prev in reservedLines)
                        {
                            await _inventory.ReleaseReservationAsync(
                                warehouse.Id, prev.VariantId, prev.Quantity, order.Id, actor.UserId, cancellationToken);
                        }

                        var draftHistory = await _db.OrderStatusHistories
                            .Where(h => h.OrderId == order.Id)
                            .ToListAsync(cancellationToken);
                        _db.OrderStatusHistories.RemoveRange(draftHistory);
                        _db.Orders.Remove(order);
                        await _db.SaveChangesAsync(cancellationToken);

                        await tx.RollbackAsync(cancellationToken);
                        return Result.Conflict<OrderDetailsDto>(
                            reserve.Error ?? "Insufficient stock to reserve.",
                            reserve.Code ?? ResultCodes.InsufficientStock);
                    }

                    reservedLines.Add((line.VariantId, line.Quantity));
                }

                var details = await LoadDetailsAsync(order.Id, cancellationToken)
                    ?? throw new InvalidOperationException("Order was not found after create.");

                if (key is not null && hash is not null)
                {
                    _db.OrderOperationIdempotencies.Add(new OrderOperationIdempotency
                    {
                        IdempotencyKey = key,
                        UserId = actor.UserId,
                        OperationType = "create",
                        RequestHash = hash,
                        OrderId = order.Id,
                        ResponseJson = JsonSerializer.Serialize(details, JsonOptions),
                        CreatedAt = now
                    });
                    await _db.SaveChangesAsync(cancellationToken);
                }

                await WriteAuditAsync(actor.UserId, "Order", order.Id.ToString(), AuditAction.Create,
                    $"Created {order.OrderNumber}", cancellationToken);

                await tx.CommitAsync(cancellationToken);
                return Result.Success(details);
            }
            catch (DbUpdateException) when (key is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                var raced = await _db.OrderOperationIdempotencies.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == actor.UserId && x.IdempotencyKey == key, cancellationToken);
                if (raced is not null &&
                    string.Equals(raced.RequestHash, hash, StringComparison.Ordinal) &&
                    string.Equals(raced.OperationType, "create", StringComparison.Ordinal))
                {
                    var replayed = JsonSerializer.Deserialize<OrderDetailsDto>(raced.ResponseJson, JsonOptions);
                    if (replayed is not null)
                    {
                        return Result.Success(replayed with { ReplayFromIdempotency = true });
                    }
                }

                return Result.Conflict<OrderDetailsDto>("Idempotency key conflict.", ResultCodes.IdempotencyConflict);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<PagedResult<OrderSummaryDto>> GetPagedAsync(
        OrderQuery query,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var orders = _db.Orders.AsNoTracking().AsQueryable();

        if (actor.IsCustomer && !actor.IsStaff)
        {
            var customerId = await GetCustomerIdForUserAsync(actor.UserId, cancellationToken);
            if (customerId is null)
            {
                return new PagedResult<OrderSummaryDto> { Page = page, PageSize = pageSize, TotalCount = 0, Items = [] };
            }

            orders = orders.Where(o => o.CustomerId == customerId.Value);
        }
        else if (query.CustomerId.HasValue)
        {
            orders = orders.Where(o => o.CustomerId == query.CustomerId.Value);
        }

        if (query.StatusId.HasValue)
        {
            orders = orders.Where(o => o.StatusId == query.StatusId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.StatusCode))
        {
            var code = query.StatusCode.Trim();
            orders = orders.Where(o => o.Status.Code == code);
        }

        if (!string.IsNullOrWhiteSpace(query.OrderNumber))
        {
            var number = query.OrderNumber.Trim();
            orders = orders.Where(o => o.OrderNumber.Contains(number));
        }

        if (query.FromUtc.HasValue)
        {
            orders = orders.Where(o => o.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            orders = orders.Where(o => o.CreatedAt <= query.ToUtc.Value);
        }

        orders = (query.SortBy?.ToLowerInvariant()) switch
        {
            "total" or "totalamount" => query.SortDesc
                ? orders.OrderByDescending(o => o.TotalAmount).ThenByDescending(o => o.Id)
                : orders.OrderBy(o => o.TotalAmount).ThenBy(o => o.Id),
            "ordernumber" => query.SortDesc
                ? orders.OrderByDescending(o => o.OrderNumber).ThenByDescending(o => o.Id)
                : orders.OrderBy(o => o.OrderNumber).ThenBy(o => o.Id),
            _ => query.SortDesc
                ? orders.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
                : orders.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id)
        };

        var total = await orders.CountAsync(cancellationToken);
        var items = await orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.CustomerId,
                o.CustomerName,
                o.StatusId,
                o.Status.Code,
                o.Status.Name,
                o.WarehouseId,
                o.TotalAmount,
                o.HasActiveReservation,
                o.CreatedAt,
                o.ConfirmedAt,
                o.CompletedAt,
                o.CancelledAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<OrderDetailsDto?> GetByIdAsync(int id, ActorContext actor, CancellationToken cancellationToken = default)
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

    public async Task<IReadOnlyList<OrderStatusHistoryDto>> GetHistoryAsync(
        int id,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null || !await CanAccessOrderAsync(order.CustomerId, actor, cancellationToken))
        {
            return [];
        }

        return await _db.OrderStatusHistories.AsNoTracking()
            .Where(h => h.OrderId == id)
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new OrderStatusHistoryDto(
                h.Id,
                h.OldStatusId,
                h.OldStatus != null ? h.OldStatus.Code : null,
                h.NewStatusId,
                h.NewStatus.Code,
                h.ChangedBy,
                h.Comment,
                h.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<Result<OrderDetailsDto>> ConfirmAsync(int id, ActorContext actor, CancellationToken cancellationToken = default)
        => TransitionStaffAsync(id, actor, OrderStatusCodes.Confirmed, "Confirmed", setConfirmed: true, cancellationToken);

    public Task<Result<OrderDetailsDto>> StartProcessingAsync(int id, ActorContext actor, CancellationToken cancellationToken = default)
        => TransitionStaffAsync(id, actor, OrderStatusCodes.Processing, "Started processing", cancellationToken: cancellationToken);

    public async Task<Result<OrderDetailsDto>> CompleteAsync(int id, ActorContext actor, CancellationToken cancellationToken = default)
    {
        if (!actor.IsStaff)
        {
            return Result.Failure<OrderDetailsDto>("Only staff can complete orders.", ResultCodes.Forbidden);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var order = await _db.Orders.Include(o => o.Items).Include(o => o.Status)
                    .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
                if (order is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<OrderDetailsDto>("Order not found.");
                }

                if (!OrderTransitionRules.CanFulfill(order.Status.Code) ||
                    !OrderTransitionRules.CanTransition(order.Status.Code, OrderStatusCodes.Completed))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<OrderDetailsDto>(
                        $"Cannot complete order from status '{order.Status.Code}'.",
                        ResultCodes.InvalidTransition);
                }

                if (!order.HasActiveReservation)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<OrderDetailsDto>(
                        "Order has no active reservation to fulfill.",
                        ResultCodes.InvalidTransition);
                }

                var completed = await GetStatusAsync(OrderStatusCodes.Completed, cancellationToken);
                var now = DateTime.UtcNow;

                foreach (var item in order.Items)
                {
                    if (!item.VariantId.HasValue)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<OrderDetailsDto>("Order item is missing variant reference.");
                    }

                    var fulfill = await _inventory.FulfillReservedStockAsync(
                        order.WarehouseId, item.VariantId.Value, item.Quantity, order.Id, actor.UserId, cancellationToken);
                    if (fulfill.IsFailure)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Conflict<OrderDetailsDto>(fulfill.Error!, fulfill.Code ?? ResultCodes.InsufficientStock);
                    }
                }

                var oldStatusId = order.StatusId;
                order.StatusId = completed.Id;
                order.HasActiveReservation = false;
                order.CompletedAt = now;
                order.UpdatedAt = now;

                _db.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    OldStatusId = oldStatusId,
                    NewStatusId = completed.Id,
                    ChangedBy = actor.UserId,
                    Comment = "Order completed / fulfilled",
                    ChangedAt = now
                });

                await WriteAuditAsync(actor.UserId, "Order", order.Id.ToString(), AuditAction.Update,
                    "Completed", cancellationToken);
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

    public async Task<Result<OrderDetailsDto>> CancelAsync(
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
                var order = await _db.Orders.Include(o => o.Items).Include(o => o.Status)
                    .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
                if (order is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<OrderDetailsDto>("Order not found.");
                }

                if (!await CanAccessOrderAsync(order.CustomerId, actor, cancellationToken))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<OrderDetailsDto>("Order not found.", ResultCodes.NotFound);
                }

                if (order.Status.Code == OrderStatusCodes.Cancelled)
                {
                    await tx.RollbackAsync(cancellationToken);
                    var existing = await LoadDetailsAsync(order.Id, cancellationToken);
                    return Result.Success(existing!);
                }

                if (!OrderTransitionRules.CanCancel(order.Status.Code))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<OrderDetailsDto>(
                        $"Cannot cancel order in status '{order.Status.Code}'.",
                        ResultCodes.InvalidTransition);
                }

                if (actor.IsCustomer && !actor.IsStaff &&
                    order.Status.Code is not (OrderStatusCodes.New or OrderStatusCodes.Confirmed))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<OrderDetailsDto>(
                        "Customers may cancel only New or Confirmed orders.",
                        ResultCodes.Forbidden);
                }

                if (order.HasActiveReservation)
                {
                    foreach (var item in order.Items)
                    {
                        if (!item.VariantId.HasValue)
                        {
                            continue;
                        }

                        var release = await _inventory.ReleaseReservationAsync(
                            order.WarehouseId, item.VariantId.Value, item.Quantity, order.Id, actor.UserId, cancellationToken);
                        if (release.IsFailure)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return Result.Conflict<OrderDetailsDto>(release.Error!, release.Code ?? ResultCodes.ConcurrencyConflict);
                        }
                    }
                }

                var cancelled = await GetStatusAsync(OrderStatusCodes.Cancelled, cancellationToken);
                var now = DateTime.UtcNow;
                var oldStatusId = order.StatusId;
                order.StatusId = cancelled.Id;
                order.HasActiveReservation = false;
                order.CancelledAt = now;
                order.UpdatedAt = now;

                _db.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    OldStatusId = oldStatusId,
                    NewStatusId = cancelled.Id,
                    ChangedBy = actor.UserId,
                    Comment = string.IsNullOrWhiteSpace(comment) ? "Cancelled" : comment.Trim(),
                    ChangedAt = now
                });

                await WriteAuditAsync(actor.UserId, "Order", order.Id.ToString(), AuditAction.Update,
                    "Cancelled", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return Result.Success((await LoadDetailsAsync(order.Id, cancellationToken))!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<Result<OrderPeriodSummaryDto>> GetPeriodSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        int? statusId,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsStaff)
        {
            return Result.Failure<OrderPeriodSummaryDto>("Reports require staff access.", ResultCodes.Forbidden);
        }

        var rangeError = ValidateReportRange(fromUtc, toUtc);
        if (rangeError is not null)
        {
            return Result.Failure<OrderPeriodSummaryDto>(rangeError);
        }

        var query = _db.Orders.AsNoTracking().Where(o => o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc);
        if (statusId.HasValue)
        {
            query = query.Where(o => o.StatusId == statusId.Value);
        }

        var completedStatusId = await _db.OrderStatuses.AsNoTracking()
            .Where(s => s.Code == OrderStatusCodes.Completed)
            .Select(s => s.Id)
            .FirstAsync(cancellationToken);
        var cancelledStatusId = await _db.OrderStatuses.AsNoTracking()
            .Where(s => s.Code == OrderStatusCodes.Cancelled)
            .Select(s => s.Id)
            .FirstAsync(cancellationToken);

        var totalOrders = await query.CountAsync(cancellationToken);
        var totalAmountAll = await query.SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var completedCount = await query.CountAsync(o => o.StatusId == completedStatusId, cancellationToken);
        var totalAmountCompleted = await query.Where(o => o.StatusId == completedStatusId)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;
        var cancelledCount = await query.CountAsync(o => o.StatusId == cancelledStatusId, cancellationToken);

        var statusLookup = await _db.OrderStatuses.AsNoTracking()
            .Select(s => new { s.Id, s.Code, s.Name })
            .ToListAsync(cancellationToken);

        var grouped = await query
            .GroupBy(o => o.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count(), TotalAmount = g.Sum(x => x.TotalAmount) })
            .ToListAsync(cancellationToken);

        var byStatus = grouped
            .Select(g =>
            {
                var st = statusLookup.First(s => s.Id == g.StatusId);
                return new OrderStatusCountDto(g.StatusId, st.Code, st.Name, g.Count, g.TotalAmount);
            })
            .OrderBy(x => x.StatusCode)
            .ToList();

        return Result.Success(new OrderPeriodSummaryDto(
            fromUtc, toUtc, totalOrders, totalAmountAll, totalAmountCompleted, completedCount, cancelledCount, byStatus));
    }

    public async Task<Result<IReadOnlyList<OrdersByDayDto>>> GetByDayReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsStaff)
        {
            return Result.Failure<IReadOnlyList<OrdersByDayDto>>("Reports require staff access.", ResultCodes.Forbidden);
        }

        var rangeError = ValidateReportRange(fromUtc, toUtc);
        if (rangeError is not null)
        {
            return Result.Failure<IReadOnlyList<OrdersByDayDto>>(rangeError);
        }

        var completedStatusId = await _db.OrderStatuses.AsNoTracking()
            .Where(s => s.Code == OrderStatusCodes.Completed)
            .Select(s => s.Id)
            .FirstAsync(cancellationToken);

        var rows = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new
            {
                Day = g.Key,
                OrderCount = g.Count(),
                TotalAmount = g.Sum(x => x.TotalAmount),
                CompletedCount = g.Count(x => x.StatusId == completedStatusId)
            })
            .OrderBy(x => x.Day)
            .ToListAsync(cancellationToken);

        IReadOnlyList<OrdersByDayDto> result = rows
            .Select(r => new OrdersByDayDto(DateOnly.FromDateTime(r.Day), r.OrderCount, r.TotalAmount, r.CompletedCount))
            .ToList();
        return Result.Success(result);
    }

    private async Task<Result<OrderDetailsDto>> TransitionStaffAsync(
        int id,
        ActorContext actor,
        string toCode,
        string comment,
        bool setConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsStaff)
        {
            return Result.Failure<OrderDetailsDto>("Only staff can change this status.", ResultCodes.Forbidden);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var order = await _db.Orders.Include(o => o.Status)
                    .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
                if (order is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.NotFound<OrderDetailsDto>("Order not found.");
                }

                if (!OrderTransitionRules.CanTransition(order.Status.Code, toCode))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Conflict<OrderDetailsDto>(
                        $"Transition from '{order.Status.Code}' to '{toCode}' is not allowed.",
                        ResultCodes.InvalidTransition);
                }

                var newStatus = await GetStatusAsync(toCode, cancellationToken);
                var now = DateTime.UtcNow;
                var oldStatusId = order.StatusId;
                order.StatusId = newStatus.Id;
                order.UpdatedAt = now;
                if (setConfirmed)
                {
                    order.ConfirmedAt = now;
                }

                _db.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    OldStatusId = oldStatusId,
                    NewStatusId = newStatus.Id,
                    ChangedBy = actor.UserId,
                    Comment = comment,
                    ChangedAt = now
                });

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return Result.Success((await LoadDetailsAsync(order.Id, cancellationToken))!);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<Result<Customer>> ResolveCustomerAsync(
        CreateOrderRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        if (actor.IsCustomer && !actor.IsStaff)
        {
            var linked = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == actor.UserId, cancellationToken);
            if (linked is null || !linked.IsActive)
            {
                return Result.Failure<Customer>("No customer profile is linked to this user.", ResultCodes.Forbidden);
            }

            return Result.Success(linked);
        }

        if (!actor.IsStaff)
        {
            return Result.Failure<Customer>("Not authorized to create orders.", ResultCodes.Forbidden);
        }

        if (!request.CustomerId.HasValue)
        {
            return Result.Failure<Customer>("CustomerId is required when staff creates an order.");
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value, cancellationToken);
        if (customer is null || !customer.IsActive)
        {
            return Result.Failure<Customer>("Customer not found or inactive.", ResultCodes.NotFound);
        }

        return Result.Success(customer);
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

    private async Task<OrderStatus> GetStatusAsync(string code, CancellationToken cancellationToken)
    {
        var status = await _db.OrderStatuses.FirstOrDefaultAsync(s => s.Code == code, cancellationToken);
        return status ?? throw new InvalidOperationException($"Order status '{code}' is not seeded.");
    }

    private async Task<OrderDetailsDto?> LoadDetailsAsync(int id, CancellationToken cancellationToken)
    {
        return await _db.Orders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrderDetailsDto(
                o.Id,
                o.OrderNumber,
                o.CustomerId,
                o.CustomerName,
                o.CustomerEmail,
                o.CustomerPhone,
                o.StatusId,
                o.Status.Code,
                o.Status.Name,
                o.WarehouseId,
                o.ShippingAddressId,
                o.DeliveryMethod,
                o.DeliveryAddress,
                o.Comment,
                o.Subtotal,
                o.DiscountAmount,
                o.DeliveryCost,
                o.TotalAmount,
                o.HasActiveReservation,
                false,
                o.CreatedAt,
                o.ConfirmedAt,
                o.CompletedAt,
                o.CancelledAt,
                o.Items.OrderBy(i => i.Id).Select(i => new OrderItemDto(
                    i.Id,
                    i.VariantId,
                    i.ProductName,
                    i.VariantName,
                    i.Sku,
                    i.Quantity,
                    i.UnitPrice,
                    i.DiscountAmount,
                    i.Subtotal)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string FormatAddress(CustomerAddress a)
    {
        var parts = new[]
        {
            a.Country,
            a.Region,
            a.City,
            a.Street,
            a.Building,
            a.Apartment,
            a.PostalCode
        }.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(", ", parts);
    }

    private static string? ValidateReportRange(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc < fromUtc)
        {
            return "Report 'to' must be greater than or equal to 'from'.";
        }

        if ((toUtc - fromUtc).TotalDays > 366)
        {
            return "Report range cannot exceed 366 days.";
        }

        return null;
    }

    private static string ComputeHash(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
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
}
