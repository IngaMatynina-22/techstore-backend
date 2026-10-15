using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class ReturnRequest : AuditableEntity
{
    public string ReturnNumber { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public int WarehouseId { get; set; }
    public ReturnRequestStatus Status { get; set; } = ReturnRequestStatus.Requested;
    public string? CustomerComment { get; set; }
    public string? StaffComment { get; set; }
    public int? RequestedByUserId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool InventoryApplied { get; set; }

    public Order Order { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<ReturnItem> Items { get; set; } = [];
    public ICollection<ReturnStatusHistory> StatusHistory { get; set; } = [];
}
