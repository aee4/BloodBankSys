using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;

namespace BloodLink.Infrastructure.Services.Inventory;

public sealed class InventoryService : IInventoryService
{
    public Task<IReadOnlyList<InventoryItemDto>> GetOwnInventoryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<InventoryItemDto>>(Array.Empty<InventoryItemDto>());

    public Task AdjustInventoryAsync(InventoryAdjustmentRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionHistoryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<InventoryTransactionDto>>(Array.Empty<InventoryTransactionDto>());

    public Task<IReadOnlyList<AvailabilityResultDto>> SearchAvailabilityAsync(AvailabilitySearchRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AvailabilityResultDto>>(Array.Empty<AvailabilityResultDto>());

    public Task ReserveForRequestAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ReleaseReservationAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task FulfilTransferAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
