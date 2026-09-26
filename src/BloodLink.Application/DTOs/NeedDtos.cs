using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

public sealed record CreateBloodNeedRequest(BloodType BloodType, int UnitsNeeded, UrgencyLevel Urgency, DateTime NeededByUtc, string? Note);
public sealed record BloodNeedDto(Guid Id, Guid FacilityId, BloodType BloodType, int UnitsNeeded, UrgencyLevel Urgency, BloodNeedStatus Status, DateTime CreatedAtUtc);
public sealed record NeedDecisionRequest(Guid BloodNeedId, string? Reason);
public sealed record BloodNeedDetailDto(
    Guid Id, Guid FacilityId, string FacilityName, BloodType BloodType, int UnitsNeeded, UrgencyLevel Urgency,
    BloodNeedStatus Status, DateTime NeededByUtc, string? Note, string? DecisionReason, string CreatorDisplayName,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, int? InventoryTotalUnits, int? InventoryReservedUnits, int? InventoryAvailableUnits);
public sealed record BloodNeedTimelineItemDto(
    BloodNeedStatus? FromStatus, BloodNeedStatus ToStatus, string ActorDisplayName, string? Note, DateTime ChangedAtUtc);
