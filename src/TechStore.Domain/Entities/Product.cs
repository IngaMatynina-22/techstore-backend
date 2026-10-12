using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class Product : AuditableEntity
{
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? ImageUrl { get; set; }
    public int? WarrantyMonths { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public Category Category { get; set; } = null!;
    public Brand? Brand { get; set; }
    public ICollection<ProductImage> Images { get; set; } = [];
    public ICollection<ProductVariant> Variants { get; set; } = [];
}
