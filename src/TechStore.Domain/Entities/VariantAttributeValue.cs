using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class VariantAttributeValue : BaseEntity
{
    public int VariantId { get; set; }
    public int AttributeId { get; set; }
    public int? OptionId { get; set; }
    public string? Value { get; set; }

    public ProductVariant Variant { get; set; } = null!;
    public AttributeDefinition Attribute { get; set; } = null!;
    public AttributeOption? Option { get; set; }
}
