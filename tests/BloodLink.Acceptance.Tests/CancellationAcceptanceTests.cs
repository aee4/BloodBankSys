using BloodLink.Acceptance.Tests.Support;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Needs;
using BloodLink.Infrastructure.Services.Requests;

namespace BloodLink.Acceptance.Tests;

public sealed class CancellationAcceptanceTests
{
    [Fact]
    public async Task Cancel_AfterAccepted_ReleasesTheReservation()
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
        Assert.Equal(1, inventory.ReserveCalls);
        Assert.Equal(0, inventory.ReleaseCalls);

        // Either side may cancel an accepted request, cancelling here as the requesting facility.
        await requestServiceAsRequester.CancelAsync(request.Id);

        Assert.Equal(1, inventory.ReleaseCalls);
        var stored = await requestServiceAsRequester.GetAsync(request.Id);
        Assert.Equal(BloodRequestStatus.Cancelled, stored!.Status);
    }

    [Fact]
    public async Task Cancel_WhileStillSent_DoesNotCallRelease()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);

        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);
        var adminAUser = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        adminAUser.RoleList.Add(RoleNames.FacilityAdmin);

        var inventory = new FakeInventoryService();

        var needService = new BloodNeedService(dbContext, staffUser);
        var need = await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.APositive, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(1), null));

        var needServiceAsAdmin = new BloodNeedService(dbContext, adminAUser);
        await needServiceAsAdmin.StartSearchAsync(new NeedDecisionRequest(need.Id, null));

        var requestService = new BloodRequestService(dbContext, adminAUser, inventory);
        var request = await requestService.CreateFromNeedAsync(new CreateBloodRequestRequest(
            need.Id, WorkflowTestSupport.FacilityBId, 1, null));

        // Nothing was ever reserved for a Sent (not yet accepted) request, so cancelling
        // it should not call release, there is nothing to release.
        await requestService.CancelAsync(request.Id);

        Assert.Equal(0, inventory.ReleaseCalls);
    }
}