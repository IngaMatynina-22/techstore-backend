using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.TransactionReference).HasMaxLength(200);
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.PaidAt).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.TransactionReference);
        builder.HasIndex(x => x.PaymentStatus);
    }
}
