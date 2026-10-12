using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class SupplierProductConfiguration : IEntityTypeConfiguration<SupplierProduct>
{
    public void Configure(EntityTypeBuilder<SupplierProduct> builder)
    {
        builder.ToTable("SupplierProducts", t =>
        {
            t.HasCheckConstraint("CK_SupplierProducts_SupplyPrice", "[SupplyPrice] >= 0");
            t.HasCheckConstraint("CK_SupplierProducts_DeliveryDays", "[DeliveryDays] IS NULL OR [DeliveryDays] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SupplierSku).HasMaxLength(100);
        builder.Property(x => x.SupplyPrice).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.SupplierId, x.VariantId }).IsUnique();

        builder.HasOne(x => x.Supplier)
            .WithMany(x => x.SupplierProducts)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Variant)
            .WithMany(x => x.SupplierProducts)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
