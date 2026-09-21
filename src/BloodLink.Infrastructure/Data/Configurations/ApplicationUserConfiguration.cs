using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.LastName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.CreatedAtUtc).IsRequired();
        builder.HasIndex(user => user.FacilityId);
        builder.HasOne<Facility>().WithMany().HasForeignKey(user => user.FacilityId).OnDelete(DeleteBehavior.NoAction);
    }
}
