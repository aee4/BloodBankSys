using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class FacilityStaffConfiguration : IEntityTypeConfiguration<FacilityStaff>
{
    public void Configure(EntityTypeBuilder<FacilityStaff> builder)
    {
        builder.ToTable("FacilityStaff");
        builder.HasKey(staff => staff.Id);
        builder.Property(staff => staff.UserId).HasMaxLength(450).IsRequired();
        builder.Property(staff => staff.CreatedByAdminId).HasMaxLength(450).IsRequired();
        builder.Property(staff => staff.StatusReason).HasMaxLength(500);
        builder.Property(staff => staff.Status).HasConversion<int>().IsRequired();
        builder.HasIndex(staff => staff.UserId).IsUnique();
        builder.HasIndex(staff => new { staff.FacilityId, staff.Status });
        builder.HasOne<Facility>().WithMany().HasForeignKey(staff => staff.FacilityId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(staff => staff.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(staff => staff.CreatedByAdminId).OnDelete(DeleteBehavior.NoAction);
    }
}
