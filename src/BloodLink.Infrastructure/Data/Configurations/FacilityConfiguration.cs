using BloodLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("Facilities", table =>
        {
            table.HasCheckConstraint("CK_Facilities_Name_NotEmpty", "[Name] <> N''");
            table.HasCheckConstraint("CK_Facilities_RegistrationNumber_NotEmpty", "[RegistrationNumber] <> N''");
        });

        builder.HasKey(facility => facility.Id);

        builder.Property(facility => facility.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(facility => facility.RegistrationNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(facility => facility.Region)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(facility => facility.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(facility => facility.Address)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(facility => facility.ContactEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(facility => facility.ContactPhone)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(facility => facility.RejectionReason)
            .HasMaxLength(500);

        builder.Property(facility => facility.CreatedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(facility => facility.ApprovedByUserId)
            .HasMaxLength(450);

        builder.Property(facility => facility.FacilityType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(facility => facility.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(facility => new { facility.Name, facility.RegistrationNumber })
            .IsUnique();
    }
}
