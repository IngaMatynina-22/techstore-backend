using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Inventory;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;

    public InventoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<InventoryItemDto>> GetPagedAsync(
        InventoryQuery query,
        CancellationToken cancellationToken = default)
    {
        return await QueryInventoryAsync(query, forceLowStock: false, forceOutOfStock: false, cancellationToken);
    }

    public async Task<PagedResult<InventoryItemDto>> GetLowStockAsync(
        InventoryQuery query,
        CancellationToken cancellationToken = default)
    {
        return await QueryInventoryAsync(query, forceLowStock: true, forceOutOfStock: false, cancellationToken);
    }

    public async Task<PagedResult<InventoryItemDto>> GetOutOfStockAsync(
        InventoryQuery query,
        CancellationToken cancellationToken = default)
    {
        return await QueryInventoryAsync(query, forceLowStock: false, forceOutOfStock: true, cancellationToken);
    }

    public async Task<InventoryItemDto?> GetAsync(
        int warehouseId,
        int variantId,
        CancellationToken cancellationToken = default)
    {
        return await BuildInventoryProjection(
                _db.Inventory.AsNoTracking()
                    .Where(i => i.WarehouseId == warehouseId && i.VariantId == variantId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result> SetLowStockThresholdAsync(
        int variantId,
        SetLowStockThresholdRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.LowStockThreshold is < 0)
        {
            return Result.Failure("Low stock threshold cannot be negative.");
        }

        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return Result.NotFound("Product variant not found.");
        }

        variant.LowStockThreshold = request.LowStockThreshold;
        variant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<PagedResult<StockMovementDto>> GetMovementsAsync(
        StockMovementQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var movements = _db.StockMovements.AsNoTracking().AsQueryable();

        if (query.WarehouseId.HasValue)
        {
            movements = movements.Where(m => m.WarehouseId == query.WarehouseId.Value);
        }

        if (query.VariantId.HasValue)
        {
            movements = movements.Where(m => m.VariantId == query.VariantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Sku))
        {
            var sku = query.Sku.Trim();
            movements = movements.Where(m => m.Variant.Sku.Contains(sku));
        }

        if (query.MovementType.HasValue)
        {
            movements = movements.Where(m => m.MovementType == query.MovementType.Value);
        }

        if (query.UserId.HasValue)
        {
            movements = movements.Where(m => m.UserId == query.UserId.Value);
        }

        if (query.TransferGroupId.HasValue)
        {
            movements = movements.Where(m => m.TransferGroupId == query.TransferGroupId.Value);
        }

        if (query.FromUtc.HasValue)
        {
            movements = movements.Where(m => m.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            movements = movements.Where(m => m.CreatedAt <= query.ToUtc.Value);
        }

        var total = await movements.CountAsync(cancellationToken);
        var items = await movements
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new StockMovementDto(
                m.Id,
                m.WarehouseId,
                m.Warehouse.Name,
                m.VariantId,
                m.Variant.Sku,
                m.Variant.VariantName,
                m.Variant.Product.Name,
                m.MovementType,
                m.Quantity,
                m.QuantityBefore,
                m.QuantityAfter,
                m.UnitCost,
                m.ReferenceType,
                m.ReferenceId,
                m.TransferGroupId,
                m.Comment,
                m.UserId,
                m.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<StockMovementDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<StockMovementDto?> GetMovementByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.StockMovements
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new StockMovementDto(
                m.Id,
                m.WarehouseId,
                m.Warehouse.Name,
                m.VariantId,
                m.Variant.Sku,
                m.Variant.VariantName,
                m.Variant.Product.Name,
                m.MovementType,
                m.Quantity,
                m.QuantityBefore,
                m.QuantityAfter,
                m.UnitCost,
                m.ReferenceType,
                m.ReferenceId,
                m.TransferGroupId,
                m.Comment,
                m.UserId,
                m.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<StockOperationResultDto>> ReceiveAsync(
        ReceiptRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (!InventoryRules.IsPositiveQuantity(request.Quantity))
        {
            return Result.Failure<StockOperationResultDto>("Receipt quantity must be greater than zero.");
        }

        return await ExecuteIdempotentAsync(
            request.IdempotencyKey,
            "receipt",
            request,
            userId,
            async () =>
            {
                var validation = await ValidateWarehouseAndVariantAsync(
                    request.WarehouseId,
                    request.VariantId,
                    requireActiveWarehouse: true,
                    requireActiveVariant: true,
                    cancellationToken);
                if (validation.IsFailure)
                {
                    return Result.Failure<StockOperationResultDto>(validation.Error!, validation.Code);
                }

                var now = DateTime.UtcNow;
                var item = await GetOrCreateInventoryAsync(request.WarehouseId, request.VariantId, now, cancellationToken);

                var affected = await ApplyQuantityDeltaAsync(item.Id, request.Quantity, requireAvailable: false, now, cancellationToken);
                if (affected == 0)
                {
                    return Result.Conflict<StockOperationResultDto>(
                        "Inventory update failed due to a concurrent change.",
                        ResultCodes.ConcurrencyConflict);
                }

                var after = await ReadOnHandAsync(item.Id, cancellationToken);
                var before = after - request.Quantity;
                var movement = new StockMovement
                {
                    WarehouseId = request.WarehouseId,
                    VariantId = request.VariantId,
                    UserId = userId,
                    MovementType = StockMovementType.Receipt,
                    Quantity = request.Quantity,
                    QuantityBefore = before,
                    QuantityAfter = after,
                    UnitCost = request.UnitCost,
                    ReferenceType = string.IsNullOrWhiteSpace(request.DocumentNumber) ? null : "Document",
                    Comment = BuildComment(request.Comment, request.DocumentNumber),
                    CreatedAt = now
                };

                _db.StockMovements.Add(movement);
                await WriteAuditAsync(userId, "InventoryItem", item.Id.ToString(), AuditAction.Update,
                    $"Receipt +{request.Quantity}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                return Result.Success(ToOperationResult(movement, after, item.QuantityReserved, replay: false));
            },
            cancellationToken);
    }

    public async Task<Result<StockOperationResultDto>> IssueAsync(
        IssueRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (!InventoryRules.IsPositiveQuantity(request.Quantity))
        {
            return Result.Failure<StockOperationResultDto>("Issue quantity must be greater than zero.");
        }

        return await ExecuteIdempotentAsync(
            request.IdempotencyKey,
            "issue",
            request,
            userId,
            async () =>
            {
                var validation = await ValidateWarehouseAndVariantAsync(
                    request.WarehouseId,
                    request.VariantId,
                    requireActiveWarehouse: true,
                    requireActiveVariant: true,
                    cancellationToken);
                if (validation.IsFailure)
                {
                    return Result.Failure<StockOperationResultDto>(validation.Error!, validation.Code);
                }

                var now = DateTime.UtcNow;

                var item = await _db.Inventory
                    .FirstOrDefaultAsync(
                        i => i.WarehouseId == request.WarehouseId && i.VariantId == request.VariantId,
                        cancellationToken);

                if (item is null)
                {
                    return Result.Conflict<StockOperationResultDto>(
                        "Insufficient available stock.",
                        ResultCodes.InsufficientStock);
                }

                var qty = request.Quantity;

                var affected = await ApplyQuantityDeltaAsync(item.Id, -qty, requireAvailable: true, now, cancellationToken);
                if (affected == 0)
                {
                    return Result.Conflict<StockOperationResultDto>(
                        "Insufficient available stock.",
                        ResultCodes.InsufficientStock);
                }

                var after = await ReadOnHandAsync(item.Id, cancellationToken);
                var before = after + qty;
                var movement = new StockMovement
                {
                    WarehouseId = request.WarehouseId,
                    VariantId = request.VariantId,
                    UserId = userId,
                    MovementType = StockMovementType.WriteOff,
                    Quantity = -qty,
                    QuantityBefore = before,
                    QuantityAfter = after,
                    Comment = Normalize(request.Comment),
                    CreatedAt = now
                };

                _db.StockMovements.Add(movement);
                await WriteAuditAsync(userId, "InventoryItem", item.Id.ToString(), AuditAction.Update,
                    $"Issue -{qty}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                return Result.Success(ToOperationResult(movement, after, item.QuantityReserved, replay: false));
            },
            cancellationToken);
    }

    public async Task<Result<StockOperationResultDto>> AdjustAsync(
        AdjustmentRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (request.TargetQuantityOnHand < 0)
        {
            return Result.Failure<StockOperationResultDto>("Target quantity cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure<StockOperationResultDto>("Adjustment reason is required.");
        }

        return await ExecuteIdempotentAsync(
            request.IdempotencyKey,
            "adjustment",
            request,
            userId,
            async () =>
            {
                var validation = await ValidateWarehouseAndVariantAsync(
                    request.WarehouseId,
                    request.VariantId,
                    requireActiveWarehouse: true,
                    requireActiveVariant: false,
                    cancellationToken);
                if (validation.IsFailure)
                {
                    return Result.Failure<StockOperationResultDto>(validation.Error!, validation.Code);
                }

                var now = DateTime.UtcNow;
                var item = await GetOrCreateInventoryAsync(request.WarehouseId, request.VariantId, now, cancellationToken);

                if (request.TargetQuantityOnHand < item.QuantityReserved)
                {
                    return Result.Conflict<StockOperationResultDto>(
                        "Target quantity cannot be less than reserved quantity.",
                        ResultCodes.InsufficientStock);
                }

                var before = item.QuantityOnHand;
                var delta = InventoryRules.ComputeAdjustmentDelta(before, request.TargetQuantityOnHand);
                if (delta == 0)
                {
                    return Result.Failure<StockOperationResultDto>(
                        "No adjustment required; quantity already matches the target.",
                        ResultCodes.NoChange);
                }

                var affected = await ApplyAbsoluteQuantityAsync(
                    item.Id,
                    before,
                    request.TargetQuantityOnHand,
                    now,
                    cancellationToken);
                if (affected == 0)
                {
                    return Result.Conflict<StockOperationResultDto>(
                        "Inventory was changed by another operation. Retry the adjustment.",
                        ResultCodes.ConcurrencyConflict);
                }

                var movement = new StockMovement
                {
                    WarehouseId = request.WarehouseId,
                    VariantId = request.VariantId,
                    UserId = userId,
                    MovementType = StockMovementType.Adjustment,
                    Quantity = delta,
                    QuantityBefore = before,
                    QuantityAfter = request.TargetQuantityOnHand,
                    Comment = request.Reason.Trim(),
                    CreatedAt = now
                };

                _db.StockMovements.Add(movement);
                await WriteAuditAsync(userId, "InventoryItem", item.Id.ToString(), AuditAction.Update,
                    $"Adjustment {before}->{request.TargetQuantityOnHand}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                return Result.Success(ToOperationResult(movement, request.TargetQuantityOnHand, item.QuantityReserved, replay: false));
            },
            cancellationToken);
    }

    public async Task<Result<TransferOperationResultDto>> TransferAsync(
        TransferRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (request.SourceWarehouseId == request.DestinationWarehouseId)
        {
            return Result.Failure<TransferOperationResultDto>("Source and destination warehouses must differ.");
        }

        if (!InventoryRules.IsPositiveQuantity(request.Quantity))
        {
            return Result.Failure<TransferOperationResultDto>("Transfer quantity must be greater than zero.");
        }

        return await ExecuteIdempotentAsync(
            request.IdempotencyKey,
            "transfer",
            request,
            userId,
            async () =>
            {
                var sourceValidation = await ValidateWarehouseAndVariantAsync(
                    request.SourceWarehouseId,
                    request.VariantId,
                    requireActiveWarehouse: true,
                    requireActiveVariant: true,
                    cancellationToken);
                if (sourceValidation.IsFailure)
                {
                    return Result.Failure<TransferOperationResultDto>(sourceValidation.Error!, sourceValidation.Code);
                }

                var destValidation = await ValidateWarehouseAndVariantAsync(
                    request.DestinationWarehouseId,
                    request.VariantId,
                    requireActiveWarehouse: true,
                    requireActiveVariant: true,
                    cancellationToken);
                if (destValidation.IsFailure)
                {
                    return Result.Failure<TransferOperationResultDto>(destValidation.Error!, destValidation.Code);
                }

                var now = DateTime.UtcNow;
                var transferGroupId = Guid.NewGuid();
                var qty = request.Quantity;

                var source = await _db.Inventory.FirstOrDefaultAsync(
                    i => i.WarehouseId == request.SourceWarehouseId && i.VariantId == request.VariantId,
                    cancellationToken);
                if (source is null)
                {
                    return Result.Conflict<TransferOperationResultDto>(
                        "Insufficient available stock at source warehouse.",
                        ResultCodes.InsufficientStock);
                }

                var sourceBefore = source.QuantityOnHand;
                var sourceAffected = await ApplyQuantityDeltaAsync(source.Id, -qty, requireAvailable: true, now, cancellationToken);
                if (sourceAffected == 0)
                {
                    return Result.Conflict<TransferOperationResultDto>(
                        "Insufficient available stock at source warehouse.",
                        ResultCodes.InsufficientStock);
                }

                var destination = await GetOrCreateInventoryAsync(
                    request.DestinationWarehouseId,
                    request.VariantId,
                    now,
                    cancellationToken);
                var destBefore = destination.QuantityOnHand;

                var destAffected = await ApplyQuantityDeltaAsync(destination.Id, qty, requireAvailable: false, now, cancellationToken);
                if (destAffected == 0)
                {
                    return Result.Conflict<TransferOperationResultDto>(
                        "Failed to update destination inventory.",
                        ResultCodes.ConcurrencyConflict);
                }

                var outbound = new StockMovement
                {
                    WarehouseId = request.SourceWarehouseId,
                    VariantId = request.VariantId,
                    UserId = userId,
                    MovementType = StockMovementType.TransferOut,
                    Quantity = -qty,
                    QuantityBefore = sourceBefore,
                    QuantityAfter = sourceBefore - qty,
                    TransferGroupId = transferGroupId,
                    ReferenceType = "Transfer",
                    Comment = Normalize(request.Comment),
                    CreatedAt = now
                };

                var inbound = new StockMovement
                {
                    WarehouseId = request.DestinationWarehouseId,
                    VariantId = request.VariantId,
                    UserId = userId,
                    MovementType = StockMovementType.TransferIn,
                    Quantity = qty,
                    QuantityBefore = destBefore,
                    QuantityAfter = destBefore + qty,
                    TransferGroupId = transferGroupId,
                    ReferenceType = "Transfer",
                    Comment = Normalize(request.Comment),
                    CreatedAt = now
                };

                _db.StockMovements.AddRange(outbound, inbound);
                await WriteAuditAsync(userId, "InventoryItem", source.Id.ToString(), AuditAction.Update,
                    $"Transfer {qty} to warehouse {request.DestinationWarehouseId} group={transferGroupId}", cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                var result = new TransferOperationResultDto(
                    transferGroupId,
                    ToOperationResult(outbound, sourceBefore - qty, source.QuantityReserved, replay: false),
                    ToOperationResult(inbound, destBefore + qty, destination.QuantityReserved, replay: false),
                    ReplayFromIdempotency: false);

                return Result.Success(result);
            },
            cancellationToken);
    }

    public async Task<WarehouseSummaryDto?> GetWarehouseSummaryAsync(
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await _db.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return null;
        }

        var stats = await _db.Inventory.AsNoTracking()
            .Where(i => i.WarehouseId == warehouseId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                SkuCount = g.Count(),
                Positive = g.Count(i => i.QuantityOnHand > 0),
                Zero = g.Count(i => i.QuantityOnHand == 0),
                Low = g.Count(i =>
                    i.Variant.LowStockThreshold != null &&
                    i.QuantityOnHand <= i.Variant.LowStockThreshold),
                Units = g.Sum(i => i.QuantityOnHand)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new WarehouseSummaryDto(
            warehouse.Id,
            warehouse.Name,
            stats?.SkuCount ?? 0,
            stats?.Positive ?? 0,
            stats?.Zero ?? 0,
            stats?.Low ?? 0,
            stats?.Units ?? 0);
    }

    public async Task<StockMovementsPeriodReportDto> GetMovementsPeriodReportAsync(
        int? warehouseId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (toUtc < fromUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        var query = _db.StockMovements.AsNoTracking()
            .Where(m => m.CreatedAt >= fromUtc && m.CreatedAt <= toUtc);

        if (warehouseId.HasValue)
        {
            query = query.Where(m => m.WarehouseId == warehouseId.Value);
        }

        var aggregates = await query
            .GroupBy(m => m.MovementType)
            .Select(g => new MovementTypeAggregateDto(
                g.Key,
                g.Count(),
                g.Sum(m => m.Quantity < 0 ? -m.Quantity : m.Quantity)))
            .OrderBy(a => a.MovementType)
            .ToListAsync(cancellationToken);

        return new StockMovementsPeriodReportDto(warehouseId, fromUtc, toUtc, aggregates);
    }

    public async Task<InventoryValuationDto?> GetInventoryValuationAsync(
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await _db.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return null;
        }

        var rows = await _db.Inventory.AsNoTracking()
            .Where(i => i.WarehouseId == warehouseId && i.QuantityOnHand > 0)
            .Select(i => new
            {
                i.QuantityOnHand,
                i.Variant.PurchasePrice
            })
            .ToListAsync(cancellationToken);

        var valued = rows.Where(r => r.PurchasePrice >= 0).ToList();
        var amount = valued.Sum(r => r.PurchasePrice * r.QuantityOnHand);
        var units = valued.Sum(r => r.QuantityOnHand);

        return new InventoryValuationDto(
            warehouse.Id,
            warehouse.Name,
            amount,
            valued.Count,
            0,
            units,
            "Valuation uses current ProductVariant.PurchasePrice (not FIFO/average costing).");
    }

    public async Task<InventoryIntegrityReportDto> GetIntegrityReportAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<InventoryIntegrityIssueDto>();

        var negative = await _db.Inventory.AsNoTracking()
            .Where(i => i.QuantityOnHand < 0 || i.QuantityReserved < 0)
            .Select(i => new { i.Id, i.WarehouseId, i.VariantId, i.QuantityOnHand, i.QuantityReserved })
            .ToListAsync(cancellationToken);
        foreach (var row in negative)
        {
            issues.Add(new InventoryIntegrityIssueDto(
                "negative_quantity",
                "critical",
                row.Id,
                row.WarehouseId,
                row.VariantId,
                $"OnHand={row.QuantityOnHand}, Reserved={row.QuantityReserved}"));
        }

        var overReserved = await _db.Inventory.AsNoTracking()
            .Where(i => i.QuantityReserved > i.QuantityOnHand)
            .Select(i => new { i.Id, i.WarehouseId, i.VariantId, i.QuantityOnHand, i.QuantityReserved })
            .ToListAsync(cancellationToken);
        foreach (var row in overReserved)
        {
            issues.Add(new InventoryIntegrityIssueDto(
                "reserved_exceeds_on_hand",
                "critical",
                row.Id,
                row.WarehouseId,
                row.VariantId,
                $"OnHand={row.QuantityOnHand}, Reserved={row.QuantityReserved}"));
        }

        var duplicates = await _db.Inventory.AsNoTracking()
            .GroupBy(i => new { i.WarehouseId, i.VariantId })
            .Where(g => g.Count() > 1)
            .Select(g => new { g.Key.WarehouseId, g.Key.VariantId, Count = g.Count() })
            .ToListAsync(cancellationToken);
        foreach (var dup in duplicates)
        {
            issues.Add(new InventoryIntegrityIssueDto(
                "duplicate_inventory_row",
                "critical",
                null,
                dup.WarehouseId,
                dup.VariantId,
                $"Found {dup.Count} inventory rows for the same warehouse/variant."));
        }

        var orphanWarehouse = await _db.Inventory.AsNoTracking()
            .Where(i => !_db.Warehouses.Any(w => w.Id == i.WarehouseId))
            .Select(i => new { i.Id, i.WarehouseId, i.VariantId })
            .ToListAsync(cancellationToken);
        foreach (var row in orphanWarehouse)
        {
            issues.Add(new InventoryIntegrityIssueDto(
                "missing_warehouse",
                "high",
                row.Id,
                row.WarehouseId,
                row.VariantId,
                "Inventory row references a missing warehouse."));
        }

        var orphanVariant = await _db.Inventory.AsNoTracking()
            .Where(i => !_db.ProductVariants.Any(v => v.Id == i.VariantId))
            .Select(i => new { i.Id, i.WarehouseId, i.VariantId })
            .ToListAsync(cancellationToken);
        foreach (var row in orphanVariant)
        {
            issues.Add(new InventoryIntegrityIssueDto(
                "missing_variant",
                "high",
                row.Id,
                row.WarehouseId,
                row.VariantId,
                "Inventory row references a missing product variant."));
        }

        var activeReservedOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.HasActiveReservation)
            .Select(o => new { o.Id, o.WarehouseId, ReservedUnits = o.Items.Sum(i => i.Quantity) })
            .ToListAsync(cancellationToken);

        var warehouseReserved = await _db.Inventory.AsNoTracking()
            .GroupBy(i => i.WarehouseId)
            .Select(g => new { WarehouseId = g.Key, Reserved = g.Sum(i => i.QuantityReserved) })
            .ToListAsync(cancellationToken);

        foreach (var wh in warehouseReserved)
        {
            var orderReserved = activeReservedOrders.Where(o => o.WarehouseId == wh.WarehouseId).Sum(o => o.ReservedUnits);
            if (wh.Reserved != orderReserved)
            {
                issues.Add(new InventoryIntegrityIssueDto(
                    "reservation_mismatch",
                    "medium",
                    null,
                    wh.WarehouseId,
                    null,
                    $"Warehouse reserved aggregate={wh.Reserved}, active order reserved lines={orderReserved}. Aggregate reservation model may diverge if non-order holds exist."));
            }
        }

        return new InventoryIntegrityReportDto(
            DateTime.UtcNow,
            issues.Count,
            issues,
            "Read-only integrity scan. Corrections must use explicit authorized adjustments. Aggregate QuantityReserved is used (no separate reservation ledger).");
    }

    private async Task<PagedResult<InventoryItemDto>> QueryInventoryAsync(
        InventoryQuery query,
        bool forceLowStock,
        bool forceOutOfStock,
        CancellationToken cancellationToken)
    {
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);
        var entityQuery = _db.Inventory.AsNoTracking().AsQueryable();

        if (query.WarehouseId.HasValue)
        {
            entityQuery = entityQuery.Where(i => i.WarehouseId == query.WarehouseId.Value);
        }

        if (query.VariantId.HasValue)
        {
            entityQuery = entityQuery.Where(i => i.VariantId == query.VariantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Sku))
        {
            var sku = query.Sku.Trim();
            entityQuery = entityQuery.Where(i => i.Variant.Sku.Contains(sku));
        }

        if (!string.IsNullOrWhiteSpace(query.ProductName))
        {
            var name = query.ProductName.Trim();
            entityQuery = entityQuery.Where(i => i.Variant.Product.Name.Contains(name));
        }

        if (query.CategoryId.HasValue)
        {
            entityQuery = entityQuery.Where(i => i.Variant.Product.CategoryId == query.CategoryId.Value);
        }

        if (query.BrandId.HasValue)
        {
            entityQuery = entityQuery.Where(i => i.Variant.Product.BrandId == query.BrandId.Value);
        }

        if (query.MaxQuantityOnHand.HasValue)
        {
            entityQuery = entityQuery.Where(i => i.QuantityOnHand <= query.MaxQuantityOnHand.Value);
        }

        if (forceOutOfStock || query.OutOfStockOnly == true)
        {
            entityQuery = entityQuery.Where(i => i.QuantityOnHand == 0);
        }

        if (forceLowStock || query.LowStockOnly == true)
        {
            entityQuery = entityQuery.Where(i =>
                i.Variant.LowStockThreshold != null &&
                i.QuantityOnHand <= i.Variant.LowStockThreshold);
        }

        entityQuery = (query.SortBy?.ToLowerInvariant()) switch
        {
            "quantity" or "quantityonhand" => query.SortDesc
                ? entityQuery.OrderByDescending(i => i.QuantityOnHand).ThenBy(i => i.Id)
                : entityQuery.OrderBy(i => i.QuantityOnHand).ThenBy(i => i.Id),
            "name" or "productname" => query.SortDesc
                ? entityQuery.OrderByDescending(i => i.Variant.Product.Name).ThenBy(i => i.Id)
                : entityQuery.OrderBy(i => i.Variant.Product.Name).ThenBy(i => i.Id),
            _ => query.SortDesc
                ? entityQuery.OrderByDescending(i => i.Variant.Sku).ThenBy(i => i.Id)
                : entityQuery.OrderBy(i => i.Variant.Sku).ThenBy(i => i.Id)
        };

        var total = await entityQuery.CountAsync(cancellationToken);
        var items = await BuildInventoryProjection(entityQuery)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    private static IQueryable<InventoryItemDto> BuildInventoryProjection(IQueryable<InventoryItem> query) =>
        query.Select(i => new InventoryItemDto(
            i.Id,
            i.WarehouseId,
            i.Warehouse.Name,
            i.Warehouse.Code,
            i.VariantId,
            i.Variant.Sku,
            i.Variant.VariantName,
            i.Variant.ProductId,
            i.Variant.Product.Name,
            i.Variant.Product.Brand != null ? i.Variant.Product.Brand.Name : null,
            i.Variant.Product.Category.Name,
            i.QuantityOnHand,
            i.QuantityReserved,
            i.QuantityOnHand - i.QuantityReserved,
            i.Variant.LowStockThreshold,
            i.Variant.LowStockThreshold != null && i.QuantityOnHand <= i.Variant.LowStockThreshold,
            i.UpdatedAt));

    private async Task<InventoryItem> GetOrCreateInventoryAsync(
        int warehouseId,
        int variantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Inventory
            .FirstOrDefaultAsync(i => i.WarehouseId == warehouseId && i.VariantId == variantId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new InventoryItem
        {
            WarehouseId = warehouseId,
            VariantId = variantId,
            QuantityOnHand = 0,
            QuantityReserved = 0,
            UpdatedAt = now
        };

        _db.Inventory.Add(created);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            _db.Entry(created).State = EntityState.Detached;
            var raced = await _db.Inventory
                .FirstAsync(i => i.WarehouseId == warehouseId && i.VariantId == variantId, cancellationToken);
            return raced;
        }
    }

    private async Task<Result> ValidateWarehouseAndVariantAsync(
        int warehouseId,
        int variantId,
        bool requireActiveWarehouse,
        bool requireActiveVariant,
        CancellationToken cancellationToken)
    {
        var warehouse = await _db.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return Result.NotFound("Warehouse not found.");
        }

        if (requireActiveWarehouse && !warehouse.IsActive)
        {
            return Result.Failure("Warehouse is inactive.");
        }

        var variant = await _db.ProductVariants.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return Result.NotFound("Product variant not found.");
        }

        if (requireActiveVariant && !variant.IsActive)
        {
            return Result.Failure("Product variant is inactive.");
        }

        return Result.Success();
    }

    private async Task<Result<T>> ExecuteIdempotentAsync<T>(
        string? idempotencyKey,
        string operationType,
        object request,
        int? userId,
        Func<Task<Result<T>>> action,
        CancellationToken cancellationToken)
    {
        string? key = null;
        string? hash = null;
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            key = idempotencyKey.Trim();
            if (key.Length > 100)
            {
                return Result.Failure<T>("Idempotency key must be at most 100 characters.");
            }

            hash = ComputeHash(request);
        }

        // Required when EnableRetryOnFailure is enabled: user transactions must run inside the strategy.
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            if (key is not null)
            {
                var existing = await _db.StockOperationIdempotencies
                    .FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
                if (existing is not null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    if (!string.Equals(existing.RequestHash, hash, StringComparison.Ordinal) ||
                        !string.Equals(existing.OperationType, operationType, StringComparison.Ordinal))
                    {
                        return Result.Conflict<T>(
                            "Idempotency key was already used with a different request payload.",
                            ResultCodes.IdempotencyConflict);
                    }

                    var replayed = JsonSerializer.Deserialize<T>(existing.ResponseJson, JsonOptions);
                    return replayed is null
                        ? Result.Conflict<T>("Stored idempotent response could not be restored.", ResultCodes.IdempotencyConflict)
                        : Result.Success(MarkReplay(replayed));
                }
            }

            try
            {
                var result = await action();
                if (result.IsFailure || result.Value is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return result;
                }

                var payload = MarkReplay(result.Value, replay: false);
                if (key is not null && hash is not null)
                {
                    _db.StockOperationIdempotencies.Add(new StockOperationIdempotency
                    {
                        IdempotencyKey = key,
                        UserId = userId,
                        OperationType = operationType,
                        RequestHash = hash,
                        ResponseJson = JsonSerializer.Serialize(payload, JsonOptions),
                        CreatedAt = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync(cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
                return Result.Success(payload);
            }
            catch (DbUpdateException) when (key is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                var raced = await _db.StockOperationIdempotencies.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
                if (raced is not null &&
                    string.Equals(raced.RequestHash, hash, StringComparison.Ordinal) &&
                    string.Equals(raced.OperationType, operationType, StringComparison.Ordinal))
                {
                    var replayed = JsonSerializer.Deserialize<T>(raced.ResponseJson, JsonOptions);
                    if (replayed is not null)
                    {
                        return Result.Success(MarkReplay(replayed));
                    }
                }

                return Result.Conflict<T>("Idempotency key conflict.", ResultCodes.IdempotencyConflict);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<int> ApplyQuantityDeltaAsync(
        int inventoryId,
        int delta,
        bool requireAvailable,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // SQL Server: conditional UPDATE (atomic check + write). InMemory lacks ExecuteUpdate.
        if (!_db.Database.IsRelational())
        {
            var item = await _db.Inventory.FirstOrDefaultAsync(i => i.Id == inventoryId, cancellationToken);
            if (item is null)
            {
                return 0;
            }

            if (requireAvailable && delta < 0 && item.AvailableQuantity < -delta)
            {
                return 0;
            }

            item.QuantityOnHand += delta;
            item.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return 1;
        }

        if (requireAvailable && delta < 0)
        {
            var need = -delta;
            return await _db.Inventory
                .Where(i => i.Id == inventoryId && i.QuantityOnHand - i.QuantityReserved >= need)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(i => i.QuantityOnHand, i => i.QuantityOnHand + delta)
                        .SetProperty(i => i.UpdatedAt, now),
                    cancellationToken);
        }

        return await _db.Inventory
            .Where(i => i.Id == inventoryId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(i => i.QuantityOnHand, i => i.QuantityOnHand + delta)
                    .SetProperty(i => i.UpdatedAt, now),
                cancellationToken);
    }

    private async Task<int> ApplyAbsoluteQuantityAsync(
        int inventoryId,
        int expectedCurrent,
        int target,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
        {
            var item = await _db.Inventory.FirstOrDefaultAsync(
                i => i.Id == inventoryId &&
                     i.QuantityOnHand == expectedCurrent &&
                     i.QuantityReserved <= target,
                cancellationToken);
            if (item is null)
            {
                return 0;
            }

            item.QuantityOnHand = target;
            item.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return 1;
        }

        return await _db.Inventory
            .Where(i =>
                i.Id == inventoryId &&
                i.QuantityOnHand == expectedCurrent &&
                i.QuantityReserved <= target)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(i => i.QuantityOnHand, target)
                    .SetProperty(i => i.UpdatedAt, now),
                cancellationToken);
    }

    private async Task<int> ReadOnHandAsync(int inventoryId, CancellationToken cancellationToken)
    {
        return await _db.Inventory.AsNoTracking()
            .Where(i => i.Id == inventoryId)
            .Select(i => i.QuantityOnHand)
            .SingleAsync(cancellationToken);
    }

    /// <summary>
    /// Increases reserved quantity when available stock is sufficient. Does not open a transaction —
    /// callers must provide the ambient transaction boundary.
    /// </summary>
    public async Task<Result> ReserveStockAsync(
        int warehouseId,
        int variantId,
        int quantity,
        int orderId,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (!InventoryRules.IsPositiveQuantity(quantity))
        {
            return Result.Failure("Reserve quantity must be greater than zero.");
        }

        var validation = await ValidateWarehouseAndVariantAsync(
            warehouseId, variantId, requireActiveWarehouse: true, requireActiveVariant: true, cancellationToken);
        if (validation.IsFailure)
        {
            return validation;
        }

        var now = DateTime.UtcNow;
        var item = await GetOrCreateInventoryAsync(warehouseId, variantId, now, cancellationToken);
        var onHandBefore = item.QuantityOnHand;

        var affected = await ApplyReservedDeltaAsync(item.Id, quantity, requireAvailable: true, now, cancellationToken);
        if (affected == 0)
        {
            return Result.Conflict("Insufficient available stock to reserve.", ResultCodes.InsufficientStock);
        }

        _db.StockMovements.Add(new StockMovement
        {
            WarehouseId = warehouseId,
            VariantId = variantId,
            UserId = userId,
            MovementType = StockMovementType.Reservation,
            Quantity = quantity,
            QuantityBefore = onHandBefore,
            QuantityAfter = onHandBefore,
            ReferenceType = "Order",
            ReferenceId = orderId,
            Comment = $"Reserve for order #{orderId}",
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Decreases reserved quantity. Does not open a transaction.
    /// </summary>
    public async Task<Result> ReleaseReservationAsync(
        int warehouseId,
        int variantId,
        int quantity,
        int orderId,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (!InventoryRules.IsPositiveQuantity(quantity))
        {
            return Result.Failure("Release quantity must be greater than zero.");
        }

        var item = await _db.Inventory.FirstOrDefaultAsync(
            i => i.WarehouseId == warehouseId && i.VariantId == variantId, cancellationToken);
        if (item is null)
        {
            return Result.Conflict("No inventory row to release reservation from.", ResultCodes.ConcurrencyConflict);
        }

        var now = DateTime.UtcNow;
        var onHand = item.QuantityOnHand;
        var affected = await ApplyReservedDeltaAsync(item.Id, -quantity, requireAvailable: false, now, cancellationToken);
        if (affected == 0)
        {
            return Result.Conflict("Reservation release conflict.", ResultCodes.ConcurrencyConflict);
        }

        _db.StockMovements.Add(new StockMovement
        {
            WarehouseId = warehouseId,
            VariantId = variantId,
            UserId = userId,
            MovementType = StockMovementType.ReleaseReservation,
            Quantity = -quantity,
            QuantityBefore = onHand,
            QuantityAfter = onHand,
            ReferenceType = "Order",
            ReferenceId = orderId,
            Comment = $"Release reservation for order #{orderId}",
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Increases on-hand and writes a Receipt movement. Does not open a transaction —
    /// callers must provide the ambient transaction boundary.
    /// </summary>
    public async Task<Result> ApplyReceiptInAmbientTransactionAsync(
        int warehouseId,
        int variantId,
        int quantity,
        decimal? unitCost,
        string referenceType,
        int referenceId,
        int? userId,
        string? comment,
        CancellationToken cancellationToken = default,
        StockMovementType movementType = StockMovementType.Receipt)
    {
        if (!InventoryRules.IsPositiveQuantity(quantity))
        {
            return Result.Failure("Receipt quantity must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(referenceType))
        {
            return Result.Failure("Reference type is required.");
        }

        if (movementType is not (StockMovementType.Receipt or StockMovementType.Return))
        {
            return Result.Failure("Ambient stock increase supports Receipt or Return movement types only.");
        }

        var validation = await ValidateWarehouseAndVariantAsync(
            warehouseId, variantId, requireActiveWarehouse: true, requireActiveVariant: true, cancellationToken);
        if (validation.IsFailure)
        {
            return validation;
        }

        var now = DateTime.UtcNow;
        var item = await GetOrCreateInventoryAsync(warehouseId, variantId, now, cancellationToken);

        var affected = await ApplyQuantityDeltaAsync(item.Id, quantity, requireAvailable: false, now, cancellationToken);
        if (affected == 0)
        {
            return Result.Conflict(
                "Inventory update failed due to a concurrent change.",
                ResultCodes.ConcurrencyConflict);
        }

        var after = await ReadOnHandAsync(item.Id, cancellationToken);
        var before = after - quantity;

        _db.StockMovements.Add(new StockMovement
        {
            WarehouseId = warehouseId,
            VariantId = variantId,
            UserId = userId,
            MovementType = movementType,
            Quantity = quantity,
            QuantityBefore = before,
            QuantityAfter = after,
            UnitCost = unitCost,
            ReferenceType = referenceType.Trim(),
            ReferenceId = referenceId,
            Comment = Normalize(comment),
            CreatedAt = now
        });

        await WriteAuditAsync(
            userId,
            "InventoryItem",
            item.Id.ToString(),
            AuditAction.Update,
            $"{movementType} +{quantity} ref={referenceType.Trim()}:{referenceId}",
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Decreases on-hand and reserved together (order fulfillment). Does not open a transaction.
    /// </summary>
    public async Task<Result> FulfillReservedStockAsync(
        int warehouseId,
        int variantId,
        int quantity,
        int orderId,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (!InventoryRules.IsPositiveQuantity(quantity))
        {
            return Result.Failure("Fulfill quantity must be greater than zero.");
        }

        var item = await _db.Inventory.FirstOrDefaultAsync(
            i => i.WarehouseId == warehouseId && i.VariantId == variantId, cancellationToken);
        if (item is null)
        {
            return Result.Conflict("No inventory row to fulfill.", ResultCodes.InsufficientStock);
        }

        var now = DateTime.UtcNow;
        var before = item.QuantityOnHand;
        var affected = await ApplyFulfillAsync(item.Id, quantity, now, cancellationToken);
        if (affected == 0)
        {
            return Result.Conflict("Cannot fulfill reserved stock.", ResultCodes.InsufficientStock);
        }

        var after = await ReadOnHandAsync(item.Id, cancellationToken);
        _db.StockMovements.Add(new StockMovement
        {
            WarehouseId = warehouseId,
            VariantId = variantId,
            UserId = userId,
            MovementType = StockMovementType.Sale,
            Quantity = -quantity,
            QuantityBefore = before,
            QuantityAfter = after,
            ReferenceType = "Order",
            ReferenceId = orderId,
            Comment = $"Fulfill order #{orderId}",
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<int> ApplyReservedDeltaAsync(
        int inventoryId,
        int reservedDelta,
        bool requireAvailable,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
        {
            var item = await _db.Inventory.FirstOrDefaultAsync(i => i.Id == inventoryId, cancellationToken);
            if (item is null)
            {
                return 0;
            }

            if (requireAvailable && reservedDelta > 0 && item.AvailableQuantity < reservedDelta)
            {
                return 0;
            }

            if (reservedDelta < 0 && item.QuantityReserved < -reservedDelta)
            {
                return 0;
            }

            item.QuantityReserved += reservedDelta;
            item.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return 1;
        }

        if (requireAvailable && reservedDelta > 0)
        {
            return await _db.Inventory
                .Where(i => i.Id == inventoryId && i.QuantityOnHand - i.QuantityReserved >= reservedDelta)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(i => i.QuantityReserved, i => i.QuantityReserved + reservedDelta)
                        .SetProperty(i => i.UpdatedAt, now),
                    cancellationToken);
        }

        if (reservedDelta < 0)
        {
            var need = -reservedDelta;
            return await _db.Inventory
                .Where(i => i.Id == inventoryId && i.QuantityReserved >= need)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(i => i.QuantityReserved, i => i.QuantityReserved + reservedDelta)
                        .SetProperty(i => i.UpdatedAt, now),
                    cancellationToken);
        }

        return await _db.Inventory
            .Where(i => i.Id == inventoryId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(i => i.QuantityReserved, i => i.QuantityReserved + reservedDelta)
                    .SetProperty(i => i.UpdatedAt, now),
                cancellationToken);
    }

    private async Task<int> ApplyFulfillAsync(
        int inventoryId,
        int quantity,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
        {
            var item = await _db.Inventory.FirstOrDefaultAsync(i => i.Id == inventoryId, cancellationToken);
            if (item is null || item.QuantityOnHand < quantity || item.QuantityReserved < quantity)
            {
                return 0;
            }

            item.QuantityOnHand -= quantity;
            item.QuantityReserved -= quantity;
            item.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return 1;
        }

        return await _db.Inventory
            .Where(i =>
                i.Id == inventoryId &&
                i.QuantityOnHand >= quantity &&
                i.QuantityReserved >= quantity)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(i => i.QuantityOnHand, i => i.QuantityOnHand - quantity)
                    .SetProperty(i => i.QuantityReserved, i => i.QuantityReserved - quantity)
                    .SetProperty(i => i.UpdatedAt, now),
                cancellationToken);
    }

    private static T MarkReplay<T>(T value, bool replay = true)
    {
        if (value is StockOperationResultDto op)
        {
            return (T)(object)(op with { ReplayFromIdempotency = replay });
        }

        if (value is TransferOperationResultDto transfer)
        {
            return (T)(object)(transfer with { ReplayFromIdempotency = replay });
        }

        return value;
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

    private static StockOperationResultDto ToOperationResult(
        StockMovement movement,
        int onHand,
        int reserved,
        bool replay) =>
        new(
            movement.Id,
            movement.TransferGroupId,
            movement.WarehouseId,
            movement.VariantId,
            onHand,
            reserved,
            onHand - reserved,
            movement.QuantityBefore,
            movement.QuantityAfter,
            movement.MovementType,
            replay);

    private static string ComputeHash(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static string? BuildComment(string? comment, string? documentNumber)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(documentNumber))
        {
            parts.Add($"Doc: {documentNumber.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(comment))
        {
            parts.Add(comment.Trim());
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize) => pageSize is < 1 or > 100 ? 20 : pageSize;
}
