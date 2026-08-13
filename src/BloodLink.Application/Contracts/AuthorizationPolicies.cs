namespace BloodLink.Application.Contracts;

public static class AuthorizationPolicies
{
    public const string RequireSystemAdmin = nameof(RequireSystemAdmin);
    public const string RequireFacilityAdmin = nameof(RequireFacilityAdmin);
    public const string RequireFacilityStaff = nameof(RequireFacilityStaff);
    public const string RequireApprovedFacilityUser = nameof(RequireApprovedFacilityUser);
}
