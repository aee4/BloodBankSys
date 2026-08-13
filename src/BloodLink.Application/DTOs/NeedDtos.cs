using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

public sealed record CreateBloodNeedRequest(BloodType BloodType, int UnitsNeeded, UrgencyLevel Urgency, DateTime NeededByUtc, string? Note);
public sealed record BloodNeedDto(Guid Id, Guid FacilityId, BloodType BloodType, int UnitsNeeded, UrgencyLevel Urgency, BloodNeedStatus Status, DateTime CreatedAtUtc);
public sealed record NeedDecisionRequest(Guid BloodNeedId, string? Reason);
