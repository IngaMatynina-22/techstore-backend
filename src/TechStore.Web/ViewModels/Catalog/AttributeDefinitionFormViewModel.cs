using System.ComponentModel.DataAnnotations;
using TechStore.Application.Features.Attributes;
using TechStore.Domain.Enums;

namespace TechStore.Web.ViewModels.Catalog;

public class AttributeDefinitionFormViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Code { get; set; }

    [Required]
    [Display(Name = "Data type")]
    public AttributeDataType DataType { get; set; } = AttributeDataType.Text;

    [StringLength(50)]
    public string? Unit { get; set; }

    [Display(Name = "Required")]
    public bool IsRequired { get; set; }

    [Display(Name = "Filterable")]
    public bool IsFilterable { get; set; }

    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }

    [Display(Name = "Options (one per line, for Select type)")]
    public string? OptionsText { get; set; }

    public static AttributeDefinitionFormViewModel FromDto(AttributeDefinitionDto dto) => new()
    {
        Id = dto.Id,
        CategoryId = dto.CategoryId,
        CategoryName = dto.CategoryName,
        Name = dto.Name,
        Code = dto.Code,
        DataType = dto.DataType,
        Unit = dto.Unit,
        IsRequired = dto.IsRequired,
        IsFilterable = dto.IsFilterable,
        SortOrder = dto.SortOrder,
        OptionsText = string.Join(Environment.NewLine, dto.Options.Select(o => o.Value))
    };

    public CreateAttributeDefinitionRequest ToCreateRequest() => new()
    {
        CategoryId = CategoryId,
        Name = Name,
        Code = Code,
        DataType = DataType,
        Unit = Unit,
        IsRequired = IsRequired,
        IsFilterable = IsFilterable,
        SortOrder = SortOrder,
        Options = ParseOptions()
    };

    public UpdateAttributeDefinitionRequest ToUpdateRequest() => new()
    {
        Name = Name,
        Code = Code,
        DataType = DataType,
        Unit = Unit,
        IsRequired = IsRequired,
        IsFilterable = IsFilterable,
        SortOrder = SortOrder,
        Options = ParseOptions()
    };

    private List<string> ParseOptions() =>
        (OptionsText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
