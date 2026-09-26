using BloodLink.Domain.Common;
using BloodLink.Domain.Enums;

namespace BloodLink.Domain.Entities;

public sealed class BloodNeedStatusHistory : Entity
{
    public Guid BloodNeedId { get; set; }
    public BloodNeedStatus? FromStatus { get; set; }
    public BloodNeedStatus ToStatus { get; set; }
    public string? Note { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
