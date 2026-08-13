using BloodLink.Domain.Common;
using BloodLink.Domain.Enums;

namespace BloodLink.Domain.Entities;

public sealed class Notification : Entity
{
    public string RecipientUserId { get; set; } = string.Empty;
    public NotificationType NotificationType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}
