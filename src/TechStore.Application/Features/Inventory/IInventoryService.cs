using TechStore.Application.Common;
using TechStore.Domain.Enums;

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

    /// <summary>Mutates reserved stock; caller owns the transaction.</summary>
    Task<Result> ReserveStockAsync(int warehouseId, int variantId, int quantity, int orderId, int? userId, CancellationToken cancellationToken = default);

    /// <summary>Releases reserved stock; caller owns the transaction.</summary>
    Task<Result> ReleaseReservationAsync(int warehouseId, int variantId, int quantity, int orderId, int? userId, CancellationToken cancellationToken = default);

    /// <summary>Decrements on-hand and reserved together; caller owns the transaction.</summary>
    Task<Result> FulfillReservedStockAsync(int warehouseId, int variantId, int quantity, int orderId, int? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increases on-hand stock and writes a stock movement without opening a transaction.
    /// Caller must provide the ambient transaction boundary (e.g. purchase-order receive, return restock).
    /// Defaults to <see cref="StockMovementType.Receipt"/>; pass <see cref="StockMovementType.Return"/> for customer returns.
    /// </summary>
    Task<Result> ApplyReceiptInAmbientTransactionAsync(
        int warehouseId,
        int variantId,
        int quantity,
        decimal? unitCost,
        string referenceType,
        int referenceId,
        int? userId,
        string? comment,
        CancellationToken cancellationToken = default,
        StockMovementType movementType = StockMovementType.Receipt);

    Task<WarehouseSummaryDto?> GetWarehouseSummaryAsync(int warehouseId, CancellationToken cancellationToken = default);
    Task<StockMovementsPeriodReportDto> GetMovementsPeriodReportAsync(int? warehouseId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<InventoryValuationDto?> GetInventoryValuationAsync(int warehouseId, CancellationToken cancellationToken = default);
    Task<InventoryIntegrityReportDto> GetIntegrityReportAsync(CancellationToken cancellationToken = default);
}
