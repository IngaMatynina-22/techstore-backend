using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_OrderItems_UnitPrice", "[UnitPrice] >= 0");
            t.HasCheckConstraint("CK_OrderItems_DiscountAmount", "[DiscountAmount] >= 0");
            t.HasCheckConstraint("CK_OrderItems_Subtotal", "[Subtotal] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.VariantName).HasMaxLength(200);
        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.VariantId);
        builder.HasIndex(x => x.Sku);

        builder.HasOne(x => x.Variant)
            .WithMany(x => x.OrderItems)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
