using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;

namespace BloodLink.Application.Interfaces;

/// <summary>
/// Service for managing blood inventory, transactions, reservations, and availability searches.
/// All operations enforce facility ownership and concurrency safety via RowVersion.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Gets all inventory items for the current user's facility.
    /// </summary>
    Task<IReadOnlyList<InventoryItemDto>> GetOwnInventoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adjusts inventory for the current user's facility.
    /// Creates an immutable InventoryTransaction record.
    /// </summary>
    /// <remarks>
    /// Validates:
    /// - Current user is FacilityAdmin
    /// - Current user's facility is Approved and Active
    /// - TotalUnitsChange does not result in negative inventory
    /// </remarks>
    Task AdjustInventoryAsync(InventoryAdjustmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all inventory transactions for the current user's facility.
    /// Transactions are immutable and provide audit trail.
    /// </summary>
    Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets low-stock blood types for the current user's facility.
    /// </summary>
    Task<IReadOnlyList<LowStockAlertDto>> GetLowStockAlertsAsync(LowStockQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches approved active facilities for exact blood type availability.
    /// Excludes the requesting facility and returns available units only.
    /// </summary>
    /// <remarks>
    /// Validates:
    /// - Current user is FacilityAdmin
    /// - Current user's facility is Approved and Active
    /// - Search excludes requester facility and pending/suspended facilities
    /// - Returns exact BloodType matches only
    /// </remarks>
    Task<IReadOnlyList<AvailabilityResultDto>> SearchAvailabilityAsync(AvailabilitySearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserves blood units for an accepted request.
    /// Atomically increases ReservedUnits and creates a Reserve transaction.
    /// Throws if insufficient AvailableUnits or RowVersion conflict.
    /// </summary>
    Task ReserveForRequestAsync(Guid bloodRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a previous reservation if request is cancelled before fulfilment.
    /// Atomically decreases ReservedUnits and creates a Release transaction.
    /// </summary>
    Task ReleaseReservationAsync(Guid bloodRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers reserved blood from source facility to requesting facility.
    /// Atomically decreases source TotalUnits and ReservedUnits, increases requesting facility TotalUnits.
    /// Creates TransferOut and TransferIn transactions for both facilities.
    /// Must be called only after real-world handover confirmation.
    /// </summary>
    Task FulfilTransferAsync(Guid bloodRequestId, CancellationToken cancellationToken = default);
}
