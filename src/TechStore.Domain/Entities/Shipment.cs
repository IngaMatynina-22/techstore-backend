using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class Shipment : AuditableEntity
{
    public int OrderId { get; set; }
    public int? AddressId { get; set; }
    public string? DeliveryService { get; set; }
    public string? TrackingNumber { get; set; }
    public ShipmentStatus ShipmentStatus { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientPhone { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? Comment { get; set; }

    public Order Order { get; set; } = null!;
    public CustomerAddress? Address { get; set; }
}
