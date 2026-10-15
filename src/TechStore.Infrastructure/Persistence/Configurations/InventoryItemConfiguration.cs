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
            t.HasCheckConstraint("CK_Inventory_ReservedNotExceedOnHand", "[QuantityReserved] <= [QuantityOnHand]");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.AvailableQuantity);

        builder.HasIndex(x => new { x.WarehouseId, x.VariantId }).IsUnique();
        builder.HasIndex(x => x.QuantityOnHand);

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
