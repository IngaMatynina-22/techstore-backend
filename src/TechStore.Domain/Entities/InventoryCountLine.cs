using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class InventoryCountLine : BaseEntity
{
    public int SessionId { get; set; }
    public int VariantId { get; set; }
    public int InventoryItemId { get; set; }
    public int ExpectedQuantity { get; set; }
    public int? ActualQuantity { get; set; }
    public int? AppliedMovementId { get; set; }
    public byte[] ExpectedRowVersion { get; set; } = Array.Empty<byte>();

    public int? Difference => ActualQuantity.HasValue ? ActualQuantity.Value - ExpectedQuantity : null;

    public InventoryCountSession Session { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
}
