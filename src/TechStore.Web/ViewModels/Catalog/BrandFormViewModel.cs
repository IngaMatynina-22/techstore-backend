using System.ComponentModel.DataAnnotations;
using TechStore.Application.Features.Brands;

namespace TechStore.Web.ViewModels.Catalog;

public class BrandFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(500)]
    [Display(Name = "Logo URL")]
    public string? LogoUrl { get; set; }

    [StringLength(500)]
    [Display(Name = "Website URL")]
    public string? WebsiteUrl { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public static BrandFormViewModel FromDetails(BrandDetailsDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        LogoUrl = dto.LogoUrl,
        WebsiteUrl = dto.WebsiteUrl,
        IsActive = dto.IsActive
    };

    public CreateBrandRequest ToCreateRequest() => new()
    {
        Name = Name,
        Description = Description,
        LogoUrl = LogoUrl,
        WebsiteUrl = WebsiteUrl,
        IsActive = IsActive
    };

    public UpdateBrandRequest ToUpdateRequest() => new()
    {
        Name = Name,
        Description = Description,
        LogoUrl = LogoUrl,
        WebsiteUrl = WebsiteUrl,
        IsActive = IsActive
    };
}
