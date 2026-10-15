using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class InventoryCountSessionConfiguration : IEntityTypeConfiguration<InventoryCountSession>
{
    public void Configure(EntityTypeBuilder<InventoryCountSession> builder)
    {
        builder.ToTable("InventoryCountSessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.SnapshotTakenAt).HasColumnType("datetime2");
        builder.Property(x => x.CompletedAt).HasColumnType("datetime2");
        builder.Property(x => x.CancelledAt).HasColumnType("datetime2");
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.SessionNumber).IsUnique();
        builder.HasIndex(x => x.WarehouseId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
