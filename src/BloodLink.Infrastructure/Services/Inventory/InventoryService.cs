using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Domain.Exceptions;
using BloodLink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Services.Inventory;

/// <summary>
/// Service for managing blood inventory, reservations, transfers, and availability searches.
/// Ensures atomicity, facility ownership, role-based authorization, and immutable transaction audit trails.
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private readonly BloodLinkDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public InventoryService(BloodLinkDbContext context, ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<IReadOnlyList<InventoryItemDto>> GetOwnInventoryAsync(CancellationToken cancellationToken = default)
    {
        ValidateCurrentUserAuthorization();

        var facilityId = _currentUserService.FacilityId!.Value;

        var inventory = await _context.BloodInventory
            .Where(bi => bi.FacilityId == facilityId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return inventory
            .Select(bi => new InventoryItemDto(
                bi.Id,
                bi.FacilityId,
                bi.BloodType,
                bi.TotalUnits,
                bi.ReservedUnits,
                bi.AvailableUnits,
                bi.LowStockThreshold))
            .ToList()
            .AsReadOnly();
    }

    public async Task AdjustInventoryAsync(InventoryAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        ValidateFacilityAdminAuthorization();
        var facilityId = _currentUserService.FacilityId!.Value;

        // Verify facility is approved
        var facility = await GetApprovedFacilityAsync(facilityId, cancellationToken);

        // Get or create inventory item
        var inventory = await _context.BloodInventory
            .FirstOrDefaultAsync(bi => bi.FacilityId == facilityId && bi.BloodType == request.BloodType, cancellationToken);

        if (inventory == null)
        {
            // Create new inventory item
            inventory = new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                BloodType = request.BloodType,
                TotalUnits = 0,
                ReservedUnits = 0,
                LowStockThreshold = 10, // Default threshold
                UpdatedAtUtc = DateTime.UtcNow
            };
            _context.BloodInventory.Add(inventory);
        }

        // Validate new total is non-negative
        var newTotal = inventory.TotalUnits + request.TotalUnitsChange;
        if (newTotal < 0)
        {
            throw new InsufficientInventoryException($"Inventory adjustment would result in negative units. Current: {inventory.TotalUnits}, Change: {request.TotalUnitsChange}");
        }

        // Update inventory
        inventory.TotalUnits = newTotal;
        inventory.UpdatedAtUtc = DateTime.UtcNow;

        // Determine transaction type
        var transactionType = request.TotalUnitsChange > 0 ? InventoryTransactionType.StockIn : InventoryTransactionType.Consumption;
        if (request.Reason.Contains("Manual", StringComparison.OrdinalIgnoreCase) || request.Reason.Contains("Adjustment", StringComparison.OrdinalIgnoreCase))
        {
            transactionType = InventoryTransactionType.ManualAdjustment;
        }

        // Create immutable transaction
        var transaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            BloodInventoryId = inventory.Id,
            TransactionType = transactionType,
            TotalUnitsChange = request.TotalUnitsChange,
            ReservedUnitsChange = 0,
            TotalAfter = inventory.TotalUnits,
            ReservedAfter = inventory.ReservedUnits,
            Reason = request.Reason,
            ReferenceType = null,
            ReferenceId = null,
            PerformedByUserId = _currentUserService.UserId!,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.InventoryTransactions.Add(transaction);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Inventory was modified concurrently. Please try again.", ex);
        }
    }

    public async Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionHistoryAsync(CancellationToken cancellationToken = default)
    {
        ValidateCurrentUserAuthorization();

        var facilityId = _currentUserService.FacilityId!.Value;

        var transactions = await _context.InventoryTransactions
            .Include(t => t.BloodInventoryId)
            .Where(t => _context.BloodInventory
                .Where(bi => bi.FacilityId == facilityId)
                .Any(bi => bi.Id == t.BloodInventoryId))
            .OrderByDescending(t => t.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Map with BloodType from related inventory
        var transactionDtos = new List<InventoryTransactionDto>();
        foreach (var transaction in transactions)
        {
            var inventory = await _context.BloodInventory
                .FirstOrDefaultAsync(bi => bi.Id == transaction.BloodInventoryId, cancellationToken);

            if (inventory != null)
            {
                transactionDtos.Add(new InventoryTransactionDto(
                    transaction.Id,
                    inventory.BloodType,
                    transaction.TransactionType,
                    transaction.TotalUnitsChange,
                    transaction.ReservedUnitsChange,
                    transaction.CreatedAtUtc));
            }
        }

        return transactionDtos.AsReadOnly();
    }

    public async Task<IReadOnlyList<LowStockAlertDto>> GetLowStockAlertsAsync(LowStockQueryRequest request, CancellationToken cancellationToken = default)
    {
        ValidateCurrentUserAuthorization();

        var facilityId = _currentUserService.FacilityId!.Value;

        var lowStockItems = await _context.BloodInventory
            .Where(bi => bi.FacilityId == facilityId && bi.AvailableUnits <= bi.LowStockThreshold)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return lowStockItems
            .Select(bi => new LowStockAlertDto(
                bi.BloodType,
                bi.AvailableUnits,
                bi.LowStockThreshold,
                bi.UpdatedAtUtc))
            .OrderBy(la => la.AvailableUnits)
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<AvailabilityResultDto>> SearchAvailabilityAsync(AvailabilitySearchRequest request, CancellationToken cancellationToken = default)
    {
        ValidateFacilityAdminAuthorization();

        var requestingFacilityId = _currentUserService.FacilityId!.Value;

        // Verify requesting facility is approved
        await GetApprovedFacilityAsync(requestingFacilityId, cancellationToken);

        // Search approved active facilities for exact blood type with minimum available units
        // Exclude the requesting facility and pending/suspended facilities
        var results = await _context.BloodInventory
            .Where(bi => 
                bi.BloodType == request.BloodType &&
                bi.AvailableUnits >= request.MinimumAvailableUnits &&
                bi.FacilityId != requestingFacilityId &&
                _context.Facilities.Any(f => 
                    f.Id == bi.FacilityId && 
                    f.Status == FacilityStatus.Approved))
            .Include(bi => _context.Facilities.FirstOrDefault(f => f.Id == bi.FacilityId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var availabilityResults = new List<AvailabilityResultDto>();

        foreach (var inventory in results)
        {
            var facility = await _context.Facilities
                .FirstOrDefaultAsync(f => f.Id == inventory.FacilityId, cancellationToken);

            if (facility != null && facility.Status == FacilityStatus.Approved)
            {
                availabilityResults.Add(new AvailabilityResultDto(
                    facility.Id,
                    facility.Name,
                    facility.FacilityType,
                    facility.City,
                    inventory.BloodType,
                    inventory.AvailableUnits));
            }
        }

        return availabilityResults.AsReadOnly();
    }

    public async Task ReserveForRequestAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        // Get the blood request
        var request = await _context.BloodRequests
            .FirstOrDefaultAsync(br => br.Id == bloodRequestId, cancellationToken)
            ?? throw new EntityNotFoundException($"Blood request with ID {bloodRequestId} not found.");

        // Verify status is Sent (can only reserve when newly accepted)
        if (request.Status != BloodRequestStatus.Sent)
        {
            throw new BusinessRuleViolationException($"Cannot reserve for request with status {request.Status}. Only Sent requests can be reserved.");
        }

        // Get the source facility inventory
        var sourceInventory = await _context.BloodInventory
            .FirstOrDefaultAsync(bi => bi.FacilityId == request.SourceFacilityId && bi.BloodType == request.BloodType, cancellationToken)
            ?? throw new EntityNotFoundException($"Inventory not found for source facility {request.SourceFacilityId} and blood type {request.BloodType}.");

        // Verify sufficient available units
        if (sourceInventory.AvailableUnits < request.UnitsRequested)
        {
            throw new InsufficientInventoryException($"Insufficient available units. Required: {request.UnitsRequested}, Available: {sourceInventory.AvailableUnits}");
        }

        // Atomically reserve units
        sourceInventory.ReservedUnits += request.UnitsRequested;
        sourceInventory.UpdatedAtUtc = DateTime.UtcNow;

        // Create immutable reservation transaction
        var transaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            BloodInventoryId = sourceInventory.Id,
            TransactionType = InventoryTransactionType.Reserve,
            TotalUnitsChange = 0,
            ReservedUnitsChange = request.UnitsRequested,
            TotalAfter = sourceInventory.TotalUnits,
            ReservedAfter = sourceInventory.ReservedUnits,
            Reason = $"Reservation for blood request {bloodRequestId} from {request.RequestingFacilityId}",
            ReferenceType = nameof(BloodRequest),
            ReferenceId = bloodRequestId,
            PerformedByUserId = _currentUserService.UserId!,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.InventoryTransactions.Add(transaction);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Inventory was modified concurrently. Reservation failed. Please try again.", ex);
        }
    }

    public async Task ReleaseReservationAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        // Get the blood request
        var request = await _context.BloodRequests
            .FirstOrDefaultAsync(br => br.Id == bloodRequestId, cancellationToken)
            ?? throw new EntityNotFoundException($"Blood request with ID {bloodRequestId} not found.");

        // Verify request has been accepted (has reserved units)
        if (request.Status != BloodRequestStatus.Accepted || !request.UnitsAccepted.HasValue)
        {
            throw new BusinessRuleViolationException($"Cannot release reservation for request with status {request.Status}. Only Accepted requests with units reserved can be released.");
        }

        // Get the source facility inventory
        var sourceInventory = await _context.BloodInventory
            .FirstOrDefaultAsync(bi => bi.FacilityId == request.SourceFacilityId && bi.BloodType == request.BloodType, cancellationToken)
            ?? throw new EntityNotFoundException($"Inventory not found for source facility {request.SourceFacilityId} and blood type {request.BloodType}.");

        // Verify reserved units exist
        if (sourceInventory.ReservedUnits < request.UnitsAccepted)
        {
            throw new InsufficientInventoryException($"Reservation mismatch. Expected to release {request.UnitsAccepted}, but only {sourceInventory.ReservedUnits} units reserved.");
        }

        // Atomically release reservation
        sourceInventory.ReservedUnits -= request.UnitsAccepted.Value;
        sourceInventory.UpdatedAtUtc = DateTime.UtcNow;

        // Create immutable release transaction
        var transaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            BloodInventoryId = sourceInventory.Id,
            TransactionType = InventoryTransactionType.Release,
            TotalUnitsChange = 0,
            ReservedUnitsChange = -request.UnitsAccepted.Value,
            TotalAfter = sourceInventory.TotalUnits,
            ReservedAfter = sourceInventory.ReservedUnits,
            Reason = $"Release of reservation for cancelled blood request {bloodRequestId}",
            ReferenceType = nameof(BloodRequest),
            ReferenceId = bloodRequestId,
            PerformedByUserId = _currentUserService.UserId!,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.InventoryTransactions.Add(transaction);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Inventory was modified concurrently. Release failed. Please try again.", ex);
        }
    }

    public async Task FulfilTransferAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        // Get the blood request
        var request = await _context.BloodRequests
            .FirstOrDefaultAsync(br => br.Id == bloodRequestId, cancellationToken)
            ?? throw new EntityNotFoundException($"Blood request with ID {bloodRequestId} not found.");

        // Verify status is Accepted
        if (request.Status != BloodRequestStatus.Accepted || !request.UnitsAccepted.HasValue)
        {
            throw new BusinessRuleViolationException($"Cannot fulfil transfer for request with status {request.Status}. Only Accepted requests with confirmed units can be transferred.");
        }

        // Get source and requesting facility inventory
        var sourceInventory = await _context.BloodInventory
            .FirstOrDefaultAsync(bi => bi.FacilityId == request.SourceFacilityId && bi.BloodType == request.BloodType, cancellationToken)
            ?? throw new EntityNotFoundException($"Inventory not found for source facility {request.SourceFacilityId} and blood type {request.BloodType}.");

        var requestingInventory = await _context.BloodInventory
            .FirstOrDefaultAsync(bi => bi.FacilityId == request.RequestingFacilityId && bi.BloodType == request.BloodType, cancellationToken);

        // If requesting facility has no inventory record, create one
        if (requestingInventory == null)
        {
            requestingInventory = new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = request.RequestingFacilityId,
                BloodType = request.BloodType,
                TotalUnits = 0,
                ReservedUnits = 0,
                LowStockThreshold = 10,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _context.BloodInventory.Add(requestingInventory);
        }

        // Verify source has sufficient reserved units
        if (sourceInventory.ReservedUnits < request.UnitsAccepted.Value)
        {
            throw new InsufficientInventoryException($"Insufficient reserved units for transfer. Expected: {request.UnitsAccepted.Value}, Reserved: {sourceInventory.ReservedUnits}");
        }

        var unitsToTransfer = request.UnitsAccepted.Value;

        // Atomically transfer: decrease source TotalUnits and ReservedUnits, increase requesting TotalUnits
        sourceInventory.TotalUnits -= unitsToTransfer;
        sourceInventory.ReservedUnits -= unitsToTransfer;
        sourceInventory.UpdatedAtUtc = DateTime.UtcNow;

        requestingInventory.TotalUnits += unitsToTransfer;
        requestingInventory.UpdatedAtUtc = DateTime.UtcNow;

        // Create immutable transfer-out transaction for source
        var transferOutTransaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            BloodInventoryId = sourceInventory.Id,
            TransactionType = InventoryTransactionType.TransferOut,
            TotalUnitsChange = -unitsToTransfer,
            ReservedUnitsChange = -unitsToTransfer,
            TotalAfter = sourceInventory.TotalUnits,
            ReservedAfter = sourceInventory.ReservedUnits,
            Reason = $"Transfer fulfillment for blood request {bloodRequestId} to facility {request.RequestingFacilityId}",
            ReferenceType = nameof(BloodRequest),
            ReferenceId = bloodRequestId,
            PerformedByUserId = _currentUserService.UserId!,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Create immutable transfer-in transaction for requesting facility
        var transferInTransaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            BloodInventoryId = requestingInventory.Id,
            TransactionType = InventoryTransactionType.TransferIn,
            TotalUnitsChange = unitsToTransfer,
            ReservedUnitsChange = 0,
            TotalAfter = requestingInventory.TotalUnits,
            ReservedAfter = requestingInventory.ReservedUnits,
            Reason = $"Transfer received for blood request {bloodRequestId} from facility {request.SourceFacilityId}",
            ReferenceType = nameof(BloodRequest),
            ReferenceId = bloodRequestId,
            PerformedByUserId = _currentUserService.UserId!,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.InventoryTransactions.Add(transferOutTransaction);
        _context.InventoryTransactions.Add(transferInTransaction);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Inventory was modified concurrently. Transfer failed. Please try again.", ex);
        }
    }

    // Authorization Helpers
    private void ValidateCurrentUserAuthorization()
    {
        if (!_currentUserService.IsAuthenticated)
        {
            throw new Domain.Exceptions.UnauthorizedAccessException("User must be authenticated.");
        }

        if (_currentUserService.FacilityId == null || _currentUserService.FacilityId == Guid.Empty)
        {
            throw new Domain.Exceptions.UnauthorizedAccessException("User must belong to a facility.");
        }

        if (!_currentUserService.IsActive)
        {
            throw new Domain.Exceptions.UnauthorizedAccessException("User account is not active.");
        }
    }

    private void ValidateFacilityAdminAuthorization()
    {
        ValidateCurrentUserAuthorization();

        if (!_currentUserService.IsInRole("FacilityAdmin"))
        {
            throw new Domain.Exceptions.UnauthorizedAccessException("Only FacilityAdmin users can perform this operation.");
        }
    }

    private async Task<Facility> GetApprovedFacilityAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        var facility = await _context.Facilities
            .FirstOrDefaultAsync(f => f.Id == facilityId, cancellationToken)
            ?? throw new EntityNotFoundException($"Facility with ID {facilityId} not found.");

        if (facility.Status != FacilityStatus.Approved)
        {
            throw new InvalidFacilityStatusException($"Facility must be Approved to perform inventory operations. Current status: {facility.Status}");
        }

        return facility;
    }
}
