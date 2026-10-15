using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class ReturnItemConfiguration : IEntityTypeConfiguration<ReturnItem>
{
    public void Configure(EntityTypeBuilder<ReturnItem> builder)
    {
        builder.ToTable("ReturnItems", t =>
        {
            t.HasCheckConstraint("CK_ReturnItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_ReturnItems_UnitPriceSnapshot", "[UnitPriceSnapshot] >= 0");
            t.HasCheckConstraint("CK_ReturnItems_LineTotal", "[LineTotal] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SkuSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProductNameSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.UnitPriceSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.Property(x => x.ReasonCode).HasMaxLength(100);
        builder.Property(x => x.Disposition).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(x => new { x.ReturnRequestId, x.OrderItemId }).IsUnique();
        builder.HasIndex(x => x.OrderItemId);
        builder.HasIndex(x => x.VariantId);

        builder.HasOne(x => x.OrderItem)
            .WithMany()
            .HasForeignKey(x => x.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
