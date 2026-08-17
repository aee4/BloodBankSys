using BloodLink.Application.Contracts;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace BloodLink.Infrastructure.Identity;

/// <summary>
/// Provides access to the current authenticated user's identity information.
/// Owned by Authentication & Security Developer.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, UserManager<ApplicationUser> userManager)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return Array.Empty<string>();

            var roles = new List<string>();
            if (user.IsInRole(RoleNames.SystemAdmin)) roles.Add(RoleNames.SystemAdmin);
            if (user.IsInRole(RoleNames.FacilityAdmin)) roles.Add(RoleNames.FacilityAdmin);
            if (user.IsInRole(RoleNames.FacilityStaff)) roles.Add(RoleNames.FacilityStaff);

            return roles.AsReadOnly();
        }
    }

    public Guid? FacilityId
    {
        get
        {
            var facilityIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("facility_id")?.Value;
            if (string.IsNullOrEmpty(facilityIdClaim) || !Guid.TryParse(facilityIdClaim, out var facilityId))
            {
                return null;
            }
            return facilityId;
        }
    }

    public bool IsActive
    {
        get
        {
            var isActiveClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("is_active")?.Value;
            return isActiveClaim == "true";
        }
    }

    public bool IsInRole(string roleName)
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole(roleName) ?? false;
    }

    public bool BelongsToFacility(Guid facilityId)
    {
        return FacilityId == facilityId;
    }
}
