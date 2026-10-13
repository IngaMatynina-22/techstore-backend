using TechStore.Application.Common;

namespace TechStore.Application.Features.Inventory;

public interface IInventoryService
{
    Task<PagedResult<InventoryItemDto>> GetPagedAsync(InventoryQuery query, CancellationToken cancellationToken = default);
    Task<InventoryItemDto?> GetAsync(int warehouseId, int variantId, CancellationToken cancellationToken = default);
    Task<PagedResult<InventoryItemDto>> GetLowStockAsync(InventoryQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<InventoryItemDto>> GetOutOfStockAsync(InventoryQuery query, CancellationToken cancellationToken = default);
    Task<Result> SetLowStockThresholdAsync(int variantId, SetLowStockThresholdRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<StockMovementDto>> GetMovementsAsync(StockMovementQuery query, CancellationToken cancellationToken = default);
    Task<StockMovementDto?> GetMovementByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<StockOperationResultDto>> ReceiveAsync(ReceiptRequest request, int? userId, CancellationToken cancellationToken = default);
    Task<Result<StockOperationResultDto>> IssueAsync(IssueRequest request, int? userId, CancellationToken cancellationToken = default);
    Task<Result<StockOperationResultDto>> AdjustAsync(AdjustmentRequest request, int? userId, CancellationToken cancellationToken = default);
    Task<Result<TransferOperationResultDto>> TransferAsync(TransferRequest request, int? userId, CancellationToken cancellationToken = default);

    Task<WarehouseSummaryDto?> GetWarehouseSummaryAsync(int warehouseId, CancellationToken cancellationToken = default);
    Task<StockMovementsPeriodReportDto> GetMovementsPeriodReportAsync(int? warehouseId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<InventoryValuationDto?> GetInventoryValuationAsync(int warehouseId, CancellationToken cancellationToken = default);
}
