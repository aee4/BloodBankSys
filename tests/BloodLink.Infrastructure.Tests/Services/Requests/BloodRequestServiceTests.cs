using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Requests;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Tests.Services.Requests;

public sealed class BloodRequestServiceTests
{
    [Fact]
    public async Task CreateFromNeedAsync_CreatesSentRequestHistoryAndSourceNotifications()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching, units: 5);
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        var result = await service.CreateFromNeedAsync(new CreateBloodRequestRequest(need.Id, WorkflowTestSupport.FacilityBId, 3, "Please review"));

        Assert.Equal(BloodRequestStatus.Sent, result.Status);
        Assert.Equal(need.BloodType, result.BloodType);
        Assert.Single(dbContext.BloodRequestStatusHistory.Where(history => history.BloodRequestId == result.Id && history.FromStatus == null && history.ToStatus == BloodRequestStatus.Sent));
        Assert.Single(dbContext.Notifications.Where(notification => notification.RecipientUserId == "admin-b" && notification.NotificationType == NotificationType.NewExternalRequest));
        Assert.Equal("Facility A", result.RequestingFacilityName);
        Assert.Equal("Facility B", result.SourceFacilityName);
        Assert.Equal(need.Urgency, result.Priority);
        Assert.Equal(0, dbContext.BloodInventory.Count());
        var timeline = await service.GetTimelineAsync(result.Id);
        Assert.Single(timeline);
        Assert.Equal(BloodRequestStatus.Sent, timeline[0].ToStatus);
        Assert.Equal("admin-a@example.test", timeline[0].ActorDisplayName);
    }

    [Fact]
    public async Task CreateFromNeedAsync_RechecksCurrentExactTypeAvailability()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var inventory = new FakeInventoryService { NoAvailability = true };
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId), inventory);

        await Assert.ThrowsAsync<BloodLink.Domain.Exceptions.InsufficientInventoryException>(() =>
            service.CreateFromNeedAsync(new CreateBloodRequestRequest(need.Id, WorkflowTestSupport.FacilityBId, 2, null)));

        Assert.Empty(dbContext.BloodRequests);
        Assert.Empty(dbContext.BloodRequestStatusHistory);
    }

    [Fact]
    public async Task CreateFromNeedAsync_RejectsWrongRole()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var service = CreateService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateFromNeedAsync(new CreateBloodRequestRequest(need.Id, WorkflowTestSupport.FacilityBId, 1, null)));
    }

    [Fact]
    public async Task CreateFromNeedAsync_RequiresOwnSearchingNeed()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var otherNeed = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityBId, "staff-b", BloodNeedStatus.Searching);
        var pendingNeed = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.PendingReview);
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateFromNeedAsync(new CreateBloodRequestRequest(otherNeed.Id, WorkflowTestSupport.FacilityBId, 1, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateFromNeedAsync(new CreateBloodRequestRequest(pendingNeed.Id, WorkflowTestSupport.FacilityBId, 1, null)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateFromNeedAsync_RejectsNonPositiveUnits(int units)
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateFromNeedAsync(new CreateBloodRequestRequest(need.Id, WorkflowTestSupport.FacilityBId, units, null)));
    }

    [Fact]
    public async Task CreateFromNeedAsync_RejectsSameOrUnapprovedSourceAndDuplicateActiveRequest()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateFromNeedAsync(new CreateBloodRequestRequest(need.Id, WorkflowTestSupport.FacilityAId, 1, null)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateFromNeedAsync(new CreateBloodRequestRequest(need.Id, WorkflowTestSupport.FacilityCId, 1, null)));

        WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateFromNeedAsync(new CreateBloodRequestRequest(need.Id, WorkflowTestSupport.FacilityBId, 1, null)));
    }

    [Fact]
    public async Task ListsAndGetAreFacilityScoped()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var needA = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var needB = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityBId, "staff-b", BloodNeedStatus.Searching);
        var sentByA = WorkflowTestSupport.AddRequest(dbContext, needA.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId);
        var sentByB = WorkflowTestSupport.AddRequest(dbContext, needB.Id, WorkflowTestSupport.FacilityBId, WorkflowTestSupport.FacilityAId);
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        var sent = await service.ListSentAsync();
        var received = await service.ListReceivedAsync();
        var viewable = await service.GetAsync(sentByA.Id);

        Assert.Equal(sentByA.Id, Assert.Single(sent).Id);
        Assert.Equal(sentByB.Id, Assert.Single(received).Id);
        Assert.NotNull(viewable);

        var unrelated = WorkflowTestSupport.AddRequest(dbContext, needB.Id, WorkflowTestSupport.FacilityBId, WorkflowTestSupport.FacilityCId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(unrelated.Id));
    }

    [Fact]
    public async Task AcceptAsync_SourceAdminAcceptsSentRequestAndCallsReserve()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var bloodRequest = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, unitsRequested: 4);
        var inventory = new FakeInventoryService();
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId), inventory);

        await service.AcceptAsync(new RequestResponseRequest(bloodRequest.Id, 3, "Can supply three"));

        var stored = dbContext.BloodRequests.Single(item => item.Id == bloodRequest.Id);
        Assert.Equal(BloodRequestStatus.Accepted, stored.Status);
        Assert.Equal(3, stored.UnitsAccepted);
        Assert.Equal(1, inventory.ReserveCalls);
        Assert.Single(dbContext.BloodRequestStatusHistory.Where(history => history.ToStatus == BloodRequestStatus.Accepted));
        Assert.Single(dbContext.Notifications.Where(notification => notification.NotificationType == NotificationType.RequestResponse));
    }

    [Fact]
    public async Task AcceptAsync_RejectsWrongSourceStateAndInvalidUnits()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var accepted = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Accepted);
        var sent = WorkflowTestSupport.AddRequest(dbContext, Guid.NewGuid(), WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, unitsRequested: 2);
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync(new RequestResponseRequest(accepted.Id, 1, null)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AcceptAsync(new RequestResponseRequest(sent.Id, 3, null)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId)).AcceptAsync(new RequestResponseRequest(sent.Id, 1, null)));
    }

    [Fact]
    public async Task AcceptAsync_ReserveFailureLeavesRequestSent()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var bloodRequest = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId);
        var inventory = new FakeInventoryService { FailReserve = true };
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId), inventory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync(new RequestResponseRequest(bloodRequest.Id, 1, null)));

        Assert.Equal(BloodRequestStatus.Sent, dbContext.BloodRequests.Single(item => item.Id == bloodRequest.Id).Status);
    }

    [Fact]
    public async Task AcceptAsync_RequestingAdminReceivesOneNotificationWhenAlsoActiveFacilityAdmin()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-other", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var bloodRequest = WorkflowTestSupport.AddRequest(
            dbContext,
            need.Id,
            WorkflowTestSupport.FacilityAId,
            WorkflowTestSupport.FacilityBId,
            requestedByAdminId: "admin-a");
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId));

        await service.AcceptAsync(new RequestResponseRequest(bloodRequest.Id, 1, null));

        var responseNotifications = dbContext.Notifications
            .Where(notification => notification.NotificationType == NotificationType.RequestResponse)
            .ToList();
        Assert.Equal(2, responseNotifications.Count);
        Assert.Single(responseNotifications, notification => notification.RecipientUserId == "admin-a");
        Assert.Single(responseNotifications, notification => notification.RecipientUserId == "admin-other");
    }

    [Fact]
    public async Task RejectAsync_RequiresReasonAndLeavesNeedSearching()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var bloodRequest = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId);
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId));

        await Assert.ThrowsAsync<ArgumentException>(() => service.RejectAsync(new RequestResponseRequest(bloodRequest.Id, null, "")));
        await service.RejectAsync(new RequestResponseRequest(bloodRequest.Id, null, "Unavailable"));

        Assert.Equal(BloodRequestStatus.Rejected, dbContext.BloodRequests.Single().Status);
        Assert.Equal(BloodNeedStatus.Searching, dbContext.BloodNeeds.Single().Status);
        Assert.Single(dbContext.BloodRequestStatusHistory.Where(history => history.FromStatus == BloodRequestStatus.Sent && history.ToStatus == BloodRequestStatus.Rejected));
    }

    [Fact]
    public async Task RejectAsync_WrongSourceFacilityCannotReject()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var bloodRequest = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId);
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RejectAsync(new RequestResponseRequest(bloodRequest.Id, null, "Unavailable")));
    }

    [Fact]
    public async Task CancelAsync_SourceAdminCancelsSentAndAcceptedWithEvidenceAndOnePartialRelease()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var sent = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId);
        var accepted = WorkflowTestSupport.AddRequest(dbContext, Guid.NewGuid(), WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId,
            BloodRequestStatus.Accepted, unitsRequested: 5, unitsAccepted: 2);
        var inventory = new FakeInventoryService();
        var requester = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId), inventory);
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId), inventory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => requester.CancelAsync(sent.Id));
        await service.CancelAsync(sent.Id);
        await service.CancelAsync(accepted.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(accepted.Id));

        Assert.Equal(1, inventory.ReleaseCalls);
        Assert.All(dbContext.BloodRequests, request => Assert.Equal(BloodRequestStatus.Cancelled, request.Status));
        Assert.Single(dbContext.BloodRequestStatusHistory.Where(history => history.BloodRequestId == sent.Id && history.FromStatus == BloodRequestStatus.Sent && history.ToStatus == BloodRequestStatus.Cancelled));
        Assert.Single(dbContext.BloodRequestStatusHistory.Where(history => history.BloodRequestId == accepted.Id && history.FromStatus == BloodRequestStatus.Accepted && history.ToStatus == BloodRequestStatus.Cancelled));
        Assert.Equal(2, dbContext.AuditLogs.Count(log => log.Action == "BloodRequestCancelled"));
        Assert.Equal(2, dbContext.Notifications.Count(notification => notification.Title == "Blood request cancelled"));
        Assert.Equal(BloodNeedStatus.Searching, dbContext.BloodNeeds.Single().Status);
    }

    [Fact]
    public async Task CancelAsync_RejectsRequesterUnrelatedStaffSystemAdminAndInactiveSource()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-c", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityCId);
        WorkflowTestSupport.AddUser(dbContext, "system", RoleNames.SystemAdmin, null);
        var unrelatedFacility = await dbContext.Facilities.SingleAsync(item => item.Id == WorkflowTestSupport.FacilityCId);
        unrelatedFacility.Status = FacilityStatus.Approved;
        await dbContext.SaveChangesAsync();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var request = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId);
        var requester = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));
        var unrelatedAdmin = CreateService(dbContext, AdminUser("admin-c", WorkflowTestSupport.FacilityCId));
        var staff = CreateService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));
        var systemAdmin = new FakeCurrentUserService { UserId = "system" };
        systemAdmin.RoleList.Add(RoleNames.SystemAdmin);
        var inactiveSource = AdminUser("admin-b", WorkflowTestSupport.FacilityBId);
        inactiveSource.IsActive = false;
        var unauthenticatedSource = AdminUser("admin-b", WorkflowTestSupport.FacilityBId);
        unauthenticatedSource.IsAuthenticated = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => requester.CancelAsync(request.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => unrelatedAdmin.CancelAsync(request.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => staff.CancelAsync(request.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(dbContext, systemAdmin).CancelAsync(request.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(dbContext, inactiveSource).CancelAsync(request.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(dbContext, unauthenticatedSource).CancelAsync(request.Id));

        Assert.Equal(BloodRequestStatus.Sent, dbContext.BloodRequests.Single().Status);
        Assert.Empty(dbContext.BloodRequestStatusHistory.Where(history => history.ToStatus == BloodRequestStatus.Cancelled));
        Assert.Empty(dbContext.AuditLogs.Where(log => log.Action == "BloodRequestCancelled"));
        Assert.Empty(dbContext.Notifications.Where(notification => notification.Title == "Blood request cancelled"));
    }

    [Theory]
    [InlineData(FacilityStatus.Pending)]
    [InlineData(FacilityStatus.Rejected)]
    [InlineData(FacilityStatus.Suspended)]
    public async Task CancelAsync_RejectsSourceAdminWhenSourceFacilityIsNotApproved(FacilityStatus status)
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var request = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId);
        var source = await dbContext.Facilities.SingleAsync(item => item.Id == WorkflowTestSupport.FacilityBId);
        source.Status = status;
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId)).CancelAsync(request.Id));

        Assert.Equal(BloodRequestStatus.Sent, dbContext.BloodRequests.Single().Status);
        Assert.Empty(dbContext.BloodRequestStatusHistory.Where(history => history.ToStatus == BloodRequestStatus.Cancelled));
    }

    [Fact]
    public async Task CancelAsync_RejectsSentAndAcceptedTerminalStatesWithoutEffects()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);
        var rejectedNeed = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var cancelledNeed = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var fulfilledNeed = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var requests = new[]
        {
            WorkflowTestSupport.AddRequest(dbContext, rejectedNeed.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Rejected),
            WorkflowTestSupport.AddRequest(dbContext, cancelledNeed.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Cancelled),
            WorkflowTestSupport.AddRequest(dbContext, fulfilledNeed.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Fulfilled)
        };
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId));

        foreach (var request in requests)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(request.Id));
        }

        Assert.Empty(dbContext.BloodRequestStatusHistory.Where(history => history.ToStatus == BloodRequestStatus.Cancelled));
        Assert.Empty(dbContext.AuditLogs.Where(log => log.Action == "BloodRequestCancelled"));
        Assert.Empty(dbContext.Notifications.Where(notification => notification.Title == "Blood request cancelled"));
    }

    [Fact]
    public async Task CancelAsync_ReleaseFailureLeavesAcceptedRequestAccepted()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var accepted = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Accepted, unitsAccepted: 2);
        var inventory = new FakeInventoryService { FailRelease = true };
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId), inventory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(accepted.Id));

        Assert.Equal(BloodRequestStatus.Accepted, dbContext.BloodRequests.Single().Status);
        Assert.Empty(dbContext.BloodRequestStatusHistory);
    }

    [Fact]
    public async Task FulfilAsync_FulfilsAcceptedRequestNeedHistoryAndNotification()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var bloodRequest = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Accepted, unitsAccepted: 2);
        var inventory = new FakeInventoryService();
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId), inventory);

        await service.FulfilAsync(new FulfilRequestRequest(bloodRequest.Id, "Handed over"));

        Assert.Equal(1, inventory.FulfilCalls);
        Assert.Equal(BloodRequestStatus.Fulfilled, dbContext.BloodRequests.Single().Status);
        Assert.Equal(BloodNeedStatus.FulfilledExternally, dbContext.BloodNeeds.Single().Status);
        Assert.Single(dbContext.BloodRequestStatusHistory.Where(history => history.ToStatus == BloodRequestStatus.Fulfilled));
        Assert.Single(dbContext.Notifications.Where(notification => notification.NotificationType == NotificationType.RequestFulfilled));
    }

    [Fact]
    public async Task FulfilAsync_RejectsDuplicateOrInvalidState()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var fulfilled = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Fulfilled);
        var sent = WorkflowTestSupport.AddRequest(dbContext, Guid.NewGuid(), WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Sent);
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FulfilAsync(new FulfilRequestRequest(fulfilled.Id, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FulfilAsync(new FulfilRequestRequest(sent.Id, null)));
    }

    [Theory]
    [InlineData(BloodNeedStatus.Cancelled)]
    [InlineData(BloodNeedStatus.Rejected)]
    [InlineData(BloodNeedStatus.FulfilledInternally)]
    [InlineData(BloodNeedStatus.FulfilledExternally)]
    public async Task FulfilAsync_RejectsIncompatibleLinkedNeedState(BloodNeedStatus needStatus)
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", needStatus);
        var bloodRequest = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Accepted, unitsAccepted: 2);
        var inventory = new FakeInventoryService();
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId), inventory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FulfilAsync(new FulfilRequestRequest(bloodRequest.Id, null)));

        Assert.Equal(0, inventory.FulfilCalls);
        Assert.Equal(BloodRequestStatus.Accepted, dbContext.BloodRequests.Single().Status);
        Assert.Equal(needStatus, dbContext.BloodNeeds.Single().Status);
    }

    [Fact]
    public async Task FulfilAsync_TransferFailureLeavesRequestAndNeedUnchanged()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var bloodRequest = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Accepted, unitsAccepted: 2);
        var inventory = new FakeInventoryService { FailFulfil = true };
        var service = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId), inventory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FulfilAsync(new FulfilRequestRequest(bloodRequest.Id, null)));

        Assert.Equal(BloodRequestStatus.Accepted, dbContext.BloodRequests.Single().Status);
        Assert.Equal(BloodNeedStatus.Searching, dbContext.BloodNeeds.Single().Status);
        Assert.Empty(dbContext.BloodRequestStatusHistory);
    }

    [Fact]
    public async Task FulfilAsync_WrongSourceFacilityCannotFulfil()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var bloodRequest = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Accepted, unitsAccepted: 2);
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.FulfilAsync(new FulfilRequestRequest(bloodRequest.Id, null)));
    }

    [Theory]
    [InlineData(BloodRequestStatus.Rejected)]
    [InlineData(BloodRequestStatus.Fulfilled)]
    [InlineData(BloodRequestStatus.Cancelled)]
    public async Task FinalRequestStatusesRejectFurtherTransitions(BloodRequestStatus finalStatus)
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var bloodRequest = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, finalStatus);
        var sourceService = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId));
        var requesterService = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sourceService.AcceptAsync(new RequestResponseRequest(bloodRequest.Id, 1, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sourceService.RejectAsync(new RequestResponseRequest(bloodRequest.Id, null, "Unavailable")));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            requesterService.CancelAsync(bloodRequest.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sourceService.FulfilAsync(new FulfilRequestRequest(bloodRequest.Id, null)));

        Assert.Equal(finalStatus, dbContext.BloodRequests.Single().Status);
    }

    private static BloodRequestService CreateService(
        BloodLink.Infrastructure.Data.BloodLinkDbContext dbContext,
        FakeCurrentUserService user,
        FakeInventoryService? inventory = null) =>
        new(dbContext, user, inventory ?? new FakeInventoryService());

    private static FakeCurrentUserService AdminUser(string userId, Guid facilityId)
    {
        var user = new FakeCurrentUserService { UserId = userId, FacilityId = facilityId };
        user.RoleList.Add(RoleNames.FacilityAdmin);
        return user;
    }

    private static FakeCurrentUserService StaffUser(string userId, Guid facilityId)
    {
        var user = new FakeCurrentUserService { UserId = userId, FacilityId = facilityId };
        user.RoleList.Add(RoleNames.FacilityStaff);
        return user;
    }

}
