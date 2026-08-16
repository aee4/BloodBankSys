using BloodLink.Acceptance.Tests.Support;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Needs;
using BloodLink.Infrastructure.Services.Requests;

namespace BloodLink.Acceptance.Tests;

public sealed class FulfilmentAcceptanceTests
{
    [Fact]
    public async Task Fulfil_AfterAccepted_TransfersStockAndClosesTheLinkedNeed()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);

        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);
        var adminAUser = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        adminAUser.RoleList.Add(RoleNames.FacilityAdmin);
        var adminBUser = new FakeCurrentUserService { UserId = "admin-b", FacilityId = WorkflowTestSupport.FacilityBId };
        adminBUser.RoleList.Add(RoleNames.FacilityAdmin);

        var inventory = new FakeInventoryService();

        var needService = new BloodNeedService(dbContext, staffUser);
        var need = await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.ONegative, 2, UrgencyLevel.Urgent, DateTime.UtcNow.AddHours(6), null));

        var needServiceAsAdmin = new BloodNeedService(dbContext, adminAUser);
        await needServiceAsAdmin.StartSearchAsync(new NeedDecisionRequest(need.Id, null));

        var requestServiceAsRequester = new BloodRequestService(dbContext, adminAUser, inventory);
        var request = await requestServiceAsRequester.CreateFromNeedAsync(new CreateBloodRequestRequest(
            need.Id, WorkflowTestSupport.FacilityBId, 2, null));

        var requestServiceAsSource = new BloodRequestService(dbContext, adminBUser, inventory);
        await requestServiceAsSource.AcceptAsync(new RequestResponseRequest(request.Id, 2, null));

        await requestServiceAsSource.FulfilAsync(new FulfilRequestRequest(request.Id, "Handed over at the gate"));

        Assert.Equal(1, inventory.FulfilCalls);
        var storedRequest = await requestServiceAsSource.GetAsync(request.Id);
        Assert.Equal(BloodRequestStatus.Fulfilled, storedRequest!.Status);

        // Fulfilling the request should also close out the need it came from.
        var storedNeed = (await needServiceAsAdmin.ListOwnFacilityAsync()).Single(item => item.Id == need.Id);
        Assert.Equal(BloodNeedStatus.FulfilledExternally, storedNeed.Status);
    }

    [Fact]
    public async Task Fulfil_WhileStillSent_ThrowsInvalidOperationException()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);

        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);
        var adminAUser = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        adminAUser.RoleList.Add(RoleNames.FacilityAdmin);
        var adminBUser = new FakeCurrentUserService { UserId = "admin-b", FacilityId = WorkflowTestSupport.FacilityBId };
        adminBUser.RoleList.Add(RoleNames.FacilityAdmin);

        var inventory = new FakeInventoryService();

        var needService = new BloodNeedService(dbContext, staffUser);
        var need = await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.ONegative, 2, UrgencyLevel.Urgent, DateTime.UtcNow.AddHours(6), null));

        var needServiceAsAdmin = new BloodNeedService(dbContext, adminAUser);
        await needServiceAsAdmin.StartSearchAsync(new NeedDecisionRequest(need.Id, null));

        var requestServiceAsRequester = new BloodRequestService(dbContext, adminAUser, inventory);
        var request = await requestServiceAsRequester.CreateFromNeedAsync(new CreateBloodRequestRequest(
            need.Id, WorkflowTestSupport.FacilityBId, 2, null));

        var requestServiceAsSource = new BloodRequestService(dbContext, adminBUser, inventory);

        // Skipping straight from Sent to Fulfil without an Accept in between should be blocked.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            requestServiceAsSource.FulfilAsync(new FulfilRequestRequest(request.Id, null)));
    }
}
