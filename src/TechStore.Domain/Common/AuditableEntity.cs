namespace TechStore.Domain.Common;

public abstract class AuditableEntity : BaseEntity, IAuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
