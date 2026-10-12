using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class StockMovement : BaseEntity
{
    public int WarehouseId { get; set; }
    public int VariantId { get; set; }
    public int? UserId { get; set; }
    public StockMovementType MovementType { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
