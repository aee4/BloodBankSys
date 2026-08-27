using System.Net.Mail;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using BloodLink.Infrastructure.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Services.Staff;

public sealed class StaffService(
    BloodLinkDbContext dbContext,
    ICurrentUserService currentUser,
    IPasswordHasher<ApplicationUser>? passwordHasher = null) : IStaffService
{
    private readonly IPasswordHasher<ApplicationUser> passwordHasher = passwordHasher ?? new PasswordHasher<ApplicationUser>();

    public async Task<IReadOnlyList<StaffDto>> ListOwnFacilityStaffAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        return await (
                from staff in dbContext.FacilityStaff.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on staff.UserId equals user.Id
                where staff.FacilityId == facilityId
                orderby user.LastName, user.FirstName, user.Email
                select new StaffDto(
                    user.Id,
                    staff.FacilityId,
                    (user.FirstName + " " + user.LastName).Trim(),
                    user.Email ?? string.Empty,
                    staff.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default)
    {
        ValidateCreateStaff(request);
        var adminUserId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        var email = request.Email.Trim();
        var normalizedEmail = Normalize(email);

        if (await UserEmailExistsAsync(email, normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("A user with the same email already exists.");
        }

        var staffRoleId = await GetRoleIdAsync(RoleNames.FacilityStaff, cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            UserName = email,
            NormalizedUserName = normalizedEmail,
            Email = email,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
            PhoneNumber = TrimToNull(request.PhoneNumber),
            FacilityId = facilityId,
            IsActive = true,
            MustChangePassword = true,
            CreatedAtUtc = nowUtc,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        user.PasswordHash = passwordHasher.HashPassword(user, CreateTemporaryPassword());

        var staff = new FacilityStaff
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            UserId = user.Id,
            Status = StaffStatus.PendingActivation,
            CreatedByAdminId = adminUserId,
            CreatedAtUtc = nowUtc
        };

        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = staffRoleId });
        dbContext.FacilityStaff.Add(staff);
        AddAudit("StaffCreated", nameof(FacilityStaff), staff.Id, facilityId, "Facility staff account created.", nowUtc);
        AddNotification(user.Id, NotificationType.AccountCreated, "Account created", "Your BloodLink staff account has been created.", nameof(FacilityStaff), staff.Id, nowUtc);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(staff, user);
    }

    public Task DeactivateStaffAsync(ChangeStaffStatusRequest request, CancellationToken cancellationToken = default) =>
        ChangeStaffStatusAsync(request, StaffStatus.Inactive, isActive: false, requiresReason: true, cancellationToken);

    public Task ReactivateStaffAsync(ChangeStaffStatusRequest request, CancellationToken cancellationToken = default) =>
        ChangeStaffStatusAsync(request, StaffStatus.Active, isActive: true, requiresReason: false, cancellationToken);

    public async Task ResetTemporaryPasswordAsync(string userId, CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        var (staff, user) = await LoadOwnStaffAsync(facilityId, userId, cancellationToken);

        if (staff.Status == StaffStatus.Inactive || !user.IsActive)
        {
            throw new InvalidOperationException("Inactive staff must be reactivated before resetting credentials.");
        }

        user.PasswordHash = passwordHasher.HashPassword(user, CreateTemporaryPassword());
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.MustChangePassword = true;
        AddAudit("StaffPasswordReset", nameof(FacilityStaff), staff.Id, facilityId, "Temporary password reset for staff account.", DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ChangeStaffStatusAsync(
        ChangeStaffStatusRequest request,
        StaffStatus nextStatus,
        bool isActive,
        bool requiresReason,
        CancellationToken cancellationToken)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        if (requiresReason && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A reason is required.");
        }

        WorkflowValidation.EnsureSafeNote(request.Reason);

        var (staff, user) = await LoadOwnStaffAsync(facilityId, request.UserId, cancellationToken);

        if (currentUser.UserId == user.Id && nextStatus == StaffStatus.Inactive)
        {
            throw new InvalidOperationException("Facility admins cannot deactivate their own account through staff management.");
        }

        if (staff.Status == nextStatus && user.IsActive == isActive)
        {
            return;
        }

        staff.Status = nextStatus;
        staff.StatusReason = TrimToNull(request.Reason);
        staff.DeactivatedAtUtc = nextStatus == StaffStatus.Inactive ? DateTime.UtcNow : null;
        user.IsActive = isActive;
        AddAudit($"Staff{nextStatus}", nameof(FacilityStaff), staff.Id, facilityId, $"Staff account marked {nextStatus}.", DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(FacilityStaff Staff, ApplicationUser User)> LoadOwnStaffAsync(
        Guid facilityId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required.");
        }

        var staff = await dbContext.FacilityStaff.SingleOrDefaultAsync(
                item => item.UserId == userId && item.FacilityId == facilityId,
                cancellationToken)
            ?? throw new InvalidOperationException("The staff member was not found.");

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("The staff user was not found.");

        if (user.FacilityId != facilityId)
        {
            throw new UnauthorizedAccessException("You are not authorized to manage this staff member.");
        }

        return (staff, user);
    }

    private async Task<string> GetRoleIdAsync(string roleName, CancellationToken cancellationToken)
    {
        var normalizedRoleName = Normalize(roleName);
        var roleId = await dbContext.Roles
            .Where(role => role.NormalizedName == normalizedRoleName || role.Name == roleName)
            .Select(role => role.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(roleId)
            ? throw new InvalidOperationException($"{roleName} role is not configured.")
            : roleId;
    }

    private async Task<bool> UserEmailExistsAsync(
        string email,
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        await dbContext.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail || user.Email == email,
            cancellationToken);

    private void AddAudit(string action, string entityType, Guid entityId, Guid facilityId, string summary, DateTime nowUtc)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            FacilityId = facilityId,
            Summary = summary,
            CreatedAtUtc = nowUtc
        });
    }

    private void AddNotification(
        string recipientUserId,
        NotificationType type,
        string title,
        string message,
        string relatedEntityType,
        Guid relatedEntityId,
        DateTime nowUtc)
    {
        dbContext.Notifications.Add(new Notification
        {
            RecipientUserId = recipientUserId,
            NotificationType = type,
            Title = title,
            Message = message,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            IsRead = false,
            CreatedAtUtc = nowUtc
        });
    }

    private static void ValidateCreateStaff(CreateStaffRequest request)
    {
        RequireText(request.FirstName, nameof(request.FirstName));
        RequireText(request.LastName, nameof(request.LastName));
        RequireEmail(request.Email, nameof(request.Email));
        RequireText(request.PhoneNumber, nameof(request.PhoneNumber));
    }

    private static void RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }

    private static void RequireEmail(string value, string fieldName)
    {
        RequireText(value, fieldName);

        try
        {
            _ = new MailAddress(value.Trim());
        }
        catch (FormatException)
        {
            throw new ArgumentException($"{fieldName} must be a valid email address.");
        }
    }

    private static StaffDto ToDto(FacilityStaff staff, ApplicationUser user) =>
        new(staff.UserId, staff.FacilityId, $"{user.FirstName} {user.LastName}".Trim(), user.Email ?? string.Empty, staff.Status);

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CreateTemporaryPassword() => $"BloodLink{Guid.NewGuid():N}a1";
}
