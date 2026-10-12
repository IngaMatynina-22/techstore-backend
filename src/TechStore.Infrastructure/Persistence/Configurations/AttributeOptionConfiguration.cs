using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class AttributeOptionConfiguration : IEntityTypeConfiguration<AttributeOption>
{
    public void Configure(EntityTypeBuilder<AttributeOption> builder)
    {
        builder.ToTable("AttributeOptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Value).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.AttributeId, x.Value }).IsUnique();
    }
}
