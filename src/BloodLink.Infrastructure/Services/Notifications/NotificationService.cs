using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
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

        return await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == userId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Select(notification => new NotificationDto(
                notification.Id,
                notification.NotificationType,
                notification.Title,
                notification.Message,
                notification.IsRead,
                notification.CreatedAtUtc))
            .ToListAsync(cancellationToken);
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
