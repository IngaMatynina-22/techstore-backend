using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Configurations;

public class ReturnStatusHistoryConfiguration : IEntityTypeConfiguration<ReturnStatusHistory>
{
    public void Configure(EntityTypeBuilder<ReturnStatusHistory> builder)
    {
        builder.ToTable("ReturnStatusHistories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.ChangedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.ReturnRequestId);
        builder.HasIndex(x => x.ChangedAt);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
