using System.ComponentModel.DataAnnotations;
using TechStore.Application.Features.Products;

namespace TechStore.Web.ViewModels.Catalog;

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(300)]
    public string Name { get; set; } = string.Empty;

    [StringLength(320)]
    public string? Slug { get; set; }

    [StringLength(8000)]
    public string? Description { get; set; }

    [StringLength(1000)]
    [Display(Name = "Short description")]
    public string? ShortDescription { get; set; }

    [StringLength(500)]
    [Display(Name = "Image URL")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Warranty (months)")]
    [Range(0, 1200)]
    public int? WarrantyMonths { get; set; }

    [Required]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Brand")]
    public int? BrandId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Display(Name = "Variant name")]
    [StringLength(200)]
    public string VariantName { get; set; } = "Default";

    [Required]
    [StringLength(100)]
    public string Sku { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Barcode { get; set; }

    [Display(Name = "Purchase price")]
    [Range(0, double.MaxValue)]
    public decimal PurchasePrice { get; set; }

    [Display(Name = "Selling price")]
    [Range(0, double.MaxValue)]
    public decimal SellingPrice { get; set; }

    [Display(Name = "Old price")]
    [Range(0, double.MaxValue)]
    public decimal? OldPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Weight { get; set; }

    public static ProductFormViewModel FromDetails(ProductDetailsDto dto)
    {
        var variant = dto.Variants.FirstOrDefault();
        return new ProductFormViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            Slug = dto.Slug,
            Description = dto.Description,
            ShortDescription = dto.ShortDescription,
            ImageUrl = dto.ImageUrl,
            WarrantyMonths = dto.WarrantyMonths,
            CategoryId = dto.CategoryId,
            BrandId = dto.BrandId,
            IsActive = dto.IsActive,
            VariantName = variant?.VariantName ?? "Default",
            Sku = variant?.Sku ?? string.Empty,
            Barcode = variant?.Barcode,
            PurchasePrice = variant?.PurchasePrice ?? 0,
            SellingPrice = variant?.SellingPrice ?? 0,
            OldPrice = variant?.OldPrice,
            Weight = variant?.Weight
        };
    }

    public CreateProductRequest ToCreateRequest() => new()
    {
        Name = Name,
        Slug = this.Slug,
        Description = Description,
        ShortDescription = ShortDescription,
        ImageUrl = ImageUrl,
        WarrantyMonths = WarrantyMonths,
        CategoryId = CategoryId,
        BrandId = BrandId,
        IsActive = IsActive,
        Variants =
        [
            new ProductVariantInput
            {
                VariantName = VariantName,
                Sku = Sku,
                Barcode = Barcode,
                PurchasePrice = PurchasePrice,
                SellingPrice = SellingPrice,
                OldPrice = OldPrice,
                Weight = Weight,
                IsActive = true
            }
        ]
    };

    public UpdateProductRequest ToUpdateRequest() => new()
    {
        Name = Name,
        Slug = this.Slug,
        Description = Description,
        ShortDescription = ShortDescription,
        ImageUrl = ImageUrl,
        WarrantyMonths = WarrantyMonths,
        CategoryId = CategoryId,
        BrandId = BrandId,
        IsActive = IsActive
    };
}
