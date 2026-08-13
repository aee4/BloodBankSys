using BloodLink.Application.DTOs;

namespace BloodLink.Application.Interfaces;

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryItemDto>> GetOwnInventoryAsync(CancellationToken cancellationToken = default);
    Task AdjustInventoryAsync(InventoryAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvailabilityResultDto>> SearchAvailabilityAsync(AvailabilitySearchRequest request, CancellationToken cancellationToken = default);
    Task ReserveForRequestAsync(Guid bloodRequestId, CancellationToken cancellationToken = default);
    Task ReleaseReservationAsync(Guid bloodRequestId, CancellationToken cancellationToken = default);
    Task FulfilTransferAsync(Guid bloodRequestId, CancellationToken cancellationToken = default);
}
