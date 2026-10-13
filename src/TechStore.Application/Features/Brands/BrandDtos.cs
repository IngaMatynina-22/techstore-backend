namespace TechStore.Application.Features.Brands;

public record BrandListItemDto(
    int Id,
    string Name,
    string? WebsiteUrl,
    bool IsActive,
    int ProductsCount);

public record BrandDetailsDto(
    int Id,
    string Name,
    string? Description,
    string? LogoUrl,
    string? WebsiteUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int ProductsCount);

public record BrandLookupDto(int Id, string Name, bool IsActive);

public class CreateBrandRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateBrandRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
