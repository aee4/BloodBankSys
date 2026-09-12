using System.Security.Claims;
using BloodLink.Application.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Http;

namespace BloodLink.Infrastructure.Identity;

public sealed class CurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authenticationStateProvider,
    AccountAccessService access) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => GetPrincipal();

    public string? UserId => IsAuthenticated
        ? Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var principal = Principal;
            var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (principal?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(userId))
            {
                return Array.Empty<string>();
            }

            var account = access.Find(userId);
            return access.MatchesSession(principal, account) && account?.CanOperate == true
                ? account.IsSystemAdmin ? new[] { BloodLink.Application.Contracts.RoleNames.SystemAdmin } : account.Roles
                : Array.Empty<string>();
        }
    }

    public Guid? FacilityId => GetOperationalAccount() is { IsSystemAdmin: false } account ? account.User.FacilityId : null;

    // The synchronous contract is used by backend guards: fail closed for restricted/stale sessions.
    public bool IsActive => GetOperationalAccount() is not null;

    public bool IsInRole(string roleName) =>
        IsAuthenticated
        && !string.IsNullOrWhiteSpace(roleName)
        && Roles.Contains(roleName, StringComparer.Ordinal);

    public bool BelongsToFacility(Guid facilityId) =>
        IsAuthenticated && FacilityId == facilityId;

    private AccountAccess? GetOperationalAccount()
    {
        var principal = Principal;
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var account = access.Find(userId);
        return principal is not null && access.MatchesSession(principal, account) && account?.CanOperate == true
            ? account : null;
    }

    private ClaimsPrincipal? GetPrincipal()
    {
        try
        {
            var authenticationStateTask = authenticationStateProvider.GetAuthenticationStateAsync();

            // Never block a circuit thread: read the result only when already completed successfully.
            return authenticationStateTask.IsCompletedSuccessfully
                ? authenticationStateTask.GetAwaiter().GetResult().User
                : null;
        }
        catch (InvalidOperationException) when (
            authenticationStateProvider is ServerAuthenticationStateProvider)
        {
            return httpContextAccessor.HttpContext?.User;
        }
    }
}
