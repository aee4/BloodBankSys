using BloodLink.Domain.Common;
using BloodLink.Domain.Enums;

namespace BloodLink.Domain.Entities;

public sealed class BloodNeed : Entity
{
    public Guid FacilityId { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public BloodType BloodType { get; set; }
    public int UnitsNeeded { get; set; }
    public UrgencyLevel Urgency { get; set; }
    public DateTime NeededByUtc { get; set; }
    public string? Note { get; set; }
    public BloodNeedStatus Status { get; set; } = BloodNeedStatus.PendingReview;
    public string? DecisionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
