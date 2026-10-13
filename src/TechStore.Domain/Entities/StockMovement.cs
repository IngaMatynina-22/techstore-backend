using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class StockMovement : BaseEntity
{
    public int WarehouseId { get; set; }
    public int VariantId { get; set; }
    public int? UserId { get; set; }
    public StockMovementType MovementType { get; set; }

    /// <summary>
    /// Signed quantity: positive increases on-hand for receipts/transfer-in;
    /// negative decreases for issues/transfer-out; adjustments store the computed delta.
    /// </summary>
    public int Quantity { get; set; }

    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public decimal? UnitCost { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }

    /// <summary>
    /// Links TransferOut and TransferIn rows of a single transfer operation.
    /// </summary>
    public Guid? TransferGroupId { get; set; }

    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
