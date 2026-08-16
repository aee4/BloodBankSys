using BloodLink.Acceptance.Tests.Support;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Dashboard;
using BloodLink.Infrastructure.Services.Needs;
using BloodLink.Infrastructure.Services.Requests;

namespace BloodLink.Acceptance.Tests;

public sealed class DashboardsAcceptanceTests
{
    [Fact]
    public async Task FacilityAdminDashboard_CountsOnlyOpenNeeds_NotFulfilledOrRejected()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);

        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);
        var adminAUser = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        adminAUser.RoleList.Add(RoleNames.FacilityAdmin);

        var needService = new BloodNeedService(dbContext, staffUser);
        var needServiceAsAdmin = new BloodNeedService(dbContext, adminAUser);

        // One need left open (PendingReview), one need fulfilled internally, and one rejected.
        var openNeed = await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.ONegative, 1, UrgencyLevel.Urgent, DateTime.UtcNow.AddHours(4), null));

        var fulfilledNeed = await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.APositive, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(1), null));
        await needServiceAsAdmin.FulfilInternallyAsync(new NeedDecisionRequest(fulfilledNeed.Id, "Covered from stock"));

        var rejectedNeed = await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.BNegative, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(1), null));
        await needServiceAsAdmin.RejectAsync(new NeedDecisionRequest(rejectedNeed.Id, "Duplicate request"));

        var dashboardService = new DashboardService(dbContext, adminAUser);
        var dashboard = await dashboardService.GetFacilityAdminDashboardAsync();

        Assert.Equal(1, dashboard.OpenNeeds);
    }

    [Fact]
    public async Task FacilityAdminDashboard_ExcludesFulfilledRequestsFromSentAndReceivedCounts()
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
            BloodType.ONegative, 1, UrgencyLevel.Urgent, DateTime.UtcNow.AddHours(4), null));

        var needServiceAsAdmin = new BloodNeedService(dbContext, adminAUser);
        await needServiceAsAdmin.StartSearchAsync(new NeedDecisionRequest(need.Id, null));

        var requestServiceAsRequester = new BloodRequestService(dbContext, adminAUser, inventory);
        var request = await requestServiceAsRequester.CreateFromNeedAsync(
            new CreateBloodRequestRequest(need.Id, WorkflowTestSupport.FacilityBId, 1, null));

        var requestServiceAsSource = new BloodRequestService(dbContext, adminBUser, inventory);
        await requestServiceAsSource.AcceptAsync(new RequestResponseRequest(request.Id, 1, null));
        await requestServiceAsSource.FulfilAsync(new FulfilRequestRequest(request.Id, null));

        var dashboardServiceForRequester = new DashboardService(dbContext, adminAUser);
        var requesterDashboard = await dashboardServiceForRequester.GetFacilityAdminDashboardAsync();
        Assert.Equal(0, requesterDashboard.SentRequests);

        var dashboardServiceForSource = new DashboardService(dbContext, adminBUser);
        var sourceDashboard = await dashboardServiceForSource.GetFacilityAdminDashboardAsync();
        Assert.Equal(0, sourceDashboard.ReceivedRequests);
    }
}