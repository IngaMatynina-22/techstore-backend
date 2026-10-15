namespace TechStore.Application.Features.Reports;

public record SalesSummaryDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalOrders,
    int CompletedOrders,
    int CancelledOrders,
    decimal GrossOrderTotal,
    decimal CompletedSalesTotal,
    decimal AverageCompletedOrderValue,
    decimal ReturnLineTotal,
    string Notes);

public record InventoryOverviewDto(
    int? WarehouseId,
    int SkuCount,
    int TotalOnHand,
    int TotalReserved,
    int TotalAvailable,
    int LowStockSkuCount,
    int OutOfStockSkuCount,
    decimal ValuationByPurchasePrice,
    string Notes);

public record PurchasingSummaryDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int PurchaseOrderCount,
    int ReceivedLineQuantity,
    decimal ReceivedPurchaseTotal,
    string Notes);
