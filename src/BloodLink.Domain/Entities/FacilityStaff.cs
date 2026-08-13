using BloodLink.Domain.Common;
using BloodLink.Domain.Enums;

namespace BloodLink.Domain.Entities;

public sealed class FacilityStaff : Entity
{
    public Guid FacilityId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public StaffStatus Status { get; set; } = StaffStatus.PendingActivation;
    public string CreatedByAdminId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAtUtc { get; set; }
    public string? StatusReason { get; set; }
}
