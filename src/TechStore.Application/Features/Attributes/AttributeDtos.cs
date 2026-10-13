using TechStore.Domain.Enums;

namespace TechStore.Application.Features.Attributes;

public record AttributeOptionDto(int Id, string Value, int SortOrder);

public record AttributeDefinitionDto(
    int Id,
    int CategoryId,
    string CategoryName,
    string Name,
    string Code,
    AttributeDataType DataType,
    string? Unit,
    bool IsRequired,
    bool IsFilterable,
    int SortOrder,
    IReadOnlyList<AttributeOptionDto> Options);

public class CreateAttributeDefinitionRequest
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public AttributeDataType DataType { get; set; }
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
    public bool IsFilterable { get; set; }
    public int SortOrder { get; set; }
    public List<string> Options { get; set; } = [];
}

public class UpdateAttributeDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public AttributeDataType DataType { get; set; }
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
    public bool IsFilterable { get; set; }
    public int SortOrder { get; set; }
    public List<string> Options { get; set; } = [];
}

public class SetVariantAttributeValueRequest
{
    public int AttributeId { get; set; }
    public string? Value { get; set; }
    public int? OptionId { get; set; }
}

public record VariantAttributeValueDto(
    int Id,
    int VariantId,
    int AttributeId,
    string AttributeName,
    AttributeDataType DataType,
    string? Unit,
    string? Value,
    int? OptionId,
    string? OptionValue);
