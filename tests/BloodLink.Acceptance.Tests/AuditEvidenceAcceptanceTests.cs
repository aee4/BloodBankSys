using BloodLink.Acceptance.Tests.Support;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Needs;
using BloodLink.Infrastructure.Services.Requests;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Acceptance.Tests;

public sealed class AuditEvidenceAcceptanceTests
{
    [Fact]
    public async Task RequestTransitions_CreateImmutableStatusHistoryAuditTrail()
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

        var requestService = new BloodRequestService(dbContext, adminAUser, inventory);
        var request = await requestService.CreateFromNeedAsync(new CreateBloodRequestRequest(
            need.Id, WorkflowTestSupport.FacilityBId, 2, "Urgent transfer needed"));

        var requestServiceAsSource = new BloodRequestService(dbContext, adminBUser, inventory);
        await requestServiceAsSource.AcceptAsync(new RequestResponseRequest(request.Id, 2, "Stock available"));

        var historyEntries = await dbContext.BloodRequestStatusHistory
            .Where(h => h.BloodRequestId == request.Id)
            .OrderBy(h => h.ChangedAtUtc)
            .ToListAsync();

        Assert.Equal(2, historyEntries.Count);
        Assert.Equal(BloodRequestStatus.Sent, historyEntries[0].ToStatus);
        Assert.Null(historyEntries[0].FromStatus);
        Assert.Equal("admin-a", historyEntries[0].ChangedByUserId);

        Assert.Equal(BloodRequestStatus.Accepted, historyEntries[1].ToStatus);
        Assert.Equal(BloodRequestStatus.Sent, historyEntries[1].FromStatus);
        Assert.Equal("admin-b", historyEntries[1].ChangedByUserId);
    }

    // TODO: waiting on Backend Developer 1 (Poku Nancy) and Backend Developer 2 (Jephthah Peprah)
    // for AuditLog entries on Facility decisions, Staff actions, and Inventory adjustments.
}
