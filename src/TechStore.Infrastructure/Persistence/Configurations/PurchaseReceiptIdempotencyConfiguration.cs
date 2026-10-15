using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class PurchaseReceiptIdempotencyConfiguration : IEntityTypeConfiguration<PurchaseReceiptIdempotency>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptIdempotency> builder)
    {
        builder.ToTable("PurchaseReceiptIdempotency");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponseJson).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");

        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<PurchaseOrder>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
