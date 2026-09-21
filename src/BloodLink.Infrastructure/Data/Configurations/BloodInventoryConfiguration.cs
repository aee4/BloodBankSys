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
            table.HasCheckConstraint("CK_BloodInventory_TotalUnits_NonNegative", "[TotalUnits] >= 0");
            table.HasCheckConstraint("CK_BloodInventory_ReservedUnits_NonNegative", "[ReservedUnits] >= 0");
            table.HasCheckConstraint("CK_BloodInventory_ReservedWithinTotal", "[ReservedUnits] <= [TotalUnits]");
            table.HasCheckConstraint("CK_BloodInventory_LowStockThreshold_NonNegative", "[LowStockThreshold] >= 0");
        });
        builder.HasKey(inventory => inventory.Id);
        builder.Property(inventory => inventory.BloodType).HasConversion<int>().IsRequired();
        builder.Property(inventory => inventory.UpdatedAtUtc).IsRequired();
        builder.Property(inventory => inventory.RowVersion).IsRowVersion();
        builder.HasIndex(inventory => new { inventory.FacilityId, inventory.BloodType }).IsUnique();
        builder.HasIndex(inventory => new { inventory.BloodType, inventory.TotalUnits, inventory.ReservedUnits });
        builder.HasOne<Facility>().WithMany().HasForeignKey(inventory => inventory.FacilityId).OnDelete(DeleteBehavior.NoAction);
    }
}
