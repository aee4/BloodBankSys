using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Services.Dashboard;

public sealed class DashboardService(
    BloodLinkDbContext dbContext,
    ICurrentUserService currentUser) : IDashboardService
{
    private static readonly BloodRequestStatus[] ActiveRequestStatuses =
    [BloodRequestStatus.Sent, BloodRequestStatus.Accepted];

    public async Task<SystemDashboardDto> GetSystemAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        ServiceGuards.RequireSystemAdmin(currentUser);
        var pending = await dbContext.Facilities.AsNoTracking()
            .Where(facility => facility.Status == FacilityStatus.Pending)
            .OrderBy(facility => facility.CreatedAtUtc).ThenBy(facility => facility.Id)
            .Select(facility => new DashboardFacilityItemDto(facility.Id, facility.Name, facility.City, facility.Region, facility.CreatedAtUtc))
            .Take(6).ToListAsync(cancellationToken);
        var activity = await dbContext.AuditLogs.AsNoTracking()
            .OrderByDescending(log => log.CreatedAtUtc).ThenBy(log => log.Id)
            .Select(log => new DashboardActivityDto(log.Action, log.Summary, log.CreatedAtUtc, log.EntityType, log.EntityId))
            .Take(8).ToListAsync(cancellationToken);

        return new SystemDashboardDto(
            await dbContext.Facilities.CountAsync(facility => facility.Status == FacilityStatus.Pending, cancellationToken),
            await dbContext.Facilities.CountAsync(facility => facility.Status == FacilityStatus.Approved, cancellationToken),
            await dbContext.Facilities.CountAsync(facility => facility.Status == FacilityStatus.Suspended, cancellationToken))
        {
            ActiveRequests = await dbContext.BloodRequests.CountAsync(request => ActiveRequestStatuses.Contains(request.Status), cancellationToken),
            PendingReviews = pending,
            RecentActivity = activity
        };
    }

    public async Task<FacilityAdminDashboardDto> GetFacilityAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var openNeeds = dbContext.BloodNeeds.AsNoTracking().Where(need =>
            need.FacilityId == facilityId && (need.Status == BloodNeedStatus.PendingReview || need.Status == BloodNeedStatus.Searching));
        var inventory = dbContext.BloodInventory.AsNoTracking().Where(item => item.FacilityId == facilityId);
        var pending = await openNeeds.OrderByDescending(need => need.Urgency)
            .ThenBy(need => need.CreatedAtUtc).ThenBy(need => need.Id)
            .Select(need => new DashboardNeedItemDto(need.Id, need.BloodType, need.UnitsNeeded, need.Urgency, need.Status, need.CreatedAtUtc))
            .Take(8).ToListAsync(cancellationToken);
        var activity = await dbContext.AuditLogs.AsNoTracking().Where(log => log.FacilityId == facilityId)
            .OrderByDescending(log => log.CreatedAtUtc).ThenBy(log => log.Id)
            .Select(log => new DashboardActivityDto(log.Action, log.Summary, log.CreatedAtUtc, log.EntityType, log.EntityId))
            .Take(8).ToListAsync(cancellationToken);

        return new FacilityAdminDashboardDto(
            await openNeeds.CountAsync(cancellationToken),
            await dbContext.BloodRequests.CountAsync(request => request.RequestingFacilityId == facilityId && ActiveRequestStatuses.Contains(request.Status), cancellationToken),
            await dbContext.BloodRequests.CountAsync(request => request.SourceFacilityId == facilityId && ActiveRequestStatuses.Contains(request.Status), cancellationToken),
            await inventory.CountAsync(item => item.TotalUnits - item.ReservedUnits <= item.LowStockThreshold, cancellationToken))
        {
            TotalInventoryUnits = await inventory.SumAsync(item => (long)item.TotalUnits, cancellationToken),
            AvailableInventoryUnits = await inventory.SumAsync(item => (long)(item.TotalUnits - item.ReservedUnits), cancellationToken),
            UnreadNotifications = await dbContext.Notifications.CountAsync(item => item.RecipientUserId == userId && !item.IsRead, cancellationToken),
            PendingNeeds = pending,
            RecentActivity = activity
        };
    }

    public async Task<StaffDashboardDto> GetFacilityStaffDashboardAsync(CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityStaff);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);
        var mine = dbContext.BloodNeeds.AsNoTracking().Where(need =>
            need.FacilityId == facilityId && need.RequestedByUserId == userId);
        var recentNeeds = await mine.OrderByDescending(need => need.CreatedAtUtc).ThenBy(need => need.Id)
            .Select(need => new DashboardNeedItemDto(need.Id, need.BloodType, need.UnitsNeeded, need.Urgency, need.Status, need.CreatedAtUtc))
            .Take(6).ToListAsync(cancellationToken);
        var pending = await mine.CountAsync(need => need.Status == BloodNeedStatus.PendingReview, cancellationToken);
        var searching = await mine.CountAsync(need => need.Status == BloodNeedStatus.Searching, cancellationToken);
        var open = pending + searching;
        var unread = await dbContext.Notifications.CountAsync(
            notification => notification.RecipientUserId == userId && !notification.IsRead, cancellationToken);

        return new StaffDashboardDto(open, unread)
        {
            PendingReviewNeeds = pending,
            SearchingNeeds = searching,
            RecentNeeds = recentNeeds
        };
    }
}
