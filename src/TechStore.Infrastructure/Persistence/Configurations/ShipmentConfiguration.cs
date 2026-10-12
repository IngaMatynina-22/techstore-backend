using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeliveryService).HasMaxLength(100);
        builder.Property(x => x.TrackingNumber).HasMaxLength(100);
        builder.Property(x => x.ShipmentStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RecipientPhone).HasMaxLength(50);
        builder.Property(x => x.DeliveryAddress).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.ShippedAt).HasColumnType("datetime2");
        builder.Property(x => x.DeliveredAt).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.TrackingNumber);
        builder.HasIndex(x => x.ShipmentStatus);

        builder.HasOne(x => x.Address)
            .WithMany(x => x.Shipments)
            .HasForeignKey(x => x.AddressId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
