using System.ComponentModel.DataAnnotations;
using TechStore.Application.Features.Categories;

namespace TechStore.Web.ViewModels.Catalog;

public class CategoryFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(220)]
    public string? Slug { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(500)]
    [Display(Name = "Image URL")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Parent category")]
    public int? ParentId { get; set; }

    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public static CategoryFormViewModel FromDetails(CategoryDetailsDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Slug = dto.Slug,
        Description = dto.Description,
        ImageUrl = dto.ImageUrl,
        ParentId = dto.ParentId,
        SortOrder = dto.SortOrder,
        IsActive = dto.IsActive
    };

    public CreateCategoryRequest ToCreateRequest() => new()
    {
        Name = Name,
        Slug = this.Slug,
        Description = Description,
        ImageUrl = ImageUrl,
        ParentId = ParentId,
        SortOrder = SortOrder,
        IsActive = IsActive
    };

    public UpdateCategoryRequest ToUpdateRequest() => new()
    {
        Name = Name,
        Slug = this.Slug,
        Description = Description,
        ImageUrl = ImageUrl,
        ParentId = ParentId,
        SortOrder = SortOrder,
        IsActive = IsActive
    };
}
