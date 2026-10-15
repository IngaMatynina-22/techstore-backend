using TechStore.Application.Common;

namespace TechStore.Application.Features.Reports;

public interface IAnalyticsService
{
    Task<Result<SalesSummaryDto>> GetSalesSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<Result<InventoryOverviewDto>> GetInventoryOverviewAsync(int? warehouseId, CancellationToken cancellationToken = default);
    Task<Result<PurchasingSummaryDto>> GetPurchasingSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
