using System.Security.Claims;
using BloodLink.Application.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace BloodLink.Infrastructure.Identity;

public sealed class OperationalUserHandler(AccountAccessService access)
    : AuthorizationHandler<OperationalUserRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationalUserRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        // Use the principal being authorized, and fresh data for every decision (including circuits).
        var account = await access.FindAsync(userId);
        if (!access.MatchesSession(context.User, account) || account?.CanOperate != true)
        {
            return;
        }

        // Role grants require a refreshed principal; revocations take effect immediately.
        if (!requirement.AllowedRoles.Any(role => context.User.IsInRole(role) && account.Roles.Contains(role)))
        {
            return;
        }

        if (requirement.RequiresApprovedFacility
            && (account.IsSystemAdmin || !account.HasApprovedFacility))
        {
            return;
        }

        context.Succeed(requirement);
    }
}
