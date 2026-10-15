using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItems", t =>
        {
            t.HasCheckConstraint("CK_PurchaseOrderItems_OrderedQuantity", "[OrderedQuantity] > 0");
            t.HasCheckConstraint("CK_PurchaseOrderItems_ReceivedQuantity", "[ReceivedQuantity] >= 0");
            t.HasCheckConstraint("CK_PurchaseOrderItems_UnitCost", "[UnitCost] >= 0");
            t.HasCheckConstraint("CK_PurchaseOrderItems_LineTotal", "[LineTotal] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SkuSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProductNameSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);

        builder.Ignore(x => x.RemainingQuantity);

        builder.HasIndex(x => x.PurchaseOrderId);
        builder.HasIndex(x => x.VariantId);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
