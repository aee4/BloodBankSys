using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Application.Contracts;
using BloodLink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Services.Common;

internal static class WorkflowNotifications
{
    public static async Task AddForActiveFacilityAdminsAsync(
        BloodLinkDbContext dbContext,
        Guid facilityId,
        NotificationType notificationType,
        string title,
        string message,
        string relatedEntityType,
        Guid relatedEntityId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var adminRoleId = await dbContext.Roles
            .Where(role => role.Name == RoleNames.FacilityAdmin)
            .Select(role => role.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(adminRoleId))
        {
            return;
        }

        var recipientIds = await (
                from user in dbContext.Users
                join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
                where user.FacilityId == facilityId
                    && user.IsActive
                    && userRole.RoleId == adminRoleId
                select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        AddForUsers(dbContext, recipientIds, notificationType, title, message, relatedEntityType, relatedEntityId, nowUtc);
    }

    public static void AddForUsers(
        BloodLinkDbContext dbContext,
        IEnumerable<string> recipientUserIds,
        NotificationType notificationType,
        string title,
        string message,
        string relatedEntityType,
        Guid relatedEntityId,
        DateTime nowUtc)
    {
        foreach (var recipientUserId in recipientUserIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
        {
            dbContext.Notifications.Add(new Notification
            {
                RecipientUserId = recipientUserId,
                NotificationType = notificationType,
                Title = title,
                Message = message,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                IsRead = false,
                CreatedAtUtc = nowUtc
            });
        }
    }
}
