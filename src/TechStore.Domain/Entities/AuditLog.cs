using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class AuditLog : BaseEntity
{
    public int? UserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
