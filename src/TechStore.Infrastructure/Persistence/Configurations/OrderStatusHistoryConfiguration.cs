using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistory");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.ChangedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.ChangedAt);

        builder.HasOne(x => x.OldStatus)
            .WithMany()
            .HasForeignKey(x => x.OldStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.NewStatus)
            .WithMany()
            .HasForeignKey(x => x.NewStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
