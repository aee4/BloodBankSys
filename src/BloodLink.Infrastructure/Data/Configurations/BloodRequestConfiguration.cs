using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class BloodRequestConfiguration : IEntityTypeConfiguration<BloodRequest>
{
    public void Configure(EntityTypeBuilder<BloodRequest> builder)
    {
        builder.ToTable("BloodRequests", table =>
        {
            table.HasCheckConstraint("CK_BloodRequests_UnitsRequested", "[UnitsRequested] > 0");
            table.HasCheckConstraint("CK_BloodRequests_UnitsAccepted", "[UnitsAccepted] IS NULL OR [UnitsAccepted] >= 0");
        });

        builder.HasKey(request => request.Id);

        builder.Property(request => request.RequestedByAdminId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(request => request.RespondedByAdminId)
            .HasMaxLength(450);

        builder.Property(request => request.FulfilledByAdminId)
            .HasMaxLength(450);

        builder.Property(request => request.BloodType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(request => request.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(request => request.RequestNote)
            .HasMaxLength(1000);

        builder.Property(request => request.ResponseNote)
            .HasMaxLength(1000);

        builder.Property(request => request.RowVersion)
            .IsRowVersion();

        builder.HasOne<BloodNeed>()
            .WithMany()
            .HasForeignKey(request => request.BloodNeedId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(request => request.RequestingFacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(request => request.SourceFacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(request => request.RequestedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(request => request.RespondedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(request => request.FulfilledByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
