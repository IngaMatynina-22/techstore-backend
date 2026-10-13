using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.ReferenceType).HasMaxLength(100);
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.WarehouseId);
        builder.HasIndex(x => x.VariantId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.TransferGroupId);
        builder.HasIndex(x => x.MovementType);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });

        builder.HasOne(x => x.Warehouse)
            .WithMany(x => x.StockMovements)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Variant)
            .WithMany(x => x.StockMovements)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
