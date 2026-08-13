using BloodLink.Application.DTOs;

namespace BloodLink.Application.Interfaces;

public interface IBloodRequestService
{
    Task<BloodRequestDto> CreateFromNeedAsync(CreateBloodRequestRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BloodRequestDto>> ListSentAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BloodRequestDto>> ListReceivedAsync(CancellationToken cancellationToken = default);
    Task<BloodRequestDto?> GetAsync(Guid bloodRequestId, CancellationToken cancellationToken = default);
    Task AcceptAsync(RequestResponseRequest request, CancellationToken cancellationToken = default);
    Task RejectAsync(RequestResponseRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid bloodRequestId, CancellationToken cancellationToken = default);
    Task FulfilAsync(FulfilRequestRequest request, CancellationToken cancellationToken = default);
}
