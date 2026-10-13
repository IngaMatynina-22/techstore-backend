using System.ComponentModel.DataAnnotations;
using TechStore.Application.Features.Products;

namespace TechStore.Web.ViewModels.Catalog;

public class ProductVariantFormViewModel
{
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Variant name")]
    [StringLength(200)]
    public string VariantName { get; set; } = string.Empty;

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

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public static ProductVariantFormViewModel FromDto(int productId, string productName, ProductVariantDto dto) => new()
    {
        ProductId = productId,
        VariantId = dto.Id,
        ProductName = productName,
        VariantName = dto.VariantName,
        Sku = dto.Sku,
        Barcode = dto.Barcode,
        PurchasePrice = dto.PurchasePrice,
        SellingPrice = dto.SellingPrice,
        OldPrice = dto.OldPrice,
        Weight = dto.Weight,
        IsActive = dto.IsActive
    };

    public ProductVariantInput ToInput() => new()
    {
        VariantName = VariantName,
        Sku = Sku,
        Barcode = Barcode,
        PurchasePrice = PurchasePrice,
        SellingPrice = SellingPrice,
        OldPrice = OldPrice,
        Weight = Weight,
        IsActive = IsActive
    };

    public UpdateProductVariantRequest ToUpdateRequest() => new()
    {
        VariantName = VariantName,
        Sku = Sku,
        Barcode = Barcode,
        PurchasePrice = PurchasePrice,
        SellingPrice = SellingPrice,
        OldPrice = OldPrice,
        Weight = Weight,
        IsActive = IsActive
    };
}
