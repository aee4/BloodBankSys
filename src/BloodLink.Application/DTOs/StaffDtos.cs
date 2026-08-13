using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

public sealed record CreateStaffRequest(string FirstName, string LastName, string Email, string PhoneNumber);
public sealed record StaffDto(string UserId, Guid FacilityId, string FullName, string Email, StaffStatus Status);
public sealed record ChangeStaffStatusRequest(string UserId, string Reason);
