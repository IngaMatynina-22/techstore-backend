using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class SupplierProduct : BaseEntity
{
    public int SupplierId { get; set; }
    public int VariantId { get; set; }
    public string? SupplierSku { get; set; }
    public decimal SupplyPrice { get; set; }
    public int? DeliveryDays { get; set; }
    public bool IsPreferred { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
