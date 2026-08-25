using BloodLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class FacilityStaffConfiguration : IEntityTypeConfiguration<FacilityStaff>
{
    public void Configure(EntityTypeBuilder<FacilityStaff> builder)
    {
        builder.ToTable("FacilityStaff");

        builder.HasKey(staff => staff.Id);

        builder.Property(staff => staff.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(staff => staff.CreatedByAdminId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(staff => staff.StatusReason)
            .HasMaxLength(500);

        builder.Property(staff => staff.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(staff => new { staff.FacilityId, staff.UserId })
            .IsUnique();

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(staff => staff.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
