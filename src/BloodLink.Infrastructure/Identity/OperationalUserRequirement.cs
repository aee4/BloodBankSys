using Microsoft.AspNetCore.Authorization;

namespace BloodLink.Infrastructure.Identity;

public sealed class OperationalUserRequirement(bool requiresApprovedFacility, params string[] allowedRoles)
    : IAuthorizationRequirement
{
    public bool RequiresApprovedFacility { get; } = requiresApprovedFacility;
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.AsReadOnly(allowedRoles);
}
