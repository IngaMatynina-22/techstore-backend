using TechStore.Domain.Enums;

namespace TechStore.Application.Features.Purchasing;

public class PurchaseOrderQuery
{
    public PurchaseOrderStatus? Status { get; set; }
    public int? SupplierId { get; set; }
    public int? WarehouseId { get; set; }
    public string? OrderNumber { get; set; }
    public string? Search { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class CreatePurchaseOrderItemRequest
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitCost { get; set; }
}

public class CreatePurchaseOrderRequest
{
    public int SupplierId { get; set; }
    public int WarehouseId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
    public List<CreatePurchaseOrderItemRequest> Items { get; set; } = [];
}

public class UpdatePurchaseOrderRequest
{
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
}

public class SetPurchaseOrderItemsRequest
{
    public List<CreatePurchaseOrderItemRequest> Items { get; set; } = [];
}

public class ReceivePurchaseOrderLineRequest
{
    public int PurchaseOrderItemId { get; set; }
    public int Quantity { get; set; }
}

public class ReceivePurchaseOrderRequest
{
    public List<ReceivePurchaseOrderLineRequest> Lines { get; set; } = [];
    public string? Comment { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class CancelPurchaseOrderRequest
{
    public string? Comment { get; set; }
}

public record PurchaseOrderItemDto(
    int Id,
    int VariantId,
    string SkuSnapshot,
    string ProductNameSnapshot,
    int OrderedQuantity,
    int ReceivedQuantity,
    int RemainingQuantity,
    decimal UnitCost,
    decimal LineTotal);

public record PurchaseOrderStatusHistoryDto(
    int Id,
    PurchaseOrderStatus? OldStatus,
    PurchaseOrderStatus NewStatus,
    int? ChangedBy,
    string? Comment,
    DateTime ChangedAt);

public record PurchaseOrderSummaryDto(
    int Id,
    string OrderNumber,
    int SupplierId,
    string SupplierName,
    int WarehouseId,
    string WarehouseName,
    PurchaseOrderStatus Status,
    DateTime? ExpectedDeliveryDate,
    int ItemCount,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? FullyReceivedAt,
    DateTime? CancelledAt);

public record PurchaseOrderDetailsDto(
    int Id,
    string OrderNumber,
    int SupplierId,
    string SupplierName,
    int WarehouseId,
    string WarehouseName,
    PurchaseOrderStatus Status,
    DateTime? ExpectedDeliveryDate,
    string? Notes,
    int? CreatedByUserId,
    bool ReplayFromIdempotency,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ConfirmedAt,
    DateTime? FullyReceivedAt,
    DateTime? CancelledAt,
    decimal TotalAmount,
    IReadOnlyList<PurchaseOrderItemDto> Items,
    IReadOnlyList<PurchaseOrderStatusHistoryDto> StatusHistory);
