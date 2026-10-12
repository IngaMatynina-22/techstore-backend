using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class OrderDocumentConfiguration : IEntityTypeConfiguration<OrderDocument>
{
    public void Configure(EntityTypeBuilder<OrderDocument> builder)
    {
        builder.ToTable("OrderDocuments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.DocumentNumber).HasMaxLength(100);
        builder.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IssuedAt).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.DocumentNumber);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.IssuedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
