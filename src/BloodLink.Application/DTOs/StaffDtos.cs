using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

public sealed record CreateStaffRequest(string FirstName, string LastName, string Email, string? PhoneNumber);
public sealed record StaffDto(
    string UserId,
    Guid FacilityId,
    string FullName,
    string Email,
    StaffStatus Status,
    DateTime CreatedAtUtc,
    DateTime? DeactivatedAtUtc,
    string? StatusReason);
public sealed record ChangeStaffStatusRequest(string UserId, string Reason);
public sealed record StaffCredentialDeliveryResult(bool Delivered);
