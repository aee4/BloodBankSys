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

namespace BloodLink.Infrastructure.Services.Facilities;

public sealed class FacilityService(
    BloodLinkDbContext dbContext,
    ICurrentUserService currentUser,
    IPasswordHasher<ApplicationUser>? passwordHasher = null) : IFacilityService
{
    private readonly IPasswordHasher<ApplicationUser> passwordHasher = passwordHasher ?? new PasswordHasher<ApplicationUser>();

    public async Task<FacilityDto> RegisterFacilityAsync(RegisterFacilityRequest request, CancellationToken cancellationToken = default)
    {
        ValidateFacilityRegistration(request);

        var registrationNumber = request.RegistrationNumber.Trim();
        var facilityName = request.Name.Trim();
        var adminEmail = request.AdminEmail.Trim();
        var normalizedAdminEmail = Normalize(adminEmail);

        if (await dbContext.Facilities.AnyAsync(
                facility => facility.RegistrationNumber == registrationNumber || facility.Name == facilityName,
                cancellationToken))
        {
            throw new InvalidOperationException("A facility with the same name or registration number already exists.");
        }

        if (await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedAdminEmail, cancellationToken))
        {
            throw new InvalidOperationException("A user with the same email already exists.");
        }

        var adminRoleId = await GetRoleIdAsync(RoleNames.FacilityAdmin, cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var facilityId = Guid.NewGuid();
        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = request.AdminFirstName.Trim(),
            LastName = request.AdminLastName.Trim(),
            UserName = adminEmail,
            NormalizedUserName = normalizedAdminEmail,
            Email = adminEmail,
            NormalizedEmail = normalizedAdminEmail,
            EmailConfirmed = true,
            PhoneNumber = TrimToNull(request.AdminPhoneNumber),
            FacilityId = facilityId,
            IsActive = false,
            MustChangePassword = true,
            CreatedAtUtc = nowUtc,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, CreateTemporaryPassword());

        var facility = new Facility
        {
            Id = facilityId,
            Name = facilityName,
            FacilityType = request.FacilityType,
            RegistrationNumber = registrationNumber,
            Region = request.Region.Trim(),
            City = request.City.Trim(),
            Address = request.Address.Trim(),
            ContactEmail = request.ContactEmail.Trim(),
            ContactPhone = request.ContactPhone.Trim(),
            Status = FacilityStatus.Pending,
            CreatedByUserId = admin.Id,
            CreatedAtUtc = nowUtc
        };

        dbContext.Facilities.Add(facility);
        dbContext.Users.Add(admin);
        dbContext.UserRoles.Add(new IdentityUserRole<string> { UserId = admin.Id, RoleId = adminRoleId });
        AddAudit("FacilityRegistered", nameof(Facility), facility.Id, facility.Id, "Facility registration submitted.", nowUtc, actorUserId: admin.Id);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(facility);
    }

    public async Task<FacilityDto?> GetFacilityAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        ServiceGuards.RequireAuthenticatedActiveUser(currentUser);

        if (!currentUser.IsInRole(RoleNames.SystemAdmin) && !currentUser.BelongsToFacility(facilityId))
        {
            throw new UnauthorizedAccessException("You are not authorized to view this facility.");
        }

        var facility = await dbContext.Facilities.AsNoTracking().SingleOrDefaultAsync(item => item.Id == facilityId, cancellationToken);
        return facility is null ? null : ToDto(facility);
    }

    public async Task UpdateOwnFacilityAsync(UpdateFacilityRequest request, CancellationToken cancellationToken = default)
    {
        ValidateUpdate(request);
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        var facility = await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        facility.Address = request.Address.Trim();
        facility.ContactEmail = request.ContactEmail.Trim();
        facility.ContactPhone = request.ContactPhone.Trim();
        AddAudit("FacilityUpdated", nameof(Facility), facility.Id, facility.Id, "Facility profile updated.", DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FacilityDto>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        ServiceGuards.RequireSystemAdmin(currentUser);

        return await dbContext.Facilities
            .AsNoTracking()
            .Where(facility => facility.Status == FacilityStatus.Pending)
            .OrderBy(facility => facility.CreatedAtUtc)
            .Select(facility => new FacilityDto(
                facility.Id,
                facility.Name,
                facility.FacilityType,
                facility.RegistrationNumber,
                facility.Region,
                facility.City,
                facility.Status))
            .ToListAsync(cancellationToken);
    }

    public Task ApproveAsync(FacilityDecisionRequest request, CancellationToken cancellationToken = default) =>
        ApplySystemDecisionAsync(request, FacilityStatus.Pending, FacilityStatus.Approved, requiresReason: false, cancellationToken);

    public Task RejectAsync(FacilityDecisionRequest request, CancellationToken cancellationToken = default) =>
        ApplySystemDecisionAsync(request, FacilityStatus.Pending, FacilityStatus.Rejected, requiresReason: true, cancellationToken);

    public Task SuspendAsync(FacilityDecisionRequest request, CancellationToken cancellationToken = default) =>
        ApplySystemDecisionAsync(request, FacilityStatus.Approved, FacilityStatus.Suspended, requiresReason: true, cancellationToken);

    public Task RestoreAsync(FacilityDecisionRequest request, CancellationToken cancellationToken = default) =>
        ApplySystemDecisionAsync(request, FacilityStatus.Suspended, FacilityStatus.Approved, requiresReason: false, cancellationToken);

    private async Task ApplySystemDecisionAsync(
        FacilityDecisionRequest request,
        FacilityStatus expectedStatus,
        FacilityStatus nextStatus,
        bool requiresReason,
        CancellationToken cancellationToken)
    {
        ServiceGuards.RequireSystemAdmin(currentUser);

        if (requiresReason && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A reason is required.");
        }

        WorkflowValidation.EnsureSafeNote(request.Reason);

        var facility = await dbContext.Facilities.SingleOrDefaultAsync(item => item.Id == request.FacilityId, cancellationToken)
            ?? throw new InvalidOperationException("The facility was not found.");

        if (facility.Status != expectedStatus)
        {
            throw new InvalidOperationException($"Facility must be {expectedStatus} before it can be {nextStatus}.");
        }

        var nowUtc = DateTime.UtcNow;
        facility.Status = nextStatus;
        facility.RejectionReason = nextStatus == FacilityStatus.Rejected ? request.Reason!.Trim() : null;

        if (nextStatus == FacilityStatus.Approved)
        {
            facility.ApprovedByUserId = currentUser.UserId;
            facility.ApprovedAtUtc = nowUtc;
            await ActivateFacilityAdminsAsync(facility.Id, cancellationToken);
        }

        if (nextStatus == FacilityStatus.Rejected)
        {
            await SetFacilityUsersActiveAsync(facility.Id, isActive: false, cancellationToken);
        }

        AddFacilityDecisionNotifications(facility.Id, nextStatus, nowUtc);
        AddAudit(
            $"Facility{nextStatus}",
            nameof(Facility),
            facility.Id,
            facility.Id,
            $"Facility status changed to {nextStatus}.",
            nowUtc);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ActivateFacilityAdminsAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        var adminRoleId = await GetRoleIdAsync(RoleNames.FacilityAdmin, cancellationToken);
        var adminUserIds = await dbContext.UserRoles
            .Where(userRole => userRole.RoleId == adminRoleId)
            .Select(userRole => userRole.UserId)
            .ToListAsync(cancellationToken);

        var admins = await dbContext.Users
            .Where(user => user.FacilityId == facilityId && adminUserIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        foreach (var admin in admins)
        {
            admin.IsActive = true;
        }
    }

    private async Task SetFacilityUsersActiveAsync(Guid facilityId, bool isActive, CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.Where(user => user.FacilityId == facilityId).ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            user.IsActive = isActive;
        }
    }

    private void AddFacilityDecisionNotifications(Guid facilityId, FacilityStatus status, DateTime nowUtc)
    {
        var recipients = dbContext.Users
            .Where(user => user.FacilityId == facilityId)
            .Select(user => user.Id)
            .ToList();

        WorkflowNotifications.AddForUsers(
            dbContext,
            recipients,
            NotificationType.FacilityDecision,
            "Facility decision updated",
            $"Your facility status is now {status}.",
            nameof(Facility),
            facilityId,
            nowUtc);
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

    private void AddAudit(
        string action,
        string entityType,
        Guid entityId,
        Guid facilityId,
        string summary,
        DateTime nowUtc,
        string? actorUserId = null)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId ?? currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            FacilityId = facilityId,
            Summary = summary,
            CreatedAtUtc = nowUtc
        });
    }

    private static void ValidateFacilityRegistration(RegisterFacilityRequest request)
    {
        RequireText(request.Name, nameof(request.Name));
        WorkflowValidation.EnsureCanonicalEnum(request.FacilityType, nameof(request.FacilityType));
        RequireText(request.RegistrationNumber, nameof(request.RegistrationNumber));
        RequireText(request.Region, nameof(request.Region));
        RequireText(request.City, nameof(request.City));
        RequireText(request.Address, nameof(request.Address));
        RequireEmail(request.ContactEmail, nameof(request.ContactEmail));
        RequireText(request.ContactPhone, nameof(request.ContactPhone));
        RequireText(request.AdminFirstName, nameof(request.AdminFirstName));
        RequireText(request.AdminLastName, nameof(request.AdminLastName));
        RequireEmail(request.AdminEmail, nameof(request.AdminEmail));
        RequireText(request.AdminPhoneNumber, nameof(request.AdminPhoneNumber));
    }

    private static void ValidateUpdate(UpdateFacilityRequest request)
    {
        RequireText(request.Address, nameof(request.Address));
        RequireEmail(request.ContactEmail, nameof(request.ContactEmail));
        RequireText(request.ContactPhone, nameof(request.ContactPhone));
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

    private static FacilityDto ToDto(Facility facility) =>
        new(
            facility.Id,
            facility.Name,
            facility.FacilityType,
            facility.RegistrationNumber,
            facility.Region,
            facility.City,
            facility.Status);

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CreateTemporaryPassword() => $"BloodLink{Guid.NewGuid():N}a1";
}
