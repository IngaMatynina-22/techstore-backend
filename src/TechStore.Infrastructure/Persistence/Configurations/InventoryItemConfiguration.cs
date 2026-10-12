using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("Inventory", t =>
        {
            t.HasCheckConstraint("CK_Inventory_QuantityOnHand", "[QuantityOnHand] >= 0");
            t.HasCheckConstraint("CK_Inventory_QuantityReserved", "[QuantityReserved] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(x => new { x.WarehouseId, x.VariantId }).IsUnique();

        builder.HasOne(x => x.Warehouse)
            .WithMany(x => x.InventoryItems)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Variant)
            .WithMany(x => x.InventoryItems)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
