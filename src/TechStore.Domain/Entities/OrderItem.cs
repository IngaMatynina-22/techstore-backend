using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public int? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Subtotal { get; set; }

    public Order Order { get; set; } = null!;
    public ProductVariant? Variant { get; set; }
}
