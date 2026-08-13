namespace BloodLink.Application.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }
    Guid? FacilityId { get; }
    bool IsActive { get; }
    bool IsInRole(string roleName);
    bool BelongsToFacility(Guid facilityId);
}
