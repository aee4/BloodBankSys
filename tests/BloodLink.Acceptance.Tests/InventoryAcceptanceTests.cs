using BloodLink.Acceptance.Tests.Support;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Domain.Exceptions;
using BloodLink.Infrastructure.Services.Inventory;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Acceptance.Tests;

public sealed class InventoryAcceptanceTests
{
    [Fact]
    public async Task AdjustingStock_NeverAllowsTotalUnitsBelowReservedUnits()
    {
        using var dbContext = WorkflowTestSupport.CreateDbContext();
        var currentUser = AdminUser("admin-a", WorkflowTestSupport.FacilityAId);
        dbContext.BloodInventory.Add(new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = WorkflowTestSupport.FacilityAId,
            BloodType = BloodType.OPositive,
            TotalUnits = 10,
            ReservedUnits = 6,
            LowStockThreshold = 4,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var service = new InventoryService(dbContext, currentUser);

        await Assert.ThrowsAsync<InsufficientInventoryException>(() =>
            service.AdjustInventoryAsync(new InventoryAdjustmentRequest(BloodType.OPositive, -5, "Attempt invalid consumption")));

        var stored = await dbContext.BloodInventory.SingleAsync(item => item.FacilityId == WorkflowTestSupport.FacilityAId);
        Assert.Equal(10, stored.TotalUnits);
        Assert.Equal(6, stored.ReservedUnits);
        Assert.Equal(0, await dbContext.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task ManualAdjustments_CreateImmutableInventoryTransactions()
    {
        using var dbContext = WorkflowTestSupport.CreateDbContext();
        var currentUser = AdminUser("admin-a", WorkflowTestSupport.FacilityAId);
        var service = new InventoryService(dbContext, currentUser);

        await service.AdjustInventoryAsync(new InventoryAdjustmentRequest(BloodType.APositive, 12, "Initial stock count"));
        await service.AdjustInventoryAsync(new InventoryAdjustmentRequest(BloodType.APositive, -3, "Emergency issue"));

        var inventory = await dbContext.BloodInventory.SingleAsync(item =>
            item.FacilityId == WorkflowTestSupport.FacilityAId && item.BloodType == BloodType.APositive);
        var transactions = await dbContext.InventoryTransactions
            .Where(transaction => transaction.BloodInventoryId == inventory.Id)
            .OrderBy(transaction => transaction.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(9, inventory.TotalUnits);
        Assert.Equal(2, transactions.Count);
        Assert.Equal(InventoryTransactionType.StockIn, transactions[0].TransactionType);
        Assert.Equal("Initial stock count", transactions[0].Reason);
        Assert.Equal(12, transactions[0].TotalAfter);
        Assert.Equal(InventoryTransactionType.Consumption, transactions[1].TransactionType);
        Assert.Equal("Emergency issue", transactions[1].Reason);
        Assert.Equal(9, transactions[1].TotalAfter);
        Assert.All(transactions, transaction => Assert.Equal(currentUser.UserId, transaction.PerformedByUserId));
    }

    [Fact]
    public async Task ExactTypeSearch_ReturnsApprovedExternalFacilitiesOnly()
    {
        using var dbContext = WorkflowTestSupport.CreateDbContext();
        var currentUser = AdminUser("admin-a", WorkflowTestSupport.FacilityAId);
        dbContext.Facilities.Single(facility => facility.Id == WorkflowTestSupport.FacilityAId).City = "Accra";
        dbContext.Facilities.Single(facility => facility.Id == WorkflowTestSupport.FacilityBId).City = "Kumasi";
        dbContext.Facilities.Single(facility => facility.Id == WorkflowTestSupport.FacilityBId).Region = "Ashanti";
        dbContext.Facilities.Single(facility => facility.Id == WorkflowTestSupport.FacilityCId).City = "Tamale";
        dbContext.BloodInventory.AddRange(
            new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = WorkflowTestSupport.FacilityAId,
                BloodType = BloodType.OPositive,
                TotalUnits = 50,
                ReservedUnits = 0,
                LowStockThreshold = 10,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = WorkflowTestSupport.FacilityBId,
                BloodType = BloodType.OPositive,
                TotalUnits = 40,
                ReservedUnits = 8,
                LowStockThreshold = 10,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = WorkflowTestSupport.FacilityCId,
                BloodType = BloodType.OPositive,
                TotalUnits = 80,
                ReservedUnits = 0,
                LowStockThreshold = 10,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = WorkflowTestSupport.FacilityBId,
                BloodType = BloodType.ONegative,
                TotalUnits = 99,
                ReservedUnits = 0,
                LowStockThreshold = 10,
                UpdatedAtUtc = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();
        var service = new InventoryService(dbContext, currentUser);

        var results = await service.SearchAvailabilityAsync(new AvailabilitySearchRequest(BloodType.OPositive, 20));

        var result = Assert.Single(results);
        Assert.Equal(WorkflowTestSupport.FacilityBId, result.FacilityId);
        Assert.Equal("Facility B", result.FacilityName);
        Assert.Equal(BloodType.OPositive, result.BloodType);
        Assert.Equal(32, result.AvailableUnits);
        Assert.Equal("Ashanti", result.Region);
        Assert.Equal("Kumasi", result.City);
    }

    private static FakeCurrentUserService AdminUser(string userId, Guid facilityId)
    {
        return new FakeCurrentUserService
        {
            UserId = userId,
            FacilityId = facilityId,
            RoleList = { RoleNames.FacilityAdmin }
        };
    }
}
