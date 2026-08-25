using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class BloodRequestStatusHistoryConfiguration : IEntityTypeConfiguration<BloodRequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<BloodRequestStatusHistory> builder)
    {
        builder.ToTable("BloodRequestStatusHistory");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.ToStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(history => history.FromStatus)
            .HasConversion<int>();

        builder.Property(history => history.Note)
            .HasMaxLength(1000);

        builder.Property(history => history.ChangedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.HasOne<BloodRequest>()
            .WithMany()
            .HasForeignKey(history => history.BloodRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
