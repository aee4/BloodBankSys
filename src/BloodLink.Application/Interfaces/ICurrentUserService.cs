namespace BloodLink.Application.Interfaces;

/// <summary>Current actor information. Authentication alone never authorizes a business operation.</summary>
/// <remarks>
/// Production access properties perform fresh database reads, potentially synchronous I/O.
/// Values are not a transaction-wide snapshot. Callers must enforce role, facility and record ownership
/// at the operation boundary; do not cache these values across operations or circuit events.
/// </remarks>
public interface ICurrentUserService
{
    /// <summary>The principal's identifier; may still identify a deleted or revoked account.</summary>
    string? UserId { get; }
    /// <summary>Whether the principal was authenticated, not whether its account/session is currently valid.</summary>
    bool IsAuthenticated { get; }
    /// <summary>Fresh effective roles; empty for stale, inactive, blocked or password-restricted sessions.</summary>
    IReadOnlyCollection<string> Roles { get; }
    /// <summary>Authoritative facility scope for an operational facility user; otherwise null.</summary>
    Guid? FacilityId { get; }
    /// <summary>Operational session eligibility, including stamp, roles, facility and mandatory password change.</summary>
    bool IsActive { get; }
    /// <summary>Checks a fresh effective role. Does not replace target-record ownership checks.</summary>
    bool IsInRole(string roleName);
    /// <summary>Checks fresh operational facility membership against the supplied identifier.</summary>
    bool BelongsToFacility(Guid facilityId);
}
