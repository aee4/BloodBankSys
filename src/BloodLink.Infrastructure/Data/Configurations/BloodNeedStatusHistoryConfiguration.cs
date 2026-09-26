using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class BloodNeedStatusHistoryConfiguration : IEntityTypeConfiguration<BloodNeedStatusHistory>
{
    public void Configure(EntityTypeBuilder<BloodNeedStatusHistory> builder)
    {
        builder.ToTable("BloodNeedStatusHistory");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.FromStatus).HasConversion<int>();
        builder.Property(history => history.ToStatus).HasConversion<int>().IsRequired();
        builder.Property(history => history.Note).HasMaxLength(1000);
        builder.Property(history => history.ChangedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(history => history.ChangedAtUtc).IsRequired();
        builder.HasIndex(history => new { history.BloodNeedId, history.ChangedAtUtc, history.Id });
        builder.HasOne<BloodNeed>().WithMany().HasForeignKey(history => history.BloodNeedId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(history => history.ChangedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
