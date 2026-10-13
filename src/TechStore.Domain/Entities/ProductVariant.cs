using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class ProductVariant : AuditableEntity
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? Weight { get; set; }

    /// <summary>
    /// Optional global low-stock threshold for this variant. Null means threshold not configured.
    /// </summary>
    public int? LowStockThreshold { get; set; }

    public bool IsActive { get; set; } = true;

    public Product Product { get; set; } = null!;
    public ICollection<VariantAttributeValue> AttributeValues { get; set; } = [];
    public ICollection<SupplierProduct> SupplierProducts { get; set; } = [];
    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
