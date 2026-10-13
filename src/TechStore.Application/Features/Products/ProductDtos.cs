namespace TechStore.Application.Features.Products;

public enum ProductSortBy
{
    NameAsc = 0,
    NameDesc = 1,
    PriceAsc = 2,
    PriceDesc = 3,
    CreatedAtDesc = 4,
    CreatedAtAsc = 5
}

public class ProductQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? BrandId { get; set; }
    public bool? IsActive { get; set; }
    public ProductSortBy SortBy { get; set; } = ProductSortBy.CreatedAtDesc;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record ProductListItemDto(
    int Id,
    string Name,
    string Slug,
    string CategoryName,
    string? BrandName,
    string? PrimarySku,
    decimal? MinSellingPrice,
    decimal? MaxSellingPrice,
    bool IsActive,
    DateTime CreatedAt,
    int VariantsCount);

public record ProductVariantDto(
    int Id,
    string Sku,
    string? Barcode,
    string VariantName,
    decimal PurchasePrice,
    decimal SellingPrice,
    decimal? OldPrice,
    decimal? Weight,
    bool IsActive);

public record ProductDetailsDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string? ShortDescription,
    string? ImageUrl,
    int? WarrantyMonths,
    int CategoryId,
    string CategoryName,
    int? BrandId,
    string? BrandName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ProductVariantDto> Variants);

public class ProductVariantInput
{
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? Weight { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? ImageUrl { get; set; }
    public int? WarrantyMonths { get; set; }
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ProductVariantInput> Variants { get; set; } = [];
}

public class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? ImageUrl { get; set; }
    public int? WarrantyMonths { get; set; }
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateProductVariantRequest
{
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? Weight { get; set; }
    public bool IsActive { get; set; } = true;
}
