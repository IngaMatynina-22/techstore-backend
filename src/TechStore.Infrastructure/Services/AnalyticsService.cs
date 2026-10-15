using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Reports;
using TechStore.Domain.Common;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _db;

    public AnalyticsService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<SalesSummaryDto>> GetSalesSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var rangeError = ValidateRange(fromUtc, toUtc);
        if (rangeError is not null)
        {
            return Result.Failure<SalesSummaryDto>(rangeError);
        }

        var completedId = await StatusIdAsync(OrderStatusCodes.Completed, cancellationToken);
        var cancelledId = await StatusIdAsync(OrderStatusCodes.Cancelled, cancellationToken);

        var orders = _db.Orders.AsNoTracking().Where(o => o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc);
        var totalOrders = await orders.CountAsync(cancellationToken);
        var completedOrders = await orders.CountAsync(o => o.StatusId == completedId, cancellationToken);
        var cancelledOrders = await orders.CountAsync(o => o.StatusId == cancelledId, cancellationToken);
        var gross = await orders.SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;
        var completedTotal = await orders.Where(o => o.StatusId == completedId)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;
        var aov = completedOrders == 0 ? 0m : decimal.Round(completedTotal / completedOrders, 2, MidpointRounding.AwayFromZero);

        var returnTotal = await _db.ReturnItems.AsNoTracking()
            .Where(i => i.ReturnRequest.CreatedAt >= fromUtc &&
                        i.ReturnRequest.CreatedAt <= toUtc &&
                        i.ReturnRequest.Status != ReturnRequestStatus.Rejected)
            .SumAsync(i => (decimal?)i.LineTotal, cancellationToken) ?? 0m;

        return Result.Success(new SalesSummaryDto(
            fromUtc,
            toUtc,
            totalOrders,
            completedOrders,
            cancelledOrders,
            gross,
            completedTotal,
            aov,
            returnTotal,
            "Completed sales use OrderStatus Code=Completed. Gross includes all statuses. Return totals exclude Rejected."));
    }

    public async Task<Result<InventoryOverviewDto>> GetInventoryOverviewAsync(
        int? warehouseId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Inventory.AsNoTracking().AsQueryable();
        if (warehouseId.HasValue)
        {
            query = query.Where(i => i.WarehouseId == warehouseId.Value);
        }

        var skuCount = await query.CountAsync(cancellationToken);
        var onHand = await query.SumAsync(i => (int?)i.QuantityOnHand, cancellationToken) ?? 0;
        var reserved = await query.SumAsync(i => (int?)i.QuantityReserved, cancellationToken) ?? 0;
        var low = await query.CountAsync(
            i => i.Variant.LowStockThreshold != null && i.QuantityOnHand <= i.Variant.LowStockThreshold,
            cancellationToken);
        var zero = await query.CountAsync(i => i.QuantityOnHand == 0, cancellationToken);
        var valuation = await query
            .Where(i => i.QuantityOnHand > 0)
            .SumAsync(i => (decimal?)(i.QuantityOnHand * i.Variant.PurchasePrice), cancellationToken) ?? 0m;

        return Result.Success(new InventoryOverviewDto(
            warehouseId,
            skuCount,
            onHand,
            reserved,
            onHand - reserved,
            low,
            zero,
            valuation,
            "Valuation uses current ProductVariant.PurchasePrice (not FIFO/average)."));
    }

    public async Task<Result<PurchasingSummaryDto>> GetPurchasingSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var rangeError = ValidateRange(fromUtc, toUtc);
        if (rangeError is not null)
        {
            return Result.Failure<PurchasingSummaryDto>(rangeError);
        }

        var pos = _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt <= toUtc);
        var count = await pos.CountAsync(cancellationToken);
        var receivedQty = await pos.SelectMany(p => p.Items)
            .SumAsync(i => (int?)i.ReceivedQuantity, cancellationToken) ?? 0;
        var receivedTotal = await pos.SelectMany(p => p.Items)
            .SumAsync(i => (decimal?)(i.ReceivedQuantity * i.UnitCost), cancellationToken) ?? 0m;

        return Result.Success(new PurchasingSummaryDto(
            fromUtc,
            toUtc,
            count,
            receivedQty,
            receivedTotal,
            "Received totals use purchase-order line UnitCost snapshots × ReceivedQuantity."));
    }

    private async Task<int> StatusIdAsync(string code, CancellationToken cancellationToken)
        => await _db.OrderStatuses.AsNoTracking()
            .Where(s => s.Code == code)
            .Select(s => s.Id)
            .FirstAsync(cancellationToken);

    private static string? ValidateRange(DateTime fromUtc, DateTime toUtc)
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
}
