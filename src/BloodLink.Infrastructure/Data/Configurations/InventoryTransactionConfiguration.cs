using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions", table =>
        {
            table.HasCheckConstraint("CK_InventoryTransactions_HasChange", "[TotalUnitsChange] <> 0 OR [ReservedUnitsChange] <> 0");
            table.HasCheckConstraint("CK_InventoryTransactions_TotalAfter_NonNegative", "[TotalAfter] >= 0");
            table.HasCheckConstraint("CK_InventoryTransactions_ReservedAfter_NonNegative", "[ReservedAfter] >= 0");
            table.HasCheckConstraint("CK_InventoryTransactions_ReservedAfterWithinTotal", "[ReservedAfter] <= [TotalAfter]");
            table.HasCheckConstraint("CK_InventoryTransactions_TotalBefore_NonNegative", "[TotalBefore] >= 0");
            table.HasCheckConstraint("CK_InventoryTransactions_ReservedBefore_NonNegative", "[ReservedBefore] >= 0");
            table.HasCheckConstraint("CK_InventoryTransactions_ReservedBeforeWithinTotal", "[ReservedBefore] <= [TotalBefore]");
            table.HasCheckConstraint("CK_InventoryTransactions_TotalDeltaMatches", "[TotalAfter] - [TotalBefore] = [TotalUnitsChange]");
            table.HasCheckConstraint("CK_InventoryTransactions_ReservedDeltaMatches", "[ReservedAfter] - [ReservedBefore] = [ReservedUnitsChange]");
        });
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.TransactionType).HasConversion<int>().IsRequired();
        builder.Property(transaction => transaction.Reason).HasMaxLength(500).IsRequired();
        builder.Property(transaction => transaction.ReferenceType).HasMaxLength(100);
        builder.Property(transaction => transaction.PerformedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(transaction => transaction.CreatedAtUtc).IsRequired();
        builder.HasIndex(transaction => new { transaction.BloodInventoryId, transaction.CreatedAtUtc });
        builder.HasIndex(transaction => new { transaction.ReferenceType, transaction.ReferenceId });
        builder.HasOne<BloodInventory>().WithMany().HasForeignKey(transaction => transaction.BloodInventoryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(transaction => transaction.PerformedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
