using BloodLink.Domain.Common;
using BloodLink.Domain.Enums;

namespace BloodLink.Domain.Entities;

public sealed class BloodRequestStatusHistory : Entity
{
    public Guid BloodRequestId { get; set; }
    public BloodRequestStatus? FromStatus { get; set; }
    public BloodRequestStatus ToStatus { get; set; }
    public string? Note { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
