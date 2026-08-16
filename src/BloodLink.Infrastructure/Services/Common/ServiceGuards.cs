using BloodLink.Application.Contracts;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Services.Common;

internal static class ServiceGuards
{
    public static string RequireAuthenticatedActiveUser(ICurrentUserService currentUser)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new UnauthorizedAccessException("You must be signed in to perform this action.");
        }

        if (!currentUser.IsActive)
        {
            throw new UnauthorizedAccessException("Your account is not active.");
        }

        return currentUser.UserId;
    }

    public static Guid RequireFacilityRole(ICurrentUserService currentUser, string roleName)
    {
        RequireAuthenticatedActiveUser(currentUser);

        if (!currentUser.IsInRole(roleName))
        {
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
        }

        if (currentUser.FacilityId is not { } facilityId)
        {
            throw new UnauthorizedAccessException("Your account is not linked to a facility.");
        }

        return facilityId;
    }

    public static void RequireSystemAdmin(ICurrentUserService currentUser)
    {
        RequireAuthenticatedActiveUser(currentUser);

        if (!currentUser.IsInRole(RoleNames.SystemAdmin))
        {
            throw new UnauthorizedAccessException("You are not authorized to view platform dashboard data.");
        }
    }

    public static async Task<Facility> RequireApprovedFacilityAsync(
        BloodLinkDbContext dbContext,
        Guid facilityId,
        CancellationToken cancellationToken)
    {
        var facility = await dbContext.Facilities
            .SingleOrDefaultAsync(item => item.Id == facilityId, cancellationToken);

        if (facility is null)
        {
            throw new InvalidOperationException("The facility was not found.");
        }

        if (facility.Status != FacilityStatus.Approved)
        {
            throw new UnauthorizedAccessException("The facility is not approved for operational workflows.");
        }

        return facility;
    }
}
