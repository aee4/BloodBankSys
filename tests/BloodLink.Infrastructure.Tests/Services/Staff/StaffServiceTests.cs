using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Application.Security;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Staff;
using BloodLink.Infrastructure.Tests.Services;
using Microsoft.Extensions.Configuration;

namespace BloodLink.Infrastructure.Tests.Services.Staff;

public sealed class StaffServiceTests
{
    [Fact]
    public async Task CreateStaffAsync_FacilityAdminCreatesStaffForOwnApprovedFacility()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var delivery = new FakePasswordResetDelivery();
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId), delivery);

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
        var message = Assert.Single(delivery.Messages);
        Assert.Equal("staff@example.test", message.Email);
        Assert.StartsWith("https://localhost/account/reset-password?", message.Url);
        Assert.DoesNotContain("BloodLink", message.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateStaffAsync_RejectsWrongRolePendingFacilityAndDuplicateEmail()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "existing", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);

        var staffService = CreateService(dbContext, StaffUser("staff-a", WorkflowTestSupport.FacilityAId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => staffService.CreateStaffAsync(CreateRequest()));

        var pendingService = CreateService(dbContext, AdminUser("admin-c", WorkflowTestSupport.FacilityCId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pendingService.CreateStaffAsync(CreateRequest()));

        var adminService = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminService.CreateStaffAsync(CreateRequest(email: "existing@example.test")));
    }

    [Fact]
    public async Task ListOwnFacilityStaffAsync_ReturnsOnlyOwnFacilityStaff()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var serviceA = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));
        var serviceB = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId));
        var staffA = await serviceA.CreateStaffAsync(CreateRequest(email: "a@example.test"));
        await serviceB.CreateStaffAsync(CreateRequest(email: "b@example.test"));

        var result = await serviceA.ListOwnFacilityStaffAsync();

        Assert.Equal(staffA.UserId, Assert.Single(result).UserId);
    }

    [Fact]
    public async Task DeactivateStaffAsync_BlocksUserAndStoresReason()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));
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
        var serviceB = CreateService(dbContext, AdminUser("admin-b", WorkflowTestSupport.FacilityBId));
        var staffB = await serviceB.CreateStaffAsync(CreateRequest(email: "b@example.test"));
        var serviceA = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            serviceA.DeactivateStaffAsync(new ChangeStaffStatusRequest(staffB.UserId, "Wrong facility")));
    }

    [Fact]
    public async Task ReactivateAndResetTemporaryPasswordAsync_RequireOwnActiveFacility()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var delivery = new FakePasswordResetDelivery();
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId), delivery);
        var staff = await service.CreateStaffAsync(CreateRequest());
        await service.DeactivateStaffAsync(new ChangeStaffStatusRequest(staff.UserId, "Leave"));

        await service.ReactivateStaffAsync(new ChangeStaffStatusRequest(staff.UserId, string.Empty));
        var reset = await service.ResetTemporaryPasswordAsync(staff.UserId);

        var staffRecord = dbContext.FacilityStaff.Single(item => item.UserId == staff.UserId);
        var user = dbContext.Users.Single(item => item.Id == staff.UserId);
        Assert.Equal(StaffStatus.Active, staffRecord.Status);
        Assert.True(user.IsActive);
        Assert.True(user.MustChangePassword);
        Assert.True(reset.Delivered);
        Assert.Equal(2, delivery.Messages.Count);
        Assert.Single(dbContext.AuditLogs.Where(log => log.Action == "StaffPasswordReset" && log.EntityId == staffRecord.Id));
    }

    [Fact]
    public async Task ResetTemporaryPasswordAsync_DeliveryFailureDoesNotPersistAccountChanges()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var delivery = new FakePasswordResetDelivery();
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId), delivery);
        var staff = await service.CreateStaffAsync(CreateRequest());
        var user = dbContext.Users.Single(item => item.Id == staff.UserId);
        user.MustChangePassword = false;
        user.SecurityStamp = "known-stamp";
        await dbContext.SaveChangesAsync();
        delivery.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResetTemporaryPasswordAsync(staff.UserId));

        dbContext.ChangeTracker.Clear();
        var savedUser = dbContext.Users.Single(item => item.Id == staff.UserId);
        Assert.False(savedUser.MustChangePassword);
        Assert.Equal("known-stamp", savedUser.SecurityStamp);
        Assert.Empty(dbContext.AuditLogs.Where(log => log.Action == "StaffPasswordReset"));
    }

    [Fact]
    public async Task CreateStaffAsync_RequiresConfiguredCredentialDelivery()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId),
            new FakePasswordResetDelivery { IsConfigured = false });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateStaffAsync(CreateRequest()));

        Assert.DoesNotContain(dbContext.Users, user => user.Email == "staff@example.test");
        Assert.DoesNotContain(dbContext.FacilityStaff, staff => staff.UserId == "staff@example.test");
    }

    [Fact]
    public async Task CreateStaffAsync_DeliveryFailureDoesNotPersistStaffAccount()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var service = CreateService(dbContext, AdminUser("admin-a", WorkflowTestSupport.FacilityAId),
            new FakePasswordResetDelivery { Fail = true });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateStaffAsync(CreateRequest()));

        Assert.DoesNotContain(dbContext.Users, user => user.Email == "staff@example.test");
        Assert.Empty(dbContext.FacilityStaff.Where(staff => staff.CreatedByAdminId == "admin-a"));
    }

    private static CreateStaffRequest CreateRequest(string email = "staff@example.test") =>
        new("Ama", "Mensah", email, "0242222222");

    private static StaffService CreateService(
        BloodLink.Infrastructure.Data.BloodLinkDbContext dbContext,
        FakeCurrentUserService currentUser,
        FakePasswordResetDelivery? delivery = null) =>
        new(
            dbContext,
            currentUser,
            passwordHasher: null,
            passwordDelivery: delivery ?? new FakePasswordResetDelivery(),
            userManager: null,
            configuration: new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Account:PublicOrigin"] = "https://localhost" })
                .Build());

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

    private sealed class FakePasswordResetDelivery : IPasswordResetDelivery
    {
        public bool IsConfigured { get; set; } = true;
        public bool Fail { get; set; }
        public List<(string Email, string Url)> Messages { get; } = [];

        public Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
        {
            if (Fail)
            {
                throw new InvalidOperationException("Simulated delivery failure");
            }

            Messages.Add((email, resetUrl));
            return Task.CompletedTask;
        }
    }
}
