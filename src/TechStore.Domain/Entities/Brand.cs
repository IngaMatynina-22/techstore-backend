using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class Brand : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = [];
}
