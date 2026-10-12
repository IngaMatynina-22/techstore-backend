using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class Warehouse : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
}
