using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class PurchaseOrderStatusHistoryConfiguration : IEntityTypeConfiguration<PurchaseOrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderStatusHistory> builder)
    {
        builder.ToTable("PurchaseOrderStatusHistories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.ChangedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.PurchaseOrderId);
        builder.HasIndex(x => x.ChangedAt);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
