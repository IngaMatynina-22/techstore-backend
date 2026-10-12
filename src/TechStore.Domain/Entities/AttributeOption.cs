using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class AttributeOption : BaseEntity
{
    public int AttributeId { get; set; }
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public AttributeDefinition Attribute { get; set; } = null!;
    public ICollection<VariantAttributeValue> VariantValues { get; set; } = [];
}
