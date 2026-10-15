using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class PurchaseOrderStatusHistory : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public PurchaseOrderStatus? OldStatus { get; set; }
    public PurchaseOrderStatus NewStatus { get; set; }
    public int? ChangedBy { get; set; }
    public string? Comment { get; set; }
    public DateTime ChangedAt { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
