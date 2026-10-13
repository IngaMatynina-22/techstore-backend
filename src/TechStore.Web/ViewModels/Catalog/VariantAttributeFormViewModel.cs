using System.ComponentModel.DataAnnotations;
using TechStore.Application.Features.Attributes;
using TechStore.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TechStore.Web.ViewModels.Catalog;

public class VariantAttributeFormViewModel
{
    public int ProductId { get; set; }
    public int VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Attribute")]
    public int AttributeId { get; set; }

    public string? Value { get; set; }

    [Display(Name = "Option")]
    public int? OptionId { get; set; }

    public IReadOnlyList<AttributeDefinitionDto> Definitions { get; set; } = [];
    public IReadOnlyList<VariantAttributeValueDto> CurrentValues { get; set; } = [];
    public SelectList? AttributeOptions { get; set; }
    public SelectList? SelectOptions { get; set; }

    public AttributeDataType? SelectedDataType { get; set; }
}
