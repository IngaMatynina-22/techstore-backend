using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class InventoryCountLineConfiguration : IEntityTypeConfiguration<InventoryCountLine>
{
    public void Configure(EntityTypeBuilder<InventoryCountLine> builder)
    {
        builder.ToTable("InventoryCountLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExpectedRowVersion)
            .HasColumnType("varbinary(8)")
            .IsRequired();

        builder.Ignore(x => x.Difference);

        builder.HasIndex(x => new { x.SessionId, x.VariantId }).IsUnique();
        builder.HasIndex(x => x.VariantId);
        builder.HasIndex(x => x.InventoryItemId);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InventoryItem)
            .WithMany()
            .HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StockMovement>()
            .WithMany()
            .HasForeignKey(x => x.AppliedMovementId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
