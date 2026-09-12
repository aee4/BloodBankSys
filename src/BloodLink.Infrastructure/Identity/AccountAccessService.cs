using System.Security.Claims;
using BloodLink.Application.Contracts;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BloodLink.Infrastructure.Identity;

// One source of account eligibility for sign-in, cookies, circuits, policies and service guards.
public sealed class AccountAccessService(
    IDbContextFactory<BloodLinkDbContext> contextFactory,
    IOptions<IdentityOptions> options)
{
    public AccountAccess? Find(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        using var db = contextFactory.CreateDbContext();
        var user = db.Users.AsNoTracking().SingleOrDefault(u => u.Id == userId);
        if (user is null) return null;
        var roles = (from membership in db.UserRoles
                     join role in db.Roles on membership.RoleId equals role.Id
                     where membership.UserId == userId
                     select role.Name).ToArray();
        var approved = user.FacilityId is { } id && db.Facilities.AsNoTracking()
            .Any(f => f.Id == id && f.Status == FacilityStatus.Approved);
        var staff = roles.Contains(RoleNames.FacilityStaff) && !roles.Contains(RoleNames.SystemAdmin)
            ? db.FacilityStaff.AsNoTracking().Where(s => s.UserId == userId).Take(2).ToArray()
            : [];
        return new AccountAccess(user, roles.Select(role => role ?? string.Empty).ToArray(), approved,
            MatchingStaffStatus(user, staff));
    }

    public async Task<AccountAccess?> FindAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return null;
        var roles = await (from membership in db.UserRoles
                           join role in db.Roles on membership.RoleId equals role.Id
                           where membership.UserId == userId
                           select role.Name).ToArrayAsync(cancellationToken);
        var approved = user.FacilityId is { } id && await db.Facilities.AsNoTracking()
            .AnyAsync(f => f.Id == id && f.Status == FacilityStatus.Approved, cancellationToken);
        var staff = roles.Contains(RoleNames.FacilityStaff) && !roles.Contains(RoleNames.SystemAdmin)
            ? await db.FacilityStaff.AsNoTracking().Where(s => s.UserId == userId).Take(2).ToArrayAsync(cancellationToken)
            : [];
        return new AccountAccess(user, roles.Select(role => role ?? string.Empty).ToArray(), approved,
            MatchingStaffStatus(user, staff));
    }

    private static StaffStatus? MatchingStaffStatus(ApplicationUser user, FacilityStaff[] staff) =>
        staff.Length == 1 && staff[0].FacilityId == user.FacilityId ? staff[0].Status : null;

    public bool MatchesSession(ClaimsPrincipal principal, AccountAccess? account) =>
        principal.Identity?.IsAuthenticated == true
        && account?.CanSignIn == true
        && principal.FindFirstValue(options.Value.ClaimsIdentity.UserIdClaimType) == account.User.Id
        && !string.IsNullOrEmpty(account.User.SecurityStamp)
        && principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType) == account.User.SecurityStamp
        && account.Roles.ToHashSet(StringComparer.Ordinal).SetEquals(
            principal.FindAll(options.Value.ClaimsIdentity.RoleClaimType).Select(c => c.Value));

    public async Task<bool> ValidateSessionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
        MatchesSession(principal, await FindAsync(
            principal.FindFirstValue(options.Value.ClaimsIdentity.UserIdClaimType), cancellationToken));
}

public sealed record AccountAccess(ApplicationUser User, string[] Roles, bool HasApprovedFacility, StaffStatus? StaffLifecycleStatus)
{
    public bool IsSystemAdmin => Roles.Contains(RoleNames.SystemAdmin, StringComparer.Ordinal);
    public bool IsStaffOnly => !IsSystemAdmin && !Roles.Contains(RoleNames.FacilityAdmin, StringComparer.Ordinal)
        && Roles.Contains(RoleNames.FacilityStaff, StringComparer.Ordinal);
    public bool HasActiveStaffMembership => StaffLifecycleStatus == StaffStatus.Active;
    public bool CanSignIn => User.IsActive && Roles.Length > 0
        && Roles.All(role => role is RoleNames.SystemAdmin or RoleNames.FacilityAdmin or RoleNames.FacilityStaff)
        && (IsSystemAdmin || (User.FacilityId is { } id && id != Guid.Empty && HasApprovedFacility))
        && (!IsStaffOnly || StaffLifecycleStatus is StaffStatus.Active or StaffStatus.PendingActivation);
    public bool CanOperate => CanSignIn && !User.MustChangePassword && (!IsStaffOnly || HasActiveStaffMembership);
    public string[] OperationalRoles => !CanOperate ? [] : IsSystemAdmin ? [RoleNames.SystemAdmin]
        : Roles.Where(role => role != RoleNames.FacilityStaff || HasActiveStaffMembership).ToArray();
}
