using BloodLink.Domain.Common;
using BloodLink.Domain.Enums;

namespace BloodLink.Domain.Entities;

public sealed class BloodRequest : Entity
{
    public Guid BloodNeedId { get; set; }
    public Guid RequestingFacilityId { get; set; }
    public Guid SourceFacilityId { get; set; }
    public BloodType BloodType { get; set; }
    public int UnitsRequested { get; set; }
    public int? UnitsAccepted { get; set; }
    public BloodRequestStatus Status { get; set; } = BloodRequestStatus.Sent;
    public string? RequestNote { get; set; }
    public string? ResponseNote { get; set; }
    public string RequestedByAdminId { get; set; } = string.Empty;
    public string? RespondedByAdminId { get; set; }
    public string? FulfilledByAdminId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
    public DateTime? FulfilledAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
