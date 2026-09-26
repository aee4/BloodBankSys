using BloodLink.Application.DTOs;
using BloodLink.Application.Contracts;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Services.Notifications;

public sealed class NotificationService(
    BloodLinkDbContext dbContext,
    ICurrentUserService currentUser) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> ListMineAsync(CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var notifications = await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == userId)
            .OrderByDescending(notification => notification.CreatedAtUtc).ThenBy(notification => notification.Id)
            .ToListAsync(cancellationToken);

        var visibleReferences = new HashSet<(string Type, Guid Id)>();
        var relatedIds = notifications.Where(item => item.RelatedEntityId.HasValue)
            .Select(item => (Type: item.RelatedEntityType, Id: item.RelatedEntityId!.Value)).Distinct().ToArray();
        var needIds = relatedIds.Where(item => item.Type == nameof(BloodNeed)).Select(item => item.Id).ToArray();
        if (needIds.Length > 0)
        {
            var needs = dbContext.BloodNeeds.AsNoTracking().Where(need => needIds.Contains(need.Id));
            if (currentUser.IsInRole(RoleNames.FacilityAdmin) && currentUser.FacilityId is { } adminFacility)
            {
                var allowed = await needs.Where(need => need.FacilityId == adminFacility).Select(need => need.Id).ToListAsync(cancellationToken);
                foreach (var id in allowed) visibleReferences.Add((nameof(BloodNeed), id));
            }
            else if (currentUser.IsInRole(RoleNames.FacilityStaff))
            {
                var allowed = await needs.Where(need => need.RequestedByUserId == userId).Select(need => need.Id).ToListAsync(cancellationToken);
                foreach (var id in allowed) visibleReferences.Add((nameof(BloodNeed), id));
            }
        }

        var requestIds = relatedIds.Where(item => item.Type == nameof(BloodRequest)).Select(item => item.Id).ToArray();
        if (requestIds.Length > 0 && currentUser.IsInRole(RoleNames.FacilityAdmin) && currentUser.FacilityId is { } facilityId)
        {
            var allowed = await dbContext.BloodRequests.AsNoTracking()
                .Where(request => requestIds.Contains(request.Id)
                    && (request.RequestingFacilityId == facilityId || request.SourceFacilityId == facilityId))
                .Select(request => request.Id).ToListAsync(cancellationToken);
            foreach (var id in allowed) visibleReferences.Add((nameof(BloodRequest), id));
        }

        var facilityIds = relatedIds.Where(item => item.Type == nameof(Facility)).Select(item => item.Id).ToArray();
        if (facilityIds.Length > 0 && (currentUser.IsInRole(RoleNames.SystemAdmin) || currentUser.FacilityId is not null))
        {
            var allowed = await dbContext.Facilities.AsNoTracking()
                .Where(facility => facilityIds.Contains(facility.Id)
                    && (currentUser.IsInRole(RoleNames.SystemAdmin) || facility.Id == currentUser.FacilityId))
                .Select(facility => facility.Id).ToListAsync(cancellationToken);
            foreach (var id in allowed) visibleReferences.Add((nameof(Facility), id));
        }

        var staffIds = relatedIds.Where(item => item.Type == nameof(FacilityStaff)).Select(item => item.Id).ToArray();
        if (staffIds.Length > 0 && currentUser.FacilityId is { } staffFacility)
        {
            var staff = dbContext.FacilityStaff.AsNoTracking().Where(item => staffIds.Contains(item.Id) && item.FacilityId == staffFacility);
            if (!currentUser.IsInRole(RoleNames.FacilityAdmin))
            {
                staff = staff.Where(item => item.UserId == userId);
            }
            var allowed = await staff.Select(item => item.Id).ToListAsync(cancellationToken);
            foreach (var id in allowed) visibleReferences.Add((nameof(FacilityStaff), id));
        }

        return notifications.Select(notification =>
        {
            var type = notification.RelatedEntityType;
            var id = notification.RelatedEntityId;
            if (type is null || id is null || !visibleReferences.Contains((type, id.Value)))
            {
                type = null;
                id = null;
            }
            return new NotificationDto(notification.Id, notification.NotificationType, notification.Title,
                notification.Message, notification.IsRead, notification.CreatedAtUtc, type, id);
        }).ToList();
    }

    public async Task<UnreadNotificationCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);

        var count = await dbContext.Notifications
            .CountAsync(notification => notification.RecipientUserId == userId && !notification.IsRead, cancellationToken);

        return new UnreadNotificationCountDto(count);
    }

    public async Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);

        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(item => item.Id == notificationId, cancellationToken);

        if (notification is null || notification.RecipientUserId != userId)
        {
            throw new UnauthorizedAccessException("The notification was not found for your account.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var nowUtc = DateTime.UtcNow;

        var notifications = await dbContext.Notifications
            .Where(notification => notification.RecipientUserId == userId && !notification.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
