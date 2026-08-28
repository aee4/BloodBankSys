using BloodLink.Acceptance.Tests.Support;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Needs;
using BloodLink.Infrastructure.Services.Requests;

namespace BloodLink.Acceptance.Tests;

public sealed class RequestResponseAcceptanceTests
{
    private static async Task<(BloodLink.Infrastructure.Data.BloodLinkDbContext DbContext, FakeInventoryService Inventory, Guid RequestId)>
        ArrangeSentRequestAsync()
    {
        var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);

        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);
        var adminAUser = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        adminAUser.RoleList.Add(RoleNames.FacilityAdmin);

        var inventory = new FakeInventoryService();

        var needService = new BloodNeedService(dbContext, staffUser);
        var need = await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.ONegative, 2, UrgencyLevel.Urgent, DateTime.UtcNow.AddHours(6), null));

        var needServiceAsAdmin = new BloodNeedService(dbContext, adminAUser);
        await needServiceAsAdmin.StartSearchAsync(new NeedDecisionRequest(need.Id, null));

        var requestService = new BloodRequestService(dbContext, adminAUser, inventory);
        var request = await requestService.CreateFromNeedAsync(new CreateBloodRequestRequest(
            need.Id, WorkflowTestSupport.FacilityBId, 2, null));

        return (dbContext, inventory, request.Id);
    }

    [Fact]
    public async Task Accept_BySourceFacilityAdmin_MovesStatusToAccepted()
    {
        var (dbContext, inventory, requestId) = await ArrangeSentRequestAsync();
        await using var _ = dbContext;

        var adminBUser = new FakeCurrentUserService { UserId = "admin-b", FacilityId = WorkflowTestSupport.FacilityBId };
        adminBUser.RoleList.Add(RoleNames.FacilityAdmin);
        var requestServiceAsSource = new BloodRequestService(dbContext, adminBUser, inventory);

        await requestServiceAsSource.AcceptAsync(new RequestResponseRequest(requestId, 2, "Confirmed available"));

        var stored = await requestServiceAsSource.GetAsync(requestId);
        Assert.Equal(BloodRequestStatus.Accepted, stored!.Status);
    }

    [Fact]
    public async Task Reject_WithoutAReason_ThrowsArgumentException()
    {
        var (dbContext, inventory, requestId) = await ArrangeSentRequestAsync();
        await using var _ = dbContext;

        var adminBUser = new FakeCurrentUserService { UserId = "admin-b", FacilityId = WorkflowTestSupport.FacilityBId };
        adminBUser.RoleList.Add(RoleNames.FacilityAdmin);
        var requestServiceAsSource = new BloodRequestService(dbContext, adminBUser, inventory);

        // A rejection reason is required by the service, worth locking in as a test.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            requestServiceAsSource.RejectAsync(new RequestResponseRequest(requestId, null, null)));
    }

    [Fact]
    public async Task Accept_ByRequestingFacilityAdmin_IsRejectedAsUnauthorized()
    {
        var (dbContext, inventory, requestId) = await ArrangeSentRequestAsync();
        await using var _ = dbContext;

        // Only the source facility may accept, not the facility that sent the request.
        var adminAUser = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        adminAUser.RoleList.Add(RoleNames.FacilityAdmin);
        var requestServiceAsWrongSide = new BloodRequestService(dbContext, adminAUser, inventory);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            requestServiceAsWrongSide.AcceptAsync(new RequestResponseRequest(requestId, 2, null)));
    }
}
