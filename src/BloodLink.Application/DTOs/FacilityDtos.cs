using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

public sealed record RegisterFacilityRequest(
    string Name,
    FacilityType FacilityType,
    string RegistrationNumber,
    string Region,
    string City,
    string Address,
    string ContactEmail,
    string ContactPhone,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string AdminPhoneNumber);

public sealed record FacilityDto(
    Guid Id,
    string Name,
    FacilityType FacilityType,
    string RegistrationNumber,
    string Region,
    string City,
    string Address,
    string ContactEmail,
    string ContactPhone,
    FacilityStatus Status,
    string? RejectionReason,
    DateTime CreatedAtUtc,
    DateTime? ApprovedAtUtc);

public sealed record FacilityDecisionRequest(Guid FacilityId, string? Reason);
public sealed record UpdateFacilityRequest(string Address, string ContactEmail, string ContactPhone);
public sealed record FacilityQueryRequest(FacilityStatus? Status);
