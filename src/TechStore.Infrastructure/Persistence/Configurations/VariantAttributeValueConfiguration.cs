using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class VariantAttributeValueConfiguration : IEntityTypeConfiguration<VariantAttributeValue>
{
    public void Configure(EntityTypeBuilder<VariantAttributeValue> builder)
    {
        builder.ToTable("VariantAttributeValues");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Value).HasMaxLength(500);

        builder.HasIndex(x => new { x.VariantId, x.AttributeId }).IsUnique();

        builder.HasOne(x => x.Variant)
            .WithMany(x => x.AttributeValues)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Attribute)
            .WithMany(x => x.VariantValues)
            .HasForeignKey(x => x.AttributeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Option)
            .WithMany(x => x.VariantValues)
            .HasForeignKey(x => x.OptionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
