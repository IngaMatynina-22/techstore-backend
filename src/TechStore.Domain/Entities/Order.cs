using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class Order : AuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int? ManagerId { get; set; }
    public int StatusId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public DeliveryMethod DeliveryMethod { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Comment { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DeliveryCost { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public OrderStatus Status { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Shipment> Shipments { get; set; } = [];
    public ICollection<OrderDocument> Documents { get; set; } = [];
}
