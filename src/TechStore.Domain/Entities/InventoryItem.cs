using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

/// <summary>
/// Maps to the Inventory table. Named InventoryItem to avoid clash with namespace patterns.
/// </summary>
public class InventoryItem : BaseEntity
{
    public int WarehouseId { get; set; }
    public int VariantId { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
