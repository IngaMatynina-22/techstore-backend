namespace TechStore.Application.Features.Categories;

public record CategoryListItemDto(
    int Id,
    string Name,
    string Slug,
    int? ParentId,
    string? ParentName,
    int SortOrder,
    bool IsActive,
    int ChildrenCount,
    int ProductsCount);

public record CategoryDetailsDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string? ImageUrl,
    int? ParentId,
    string? ParentName,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<CategoryListItemDto> Children);

public record CategoryLookupDto(int Id, string Name, string Slug, int? ParentId, bool IsActive);

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
