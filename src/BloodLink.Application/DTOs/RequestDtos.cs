using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

public sealed record CreateBloodRequestRequest(Guid BloodNeedId, Guid SourceFacilityId, int UnitsRequested, string? RequestNote);
public sealed record BloodRequestDto(
    Guid Id, Guid BloodNeedId, Guid RequestingFacilityId, string RequestingFacilityName,
    Guid SourceFacilityId, string SourceFacilityName, BloodType BloodType, int UnitsRequested,
    int? UnitsAccepted, UrgencyLevel Priority, BloodRequestStatus Status, string? RequestNote,
    string? ResponseNote, DateTime CreatedAtUtc, DateTime? RespondedAtUtc, DateTime? FulfilledAtUtc);
public sealed record RequestResponseRequest(Guid BloodRequestId, int? UnitsAccepted, string? ResponseNote);
public sealed record FulfilRequestRequest(Guid BloodRequestId, string? Note);
public sealed record RequestTimelineItemDto(BloodRequestStatus? FromStatus, BloodRequestStatus ToStatus, string ActorDisplayName, string? Note, DateTime ChangedAtUtc);
