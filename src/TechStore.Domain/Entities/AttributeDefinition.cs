using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class AttributeDefinition : AuditableEntity
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public AttributeDataType DataType { get; set; }
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
    public bool IsFilterable { get; set; }
    public int SortOrder { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<AttributeOption> Options { get; set; } = [];
    public ICollection<VariantAttributeValue> VariantValues { get; set; } = [];
}
