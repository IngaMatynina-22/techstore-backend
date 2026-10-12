using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class OrderStatus : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsFinal { get; set; }

    public ICollection<Order> Orders { get; set; } = [];
}
