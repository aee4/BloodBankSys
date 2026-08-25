using BloodLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class BloodInventoryConfiguration : IEntityTypeConfiguration<BloodInventory>
{
    public void Configure(EntityTypeBuilder<BloodInventory> builder)
    {
        builder.ToTable("BloodInventory", table =>
        {
            table.HasCheckConstraint("CK_BloodInventory_TotalUnits", "[TotalUnits] >= 0");
            table.HasCheckConstraint("CK_BloodInventory_ReservedUnits", "[ReservedUnits] >= 0");
            table.HasCheckConstraint("CK_BloodInventory_LowStockThreshold", "[LowStockThreshold] >= 0");
        });

        builder.HasKey(inventory => inventory.Id);

        builder.Property(inventory => inventory.BloodType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(inventory => inventory.TotalUnits)
            .IsRequired();

        builder.Property(inventory => inventory.ReservedUnits)
            .IsRequired();

        builder.Property(inventory => inventory.LowStockThreshold)
            .IsRequired();

        builder.Property(inventory => inventory.UpdatedAtUtc)
            .IsRequired();

        builder.Property(inventory => inventory.RowVersion)
            .IsRowVersion();

        builder.HasIndex(inventory => new { inventory.FacilityId, inventory.BloodType })
            .IsUnique();

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(inventory => inventory.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
