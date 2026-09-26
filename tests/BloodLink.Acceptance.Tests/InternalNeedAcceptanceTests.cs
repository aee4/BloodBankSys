using BloodLink.Acceptance.Tests.Support;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Needs;

namespace BloodLink.Acceptance.Tests;

public sealed class InternalNeedAcceptanceTests
{
    [Fact]
    public async Task Create_ByStaff_StartsAsPendingReview()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);

        var needService = new BloodNeedService(dbContext, staffUser);
        var need = await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.APositive, 2, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(1), "Routine top up"));

        Assert.Equal(BloodNeedStatus.PendingReview, need.Status);
    }

    [Fact]
    public async Task StartSearch_ByAdmin_MovesFromPendingReviewToSearching()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);

        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);
        var adminUser = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        adminUser.RoleList.Add(RoleNames.FacilityAdmin);

        var needServiceAsStaff = new BloodNeedService(dbContext, staffUser);
        var need = await needServiceAsStaff.CreateAsync(new CreateBloodNeedRequest(
            BloodType.ONegative, 2, UrgencyLevel.Urgent, DateTime.UtcNow.AddHours(6), null));

        var needServiceAsAdmin = new BloodNeedService(dbContext, adminUser);
        await needServiceAsAdmin.StartSearchAsync(new NeedDecisionRequest(need.Id, null));

        var updated = (await needServiceAsAdmin.ListOwnFacilityAsync()).Single(item => item.Id == need.Id);
        Assert.Equal(BloodNeedStatus.Searching, updated.Status);
    }

    [Fact]
    public async Task FulfilInternally_FromPendingReview_MarksFulfilledInternally()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);

        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);
        var adminUser = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        adminUser.RoleList.Add(RoleNames.FacilityAdmin);

        var needServiceAsStaff = new BloodNeedService(dbContext, staffUser);
        var need = await needServiceAsStaff.CreateAsync(new CreateBloodNeedRequest(
            BloodType.BPositive, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(2), null));

        dbContext.BloodInventory.Add(new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = WorkflowTestSupport.FacilityAId,
            BloodType = BloodType.BPositive,
            TotalUnits = 5,
            ReservedUnits = 2,
            LowStockThreshold = 1,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var needServiceAsAdmin = new BloodNeedService(dbContext, adminUser);
        await needServiceAsAdmin.FulfilInternallyAsync(new NeedDecisionRequest(need.Id, "Covered from own stock"));

        var updated = (await needServiceAsAdmin.ListOwnFacilityAsync()).Single(item => item.Id == need.Id);
        Assert.Equal(BloodNeedStatus.FulfilledInternally, updated.Status);
        var inventory = Assert.Single(dbContext.BloodInventory);
        Assert.Equal(4, inventory.TotalUnits);
        Assert.Equal(2, inventory.ReservedUnits);
        var transaction = Assert.Single(dbContext.InventoryTransactions);
        Assert.Equal(-1, transaction.TotalUnitsChange);
        Assert.Equal(5, transaction.TotalBefore);
        Assert.Equal(4, transaction.TotalAfter);
    }

    [Fact]
    public async Task Reject_WithoutAReason_ThrowsArgumentException()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);

        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);
        var adminUser = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        adminUser.RoleList.Add(RoleNames.FacilityAdmin);

        var needServiceAsStaff = new BloodNeedService(dbContext, staffUser);
        var need = await needServiceAsStaff.CreateAsync(new CreateBloodNeedRequest(
            BloodType.OPositive, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(2), null));

        var needServiceAsAdmin = new BloodNeedService(dbContext, adminUser);

        // Reason is required for a rejection, this is a rule worth locking in with a test,
        // not just something to notice by reading the code.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            needServiceAsAdmin.RejectAsync(new NeedDecisionRequest(need.Id, null)));
    }

    [Fact]
    public async Task Cancel_ByOriginalCreator_BeforeAdminActs_Succeeds()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);

        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);

        var needService = new BloodNeedService(dbContext, staffUser);
        var need = await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.ABNegative, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(3), null));

        await needService.CancelAsync(new NeedDecisionRequest(need.Id, "Ordered in error"));

        var updated = (await needService.GetMineAsync()).Single(item => item.Id == need.Id);
        Assert.Equal(BloodNeedStatus.Cancelled, updated.Status);
    }
}
