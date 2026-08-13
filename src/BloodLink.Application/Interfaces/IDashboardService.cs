using BloodLink.Application.DTOs;

namespace BloodLink.Application.Interfaces;

public interface IDashboardService
{
    Task<SystemDashboardDto> GetSystemAdminDashboardAsync(CancellationToken cancellationToken = default);
    Task<FacilityAdminDashboardDto> GetFacilityAdminDashboardAsync(CancellationToken cancellationToken = default);
    Task<StaffDashboardDto> GetFacilityStaffDashboardAsync(CancellationToken cancellationToken = default);
}
