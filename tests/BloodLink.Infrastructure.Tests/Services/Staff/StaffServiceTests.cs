using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Staff;
using BloodLink.Infrastructure.Tests.Services;

namespace BloodLink.Infrastructure.Tests.Services.Staff;

public sealed class StaffServiceTests
{
    [Fact]
    public async Task CreateStaffAsync_FacilityAdminCreatesStaffForOwnApprovedFacility()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = new StaffService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        var result = await service.CreateStaffAsync(CreateRequest());

        Assert.Equal(WorkflowTestSupport.FacilityAId, result.FacilityId);
        Assert.Equal(StaffStatus.PendingActivation, result.Status);
        var user = Assert.Single(dbContext.Users.Where(item => item.Email == "staff@example.test"));
        Assert.Equal(WorkflowTestSupport.FacilityAId, user.FacilityId);
        Assert.True(user.IsActive);
        Assert.True(user.MustChangePassword);
        var staff = Assert.Single(dbContext.FacilityStaff.Where(item => item.UserId == user.Id));
        Assert.Equal("admin-a", staff.CreatedByAdminId);
        Assert.Single(dbContext.UserRoles.Where(userRole => userRole.UserId == user.Id && userRole.RoleId == RoleNames.FacilityStaff));
        Assert.Single(dbContext.Notifications.Where(notification => notification.RecipientUserId == user.Id && notification.NotificationType == NotificationType.AccountCreated));
        Assert.Single(dbContext.AuditLogs.Where(log => log.Action == "StaffCreated" && log.FacilityId == WorkflowTestSupport.FacilityAId));
    }

    [Fact]
    public async Task CreateStaffAsync_RejectsWrongRolePendingFacilityAndDuplicateEmail()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "existing", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);

        var staffService = new StaffService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => staffService.CreateStaffAsync(CreateRequest()));

        var pendingService = new StaffService(dbContext, AdminUser("admin-c", WorkflowTestSupport.FacilityCId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pendingService.CreateStaffAsync(CreateRequest()));

        var adminService = new StaffService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminService.CreateStaffAsync(CreateRequest(email: "existing@example.test")));
    }

    [Fact]
    public async Task ListOwnFacilityStaffAsync_ReturnsOnlyOwnFacilityStaff()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var serviceA = new StaffService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));
        var serviceB = new StaffService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId));
        var staffA = await serviceA.CreateStaffAsync(CreateRequest(email: "a@example.test"));
        await serviceB.CreateStaffAsync(CreateRequest(email: "b@example.test"));

        var result = await serviceA.ListOwnFacilityStaffAsync();

        Assert.Equal(staffA.UserId, Assert.Single(result).UserId);
    }

    [Fact]
    public async Task DeactivateStaffAsync_BlocksUserAndStoresReason()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = new StaffService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));
        var staff = await service.CreateStaffAsync(CreateRequest());

        await service.DeactivateStaffAsync(new ChangeStaffStatusRequest(staff.UserId, "No longer assigned"));

        var staffRecord = dbContext.FacilityStaff.Single(item => item.UserId == staff.UserId);
        var user = dbContext.Users.Single(item => item.Id == staff.UserId);
        Assert.Equal(StaffStatus.Inactive, staffRecord.Status);
        Assert.False(user.IsActive);
        Assert.Equal("No longer assigned", staffRecord.StatusReason);
        Assert.NotNull(staffRecord.DeactivatedAtUtc);
    }

    [Fact]
    public async Task DeactivateStaffAsync_CannotTargetAnotherFacility()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var serviceB = new StaffService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId));
        var staffB = await serviceB.CreateStaffAsync(CreateRequest(email: "b@example.test"));
        var serviceA = new StaffService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            serviceA.DeactivateStaffAsync(new ChangeStaffStatusRequest(staffB.UserId, "Wrong facility")));
    }

    [Fact]
    public async Task ReactivateAndResetTemporaryPasswordAsync_RequireOwnActiveFacility()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = new StaffService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));
        var staff = await service.CreateStaffAsync(CreateRequest());
        await service.DeactivateStaffAsync(new ChangeStaffStatusRequest(staff.UserId, "Leave"));

        await service.ReactivateStaffAsync(new ChangeStaffStatusRequest(staff.UserId, string.Empty));
        await service.ResetTemporaryPasswordAsync(staff.UserId);

        var staffRecord = dbContext.FacilityStaff.Single(item => item.UserId == staff.UserId);
        var user = dbContext.Users.Single(item => item.Id == staff.UserId);
        Assert.Equal(StaffStatus.Active, staffRecord.Status);
        Assert.True(user.IsActive);
        Assert.True(user.MustChangePassword);
        Assert.Single(dbContext.AuditLogs.Where(log => log.Action == "StaffPasswordReset" && log.EntityId == staffRecord.Id));
    }

    private static CreateStaffRequest CreateRequest(string email = "staff@example.test") =>
        new("Ama", "Mensah", email, "0242222222");

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
