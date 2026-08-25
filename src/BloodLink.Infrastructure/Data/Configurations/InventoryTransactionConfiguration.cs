using BloodLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions", table =>
        {
            table.HasCheckConstraint("CK_InventoryTransactions_TotalUnitsChange", "[TotalUnitsChange] <> 0 OR [ReservedUnitsChange] <> 0");
            table.HasCheckConstraint("CK_InventoryTransactions_TotalAfter", "[TotalAfter] >= 0");
            table.HasCheckConstraint("CK_InventoryTransactions_ReservedAfter", "[ReservedAfter] >= 0");
        });

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.TransactionType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(transaction => transaction.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(transaction => transaction.ReferenceType)
            .HasMaxLength(100);

        builder.Property(transaction => transaction.PerformedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(transaction => transaction.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<BloodInventory>()
            .WithMany()
            .HasForeignKey(transaction => transaction.BloodInventoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
