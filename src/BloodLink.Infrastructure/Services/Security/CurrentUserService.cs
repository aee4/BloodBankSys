using System.Security.Claims;
using BloodLink.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BloodLink.Infrastructure.Services.Security;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? UserId => GetClaimValue(ClaimTypes.NameIdentifier) ?? GetClaimValue("sub");

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public IReadOnlyCollection<string> Roles => _httpContextAccessor.HttpContext?.User?
        .Claims
        .Where(claim => claim.Type == ClaimTypes.Role || claim.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
        .Select(claim => claim.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? Array.Empty<string>();

    public Guid? FacilityId
    {
        get
        {
            var value = GetClaimValue("facilityId") ?? GetClaimValue("FacilityId");
            return Guid.TryParse(value, out var facilityId) ? facilityId : null;
        }
    }

    public bool IsActive => true;

    public bool IsInRole(string roleName) => Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);

    public bool BelongsToFacility(Guid facilityId) => FacilityId.HasValue && FacilityId.Value == facilityId;

    private string? GetClaimValue(string claimType)
        => _httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value
           ?? _httpContextAccessor.HttpContext?.User?.FindFirst(claim => claim.Type == ClaimTypes.NameIdentifier && claimType == "sub")?.Value;
}
