using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Data;

namespace BloodLink.Infrastructure.Services.FacilityServices;

/// <summary>
/// Service for managing facility registration, approval, and lifecycle.
/// Owned by Backend Developer 1.
/// </summary>
public sealed class FacilityService : IFacilityService
{
    private readonly BloodLinkDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public FacilityService(BloodLinkDbContext context, ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public Task<FacilityDto> RegisterFacilityAsync(RegisterFacilityRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - RegisterFacilityAsync");
    }

    public Task<FacilityDto?> GetFacilityAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - GetFacilityAsync");
    }

    public Task UpdateOwnFacilityAsync(UpdateFacilityRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - UpdateOwnFacilityAsync");
    }

    public Task<IReadOnlyList<FacilityDto>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - ListPendingAsync");
    }

    public Task ApproveAsync(FacilityDecisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - ApproveAsync");
    }

    public Task RejectAsync(FacilityDecisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - RejectAsync");
    }

    public Task SuspendAsync(FacilityDecisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - SuspendAsync");
    }

    public Task RestoreAsync(FacilityDecisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - RestoreAsync");
    }
}
