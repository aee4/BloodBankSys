using BloodLink.Application.DTOs;

namespace BloodLink.Application.Interfaces;

public interface IBloodNeedService
{
    Task<BloodNeedDto> CreateAsync(CreateBloodNeedRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BloodNeedDto>> GetMineAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BloodNeedDto>> ListOwnFacilityAsync(CancellationToken cancellationToken = default);
    Task StartSearchAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default);
    Task FulfilInternallyAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default);
    Task RejectAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default);
}
