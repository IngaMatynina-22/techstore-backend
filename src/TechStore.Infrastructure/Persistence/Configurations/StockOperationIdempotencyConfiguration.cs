using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class StockOperationIdempotencyConfiguration : IEntityTypeConfiguration<StockOperationIdempotency>
{
    public void Configure(EntityTypeBuilder<StockOperationIdempotency> builder)
    {
        builder.ToTable("StockOperationIdempotency");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OperationType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponseJson).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
