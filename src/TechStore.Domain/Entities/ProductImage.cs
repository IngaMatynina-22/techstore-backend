using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class ProductImage : BaseEntity
{
    public int ProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }

    public Product Product { get; set; } = null!;
}
