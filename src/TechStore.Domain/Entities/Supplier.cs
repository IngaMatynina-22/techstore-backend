using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class Supplier : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<SupplierProduct> SupplierProducts { get; set; } = [];
}
