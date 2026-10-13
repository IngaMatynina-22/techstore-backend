using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class OrderOperationIdempotencyConfiguration : IEntityTypeConfiguration<OrderOperationIdempotency>
{
    public void Configure(EntityTypeBuilder<OrderOperationIdempotency> builder)
    {
        builder.ToTable("OrderOperationIdempotency");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OperationType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponseJson).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");

        // Scope: unique per user (null user treated as a single anonymous scope).
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}