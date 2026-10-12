using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class OrderStatusHistory : BaseEntity
{
    public int OrderId { get; set; }
    public int? OldStatusId { get; set; }
    public int NewStatusId { get; set; }
    public int? ChangedBy { get; set; }
    public string? Comment { get; set; }
    public DateTime ChangedAt { get; set; }

    public Order Order { get; set; } = null!;
    public OrderStatus? OldStatus { get; set; }
    public OrderStatus NewStatus { get; set; } = null!;
}
