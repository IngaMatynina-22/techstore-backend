using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants", t =>
        {
            t.HasCheckConstraint("CK_ProductVariants_PurchasePrice", "[PurchasePrice] >= 0");
            t.HasCheckConstraint("CK_ProductVariants_SellingPrice", "[SellingPrice] >= 0");
            t.HasCheckConstraint("CK_ProductVariants_OldPrice", "[OldPrice] IS NULL OR [OldPrice] >= 0");
            t.HasCheckConstraint("CK_ProductVariants_Weight", "[Weight] IS NULL OR [Weight] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(100);
        builder.Property(x => x.VariantName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PurchasePrice).HasPrecision(18, 2);
        builder.Property(x => x.SellingPrice).HasPrecision(18, 2);
        builder.Property(x => x.OldPrice).HasPrecision(18, 2);
        builder.Property(x => x.Weight).HasPrecision(18, 3);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.Sku).IsUnique();
        builder.HasIndex(x => x.Barcode)
            .IsUnique()
            .HasFilter("[Barcode] IS NOT NULL");
        builder.HasIndex(x => x.ProductId);
    }
}
