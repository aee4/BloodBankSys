using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

public sealed record NotificationDto(Guid Id, NotificationType NotificationType, string Title, string Message, bool IsRead, DateTime CreatedAtUtc);
public sealed record UnreadNotificationCountDto(int Count);
