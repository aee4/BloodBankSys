using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

public sealed record CreateBloodRequestRequest(Guid BloodNeedId, Guid SourceFacilityId, int UnitsRequested, string? RequestNote);
public sealed record BloodRequestDto(Guid Id, Guid BloodNeedId, Guid RequestingFacilityId, Guid SourceFacilityId, BloodType BloodType, int UnitsRequested, int? UnitsAccepted, BloodRequestStatus Status);
public sealed record RequestResponseRequest(Guid BloodRequestId, int? UnitsAccepted, string? ResponseNote);
public sealed record FulfilRequestRequest(Guid BloodRequestId, string? Note);
public sealed record RequestTimelineItemDto(BloodRequestStatus? FromStatus, BloodRequestStatus ToStatus, string? Note, DateTime ChangedAtUtc);
