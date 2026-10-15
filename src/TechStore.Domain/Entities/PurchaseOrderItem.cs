using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class PurchaseOrderItem : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public int VariantId { get; set; }
    public string SkuSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public int OrderedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }

    public int RemainingQuantity => OrderedQuantity - ReceivedQuantity;

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
