using BloodLink.Application.DTOs;

namespace BloodLink.Application.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListMineAsync(CancellationToken cancellationToken = default);
    Task<UnreadNotificationCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
}
