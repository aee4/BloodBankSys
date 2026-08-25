using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class BloodNeedConfiguration : IEntityTypeConfiguration<BloodNeed>
{
    public void Configure(EntityTypeBuilder<BloodNeed> builder)
    {
        builder.ToTable("BloodNeeds", table =>
        {
            table.HasCheckConstraint("CK_BloodNeeds_UnitsNeeded", "[UnitsNeeded] > 0");
            table.HasCheckConstraint("CK_BloodNeeds_NeededByUtc", "[NeededByUtc] > GETDATE()");
        });

        builder.HasKey(need => need.Id);

        builder.Property(need => need.RequestedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(need => need.BloodType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(need => need.Urgency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(need => need.Note)
            .HasMaxLength(1000);

        builder.Property(need => need.DecisionReason)
            .HasMaxLength(1000);

        builder.Property(need => need.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(need => need.RowVersion)
            .IsRowVersion();

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(need => need.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(need => need.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
