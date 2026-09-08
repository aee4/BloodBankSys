using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Data;

namespace BloodLink.Infrastructure.Services.StaffServices;

/// <summary>
/// Service for managing facility staff lifecycle.
/// Owned by Backend Developer 1.
/// </summary>
public sealed class StaffService : IStaffService
{
    private readonly BloodLinkDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public StaffService(BloodLinkDbContext context, ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public Task<IReadOnlyList<StaffDto>> ListOwnFacilityStaffAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - ListOwnFacilityStaffAsync");
    }

    public Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - CreateStaffAsync");
    }

    public Task DeactivateStaffAsync(ChangeStaffStatusRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - DeactivateStaffAsync");
    }

    public Task ReactivateStaffAsync(ChangeStaffStatusRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - ReactivateStaffAsync");
    }

    public Task ResetTemporaryPasswordAsync(string userId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 1 - ResetTemporaryPasswordAsync");
    }
}
