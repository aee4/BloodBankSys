using BloodLink.Application.DTOs;

namespace BloodLink.Application.Interfaces;

public interface IStaffService
{
    Task<IReadOnlyList<StaffDto>> ListOwnFacilityStaffAsync(CancellationToken cancellationToken = default);
    Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default);
    Task DeactivateStaffAsync(ChangeStaffStatusRequest request, CancellationToken cancellationToken = default);
    Task ReactivateStaffAsync(ChangeStaffStatusRequest request, CancellationToken cancellationToken = default);
    Task ResetTemporaryPasswordAsync(string userId, CancellationToken cancellationToken = default);
}
