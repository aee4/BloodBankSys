using BloodLink.Domain.Common;
using BloodLink.Domain.Enums;

namespace BloodLink.Domain.Entities;

public sealed class Facility : Entity
{
    public string Name { get; set; } = string.Empty;
    public FacilityType FacilityType { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public FacilityStatus Status { get; set; } = FacilityStatus.Pending;
    public string? RejectionReason { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? ApprovedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
}
