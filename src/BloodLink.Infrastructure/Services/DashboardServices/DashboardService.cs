using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Data;

namespace BloodLink.Infrastructure.Services.DashboardServices;

/// <summary>
/// Service for providing role-specific dashboard data and metrics.
/// Owned by Backend Developer 3.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly BloodLinkDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DashboardService(BloodLinkDbContext context, ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public Task<SystemDashboardDto> GetSystemAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - GetSystemAdminDashboardAsync");
    }

    public Task<FacilityAdminDashboardDto> GetFacilityAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - GetFacilityAdminDashboardAsync");
    }

    public Task<StaffDashboardDto> GetFacilityStaffDashboardAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - GetFacilityStaffDashboardAsync");
    }
}
