using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Needs;

namespace BloodLink.Infrastructure.Tests.Services.Needs;

public sealed class BloodNeedServiceTests
{
    [Fact]
    public async Task CreateAsync_FacilityStaffCreatesPendingNeedAndNotifiesFacilityAdmins()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);
        var user = StaffUser("staff-a", WorkflowTestSupport.FacilityAId);
        var service = new BloodNeedService(dbContext, user);

        var result = await service.CreateAsync(new CreateBloodNeedRequest(BloodType.APositive, 2, UrgencyLevel.Emergency, DateTime.UtcNow.AddHours(3), "Operating room reserve low"));

        Assert.Equal(WorkflowTestSupport.FacilityAId, result.FacilityId);
        Assert.Equal(BloodNeedStatus.PendingReview, result.Status);
        var storedNeed = Assert.Single(dbContext.BloodNeeds);
        Assert.Equal("staff-a", storedNeed.RequestedByUserId);
        Assert.Single(dbContext.Notifications.Where(notification => notification.RecipientUserId == "admin-a" && notification.NotificationType == NotificationType.NewNeed));
        Assert.DoesNotContain(dbContext.Notifications, notification => notification.RecipientUserId == "admin-b");
    }

    [Theory]
    [InlineData(false, true, RoleNames.FacilityStaff)]
    [InlineData(true, false, RoleNames.FacilityStaff)]
    [InlineData(true, true, RoleNames.FacilityAdmin)]
    public async Task CreateAsync_RejectsInvalidUserContext(bool authenticated, bool active, string role)
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var user = new FakeCurrentUserService
        {
            UserId = "user",
            IsAuthenticated = authenticated,
            IsActive = active,
            FacilityId = WorkflowTestSupport.FacilityAId
        };
        user.RoleList.Add(role);
        var service = new BloodNeedService(dbContext, user);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateBloodNeedRequest(BloodType.APositive, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(1), null)));
    }

    [Fact]
    public async Task CreateAsync_RejectsUnapprovedFacility()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = new BloodNeedService(dbContext, StaffUser("staff-c", WorkflowTestSupport.FacilityCId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateBloodNeedRequest(BloodType.APositive, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(1), null)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_RejectsNonPositiveUnits(int units)
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = new BloodNeedService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateBloodNeedRequest(BloodType.APositive, units, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(1), null)));
    }

    [Fact]
    public async Task CreateAsync_RejectsPatientIdentifyingNote()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = new BloodNeedService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateBloodNeedRequest(BloodType.APositive, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddDays(1), "Patient name: Example")));
    }

    [Fact]
    public async Task CreateAsync_AcceptsFutureNeededByUtc()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = new BloodNeedService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));

        var result = await service.CreateAsync(new CreateBloodNeedRequest(BloodType.APositive, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddMinutes(5), null));

        Assert.Equal(BloodNeedStatus.PendingReview, result.Status);
    }

    [Fact]
    public async Task CreateAsync_RejectsPastNeededByUtc()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = new BloodNeedService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateBloodNeedRequest(BloodType.APositive, 1, UrgencyLevel.Routine, DateTime.UtcNow.AddMinutes(-5), null)));
    }

    [Fact]
    public async Task GetMineAsync_ReturnsOnlyCallerNeeds()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a");
        WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-other");
        WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityBId, "staff-a");
        var service = new BloodNeedService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));

        var needs = await service.GetMineAsync();

        var need = Assert.Single(needs);
        Assert.Equal("staff-a", dbContext.BloodNeeds.Single(item => item.Id == need.Id).RequestedByUserId);
        Assert.Equal(WorkflowTestSupport.FacilityAId, need.FacilityId);
    }

    [Fact]
    public async Task ListOwnFacilityAsync_ReturnsOnlyAdminsFacilityNeeds()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a");
        WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityBId, "staff-b");
        var service = new BloodNeedService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        var needs = await service.ListOwnFacilityAsync();

        var need = Assert.Single(needs);
        Assert.Equal(WorkflowTestSupport.FacilityAId, need.FacilityId);
    }

    [Fact]
    public async Task FacilityAdminActions_CannotTargetAnotherFacilityNeed()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityBId, "staff-b");
        var service = new BloodNeedService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.StartSearchAsync(new NeedDecisionRequest(need.Id, null)));
    }

    [Fact]
    public async Task StartSearchAsync_MovesPendingReviewToSearching()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a");
        var service = new BloodNeedService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await service.StartSearchAsync(new NeedDecisionRequest(need.Id, null));

        Assert.Equal(BloodNeedStatus.Searching, dbContext.BloodNeeds.Single().Status);
    }

    [Fact]
    public async Task StartSearchAsync_RejectsInvalidState()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var service = new BloodNeedService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartSearchAsync(new NeedDecisionRequest(need.Id, null)));
    }

    [Theory]
    [InlineData(BloodNeedStatus.PendingReview)]
    [InlineData(BloodNeedStatus.Searching)]
    public async Task FulfilInternallyAsync_WorksFromAllowedStates(BloodNeedStatus status)
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", status);
        var service = new BloodNeedService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await service.FulfilInternallyAsync(new NeedDecisionRequest(need.Id, "Covered by local stock"));

        Assert.Equal(BloodNeedStatus.FulfilledInternally, dbContext.BloodNeeds.Single().Status);
    }

    [Fact]
    public async Task RejectAsync_RequiresReason()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a");
        var service = new BloodNeedService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RejectAsync(new NeedDecisionRequest(need.Id, " ")));
    }

    [Fact]
    public async Task CancelAsync_CreatorCanCancelPendingReview()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a");
        var service = new BloodNeedService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));

        await service.CancelAsync(new NeedDecisionRequest(need.Id, "No longer needed"));

        Assert.Equal(BloodNeedStatus.Cancelled, dbContext.BloodNeeds.Single().Status);
    }

    [Fact]
    public async Task CancelAsync_RequiresReason()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a");
        var service = new BloodNeedService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelAsync(new NeedDecisionRequest(need.Id, " ")));

        Assert.Equal(BloodNeedStatus.PendingReview, dbContext.BloodNeeds.Single().Status);
    }

    [Theory]
    [InlineData(BloodRequestStatus.Sent)]
    [InlineData(BloodRequestStatus.Accepted)]
    public async Task CancelAsync_SearchingNeedWithActiveRequestIsBlocked(BloodRequestStatus activeStatus)
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, activeStatus);
        var service = new BloodNeedService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelAsync(new NeedDecisionRequest(need.Id, "No longer needed")));

        Assert.Equal(BloodNeedStatus.Searching, dbContext.BloodNeeds.Single().Status);
    }

    [Theory]
    [InlineData(BloodRequestStatus.Sent)]
    [InlineData(BloodRequestStatus.Accepted)]
    public async Task FulfilInternallyAsync_SearchingNeedWithActiveRequestIsBlocked(BloodRequestStatus activeStatus)
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, activeStatus);
        var service = new BloodNeedService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FulfilInternallyAsync(new NeedDecisionRequest(need.Id, "Covered locally")));

        Assert.Equal(BloodNeedStatus.Searching, dbContext.BloodNeeds.Single().Status);
    }

    [Fact]
    public async Task FinalNeedCannotTransitionAgain()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Cancelled);
        var service = new BloodNeedService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FulfilInternallyAsync(new NeedDecisionRequest(need.Id, null)));
    }

    private static FakeCurrentUserService StaffUser(string userId, Guid facilityId)
    {
        var user = new FakeCurrentUserService { UserId = userId, FacilityId = facilityId };
        user.RoleList.Add(RoleNames.FacilityStaff);
        return user;
    }

    private static FakeCurrentUserService AdminUser(string userId, Guid facilityId)
    {
        var user = new FakeCurrentUserService { UserId = userId, FacilityId = facilityId };
        user.RoleList.Add(RoleNames.FacilityAdmin);
        return user;
    }
}
