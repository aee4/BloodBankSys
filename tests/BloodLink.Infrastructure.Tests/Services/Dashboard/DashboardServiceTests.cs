using BloodLink.Application.Contracts;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Dashboard;

namespace BloodLink.Infrastructure.Tests.Services.Dashboard;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task SystemAdminDashboard_CountsFacilitiesByPlatformStatus()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var user = new FakeCurrentUserService { UserId = "system", FacilityId = null };
        user.RoleList.Add(RoleNames.SystemAdmin);
        var service = new DashboardService(dbContext, user);

        var dashboard = await service.GetSystemAdminDashboardAsync();

        Assert.Equal(1, dashboard.PendingFacilities);
        Assert.Equal(2, dashboard.ApprovedFacilities);
        Assert.Equal(0, dashboard.SuspendedFacilities);
    }

    [Fact]
    public async Task FacilityAdminDashboard_IsScopedToOwnFacility()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var needA = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.PendingReview);
        WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.FulfilledExternally);
        var needB = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityBId, "staff-b", BloodNeedStatus.PendingReview);
        WorkflowTestSupport.AddRequest(dbContext, needA.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId, BloodRequestStatus.Sent);
        WorkflowTestSupport.AddRequest(dbContext, needB.Id, WorkflowTestSupport.FacilityBId, WorkflowTestSupport.FacilityAId, BloodRequestStatus.Accepted);
        dbContext.BloodInventory.AddRange(
            new BloodInventory { Id = Guid.NewGuid(), FacilityId = WorkflowTestSupport.FacilityAId, BloodType = BloodType.APositive, TotalUnits = 2, ReservedUnits = 0, LowStockThreshold = 3 },
            new BloodInventory { Id = Guid.NewGuid(), FacilityId = WorkflowTestSupport.FacilityBId, BloodType = BloodType.APositive, TotalUnits = 1, ReservedUnits = 0, LowStockThreshold = 3 });
        await dbContext.SaveChangesAsync();
        var user = AdminUser("admin-a", WorkflowTestSupport.FacilityAId);
        var service = new DashboardService(dbContext, user);

        var dashboard = await service.GetFacilityAdminDashboardAsync();

        Assert.Equal(1, dashboard.OpenNeeds);
        Assert.Equal(1, dashboard.SentRequests);
        Assert.Equal(1, dashboard.ReceivedRequests);
        Assert.Equal(1, dashboard.LowStockItems);
    }

    [Fact]
    public async Task StaffDashboard_IsScopedToOwnNeedsAndNotifications()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.PendingReview);
        WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-other", BloodNeedStatus.PendingReview);
        WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityBId, "staff-a", BloodNeedStatus.PendingReview);
        dbContext.Notifications.AddRange(
            new Notification { Id = Guid.NewGuid(), RecipientUserId = "staff-a", NotificationType = NotificationType.Security, Title = "A", Message = "A", IsRead = false },
            new Notification { Id = Guid.NewGuid(), RecipientUserId = "staff-a", NotificationType = NotificationType.Security, Title = "B", Message = "B", IsRead = true },
            new Notification { Id = Guid.NewGuid(), RecipientUserId = "staff-other", NotificationType = NotificationType.Security, Title = "C", Message = "C", IsRead = false });
        await dbContext.SaveChangesAsync();
        var service = new DashboardService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));

        var dashboard = await service.GetFacilityStaffDashboardAsync();

        Assert.Equal(1, dashboard.MyOpenNeeds);
        Assert.Equal(1, dashboard.UnreadNotifications);
    }

    [Fact]
    public async Task Dashboards_RejectWrongRoles()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var staffService = new DashboardService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));
        var adminService = new DashboardService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => staffService.GetFacilityAdminDashboardAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => adminService.GetFacilityStaffDashboardAsync());
    }

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
