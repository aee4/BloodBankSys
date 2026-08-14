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
    public async Task<SystemDashboardDto> GetSystemAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        ServiceGuards.RequireSystemAdmin(currentUser);

        var pendingFacilities = await dbContext.Facilities
            .CountAsync(facility => facility.Status == FacilityStatus.Pending, cancellationToken);
        var approvedFacilities = await dbContext.Facilities
            .CountAsync(facility => facility.Status == FacilityStatus.Approved, cancellationToken);
        var suspendedFacilities = await dbContext.Facilities
            .CountAsync(facility => facility.Status == FacilityStatus.Suspended, cancellationToken);

        return new SystemDashboardDto(pendingFacilities, approvedFacilities, suspendedFacilities);
    }

    public async Task<FacilityAdminDashboardDto> GetFacilityAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        var openNeeds = await dbContext.BloodNeeds.CountAsync(
            need => need.FacilityId == facilityId
                && (need.Status == BloodNeedStatus.PendingReview || need.Status == BloodNeedStatus.Searching),
            cancellationToken);
        var sentRequests = await dbContext.BloodRequests.CountAsync(
            request => request.RequestingFacilityId == facilityId && request.Status != BloodRequestStatus.Fulfilled,
            cancellationToken);
        var receivedRequests = await dbContext.BloodRequests.CountAsync(
            request => request.SourceFacilityId == facilityId && request.Status != BloodRequestStatus.Fulfilled,
            cancellationToken);
        var lowStockItems = await dbContext.BloodInventory.CountAsync(
            item => item.FacilityId == facilityId && item.TotalUnits - item.ReservedUnits <= item.LowStockThreshold,
            cancellationToken);

        return new FacilityAdminDashboardDto(openNeeds, sentRequests, receivedRequests, lowStockItems);
    }

    public async Task<StaffDashboardDto> GetFacilityStaffDashboardAsync(CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityStaff);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        var myOpenNeeds = await dbContext.BloodNeeds.CountAsync(
            need => need.FacilityId == facilityId
                && need.RequestedByUserId == userId
                && (need.Status == BloodNeedStatus.PendingReview || need.Status == BloodNeedStatus.Searching),
            cancellationToken);
        var unreadNotifications = await dbContext.Notifications.CountAsync(
            notification => notification.RecipientUserId == userId && !notification.IsRead,
            cancellationToken);

        return new StaffDashboardDto(myOpenNeeds, unreadNotifications);
    }
}
