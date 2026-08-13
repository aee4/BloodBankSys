using BloodLink.Domain.Common;

namespace BloodLink.Domain.Entities;

public sealed class AuditLog : Entity
{
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public Guid? FacilityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
