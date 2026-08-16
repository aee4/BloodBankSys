using BloodLink.Acceptance.Tests.Support;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Needs;
using BloodLink.Infrastructure.Services.Notifications;
using BloodLink.Infrastructure.Services.Requests;

namespace BloodLink.Acceptance.Tests;

public sealed class NotificationsAcceptanceTests
{
    [Fact]
    public async Task CreatingANeed_NotifiesTheOwnFacilitysAdmins()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);

        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);
        var adminAUser = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        adminAUser.RoleList.Add(RoleNames.FacilityAdmin);

        var needService = new BloodNeedService(dbContext, staffUser);
        await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.ONegative, 1, UrgencyLevel.Urgent, DateTime.UtcNow.AddHours(4), null));

        var notificationService = new NotificationService(dbContext, adminAUser);
        var adminNotifications = await notificationService.ListMineAsync();

        Assert.Contains(adminNotifications, item => item.NotificationType == NotificationType.NewNeed);
    }

    [Fact]
    public async Task SendingARequest_NotifiesTheSourceFacilitysAdmins_NotTheRequestingFacility()
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

        var requestService = new BloodRequestService(dbContext, adminAUser, inventory);
        await requestService.CreateFromNeedAsync(new CreateBloodRequestRequest(need.Id, WorkflowTestSupport.FacilityBId, 1, null));

        var notificationServiceForSource = new NotificationService(dbContext, adminBUser);
        var sourceNotifications = await notificationServiceForSource.ListMineAsync();
        Assert.Contains(sourceNotifications, item => item.NotificationType == NotificationType.NewExternalRequest);

        var notificationServiceForRequester = new NotificationService(dbContext, adminAUser);
        var requesterNotifications = await notificationServiceForRequester.ListMineAsync();
        Assert.DoesNotContain(requesterNotifications, item => item.NotificationType == NotificationType.NewExternalRequest);
    }

    [Fact]
    public async Task AcceptingARequest_NotifiesTheOriginalRequester()
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

        var notificationServiceForRequester = new NotificationService(dbContext, adminAUser);
        var requesterNotifications = await notificationServiceForRequester.ListMineAsync();

        Assert.Contains(requesterNotifications, item => item.NotificationType == NotificationType.RequestResponse);
    }
}
