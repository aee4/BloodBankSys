using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Data;

namespace BloodLink.Infrastructure.Services.NotificationServices;

/// <summary>
/// Service for managing user notifications.
/// Subscribed to events from Inventory, BloodNeed, and BloodRequest services.
/// Owned by Backend Developer 3.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly BloodLinkDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public NotificationService(BloodLinkDbContext context, ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public Task<IReadOnlyList<NotificationDto>> ListMineAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - ListMineAsync");
    }

    public Task<UnreadNotificationCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - GetUnreadCountAsync");
    }

    public Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - MarkReadAsync");
    }

    public Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - MarkAllReadAsync");
    }
}
