using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class InventoryCountSession : AuditableEntity
{
    public string SessionNumber { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public InventoryCountStatus Status { get; set; } = InventoryCountStatus.Draft;
    public int? CreatedByUserId { get; set; }
    public DateTime? SnapshotTakenAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Notes { get; set; }
    public bool AdjustmentsApplied { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<InventoryCountLine> Lines { get; set; } = [];
}
