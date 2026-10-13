using TechStore.Domain.Enums;

namespace TechStore.Application.Features.Orders;

public class CreateOrderItemRequest
{
    public int ProductVariantId { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderRequest
{
    public List<CreateOrderItemRequest> Items { get; set; } = [];
    public int WarehouseId { get; set; }
    public int? ShippingAddressId { get; set; }
    public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Courier;
    public string? CustomerComment { get; set; }
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Admin/Manager only: create on behalf of this customer. Ignored for Customer role.
    /// </summary>
    public int? CustomerId { get; set; }
}

public class OrderQuery
{
    public int? StatusId { get; set; }
    public string? StatusCode { get; set; }
    public int? CustomerId { get; set; }
    public string? OrderNumber { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string SortBy { get; set; } = "createdAt";
    public bool SortDesc { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record OrderItemDto(
    int Id,
    int? VariantId,
    string ProductName,
    string? VariantName,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal Subtotal);

public record OrderStatusHistoryDto(
    int Id,
    int? OldStatusId,
    string? OldStatusCode,
    int NewStatusId,
    string NewStatusCode,
    int? ChangedBy,
    string? Comment,
    DateTime ChangedAt);

public record OrderSummaryDto(
    int Id,
    string OrderNumber,
    int CustomerId,
    string CustomerName,
    int StatusId,
    string StatusCode,
    string StatusName,
    int WarehouseId,
    decimal TotalAmount,
    bool HasActiveReservation,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt);

public record OrderDetailsDto(
    int Id,
    string OrderNumber,
    int CustomerId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    int StatusId,
    string StatusCode,
    string StatusName,
    int WarehouseId,
    int? ShippingAddressId,
    DeliveryMethod DeliveryMethod,
    string? DeliveryAddress,
    string? Comment,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal DeliveryCost,
    decimal TotalAmount,
    bool HasActiveReservation,
    bool ReplayFromIdempotency,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    IReadOnlyList<OrderItemDto> Items);

public record OrderPeriodSummaryDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalOrders,
    decimal TotalAmountAll,
    decimal TotalAmountCompleted,
    int CompletedCount,
    int CancelledCount,
    IReadOnlyList<OrderStatusCountDto> ByStatus);

public record OrderStatusCountDto(int StatusId, string StatusCode, string StatusName, int Count, decimal TotalAmount);

public record OrdersByDayDto(DateOnly Day, int OrderCount, decimal TotalAmount, int CompletedCount);

public class ActorContext
{
    public int UserId { get; init; }
    public bool IsStaff { get; init; }
    public bool IsCustomer { get; init; }
}
