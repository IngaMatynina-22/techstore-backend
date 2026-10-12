using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class CustomerAddress : AuditableEntity
{
    public int CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Apartment { get; set; }
    public string? PostalCode { get; set; }
    public string? DeliveryNotes { get; set; }
    public bool IsDefault { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<Shipment> Shipments { get; set; } = [];
}
