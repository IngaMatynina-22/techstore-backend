using TechStore.Domain.Enums;

namespace TechStore.Application.Features.Returns;

public class CreateReturnItemRequest
{
    public int OrderItemId { get; set; }
    public int Quantity { get; set; }
    public string? ReasonCode { get; set; }
    public ReturnDisposition Disposition { get; set; } = ReturnDisposition.RestockSellable;
}

public class CreateReturnRequest
{
    public int OrderId { get; set; }
    public List<CreateReturnItemRequest> Items { get; set; } = [];
    public string? Comment { get; set; }
}

public class ReturnQuery
{
    public ReturnRequestStatus? Status { get; set; }
    public int? OrderId { get; set; }
    public int? CustomerId { get; set; }
    public string? ReturnNumber { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string SortBy { get; set; } = "createdAt";
    public bool SortDesc { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class StaffReturnCommentRequest
{
    public string? Comment { get; set; }
}

public record ReturnItemDto(
    int Id,
    int OrderItemId,
    int? VariantId,
    string SkuSnapshot,
    string ProductNameSnapshot,
    int Quantity,
    decimal UnitPriceSnapshot,
    decimal LineTotal,
    string? ReasonCode,
    ReturnDisposition Disposition);

public record ReturnStatusHistoryDto(
    int Id,
    ReturnRequestStatus? OldStatus,
    ReturnRequestStatus NewStatus,
    int? ChangedBy,
    string? Comment,
    DateTime ChangedAt);

public record ReturnSummaryDto(
    int Id,
    string ReturnNumber,
    int OrderId,
    string OrderNumber,
    int CustomerId,
    string CustomerName,
    int WarehouseId,
    ReturnRequestStatus Status,
    bool InventoryApplied,
    int ItemCount,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    DateTime? ReceivedAt,
    DateTime? ClosedAt);

public record ReturnDetailsDto(
    int Id,
    string ReturnNumber,
    int OrderId,
    string OrderNumber,
    int CustomerId,
    string CustomerName,
    int WarehouseId,
    ReturnRequestStatus Status,
    string? CustomerComment,
    string? StaffComment,
    int? RequestedByUserId,
    int? ReviewedByUserId,
    bool InventoryApplied,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    DateTime? ReceivedAt,
    DateTime? ClosedAt,
    decimal TotalAmount,
    IReadOnlyList<ReturnItemDto> Items,
    IReadOnlyList<ReturnStatusHistoryDto> StatusHistory);
