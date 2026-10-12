using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class Category : AuditableEntity
{
    public int? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<AttributeDefinition> AttributeDefinitions { get; set; } = [];
}
