using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Domain.Exceptions;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using UnauthorizedAccessException = BloodLink.Domain.Exceptions.UnauthorizedAccessException;

namespace BloodLink.Infrastructure.Tests.Services;

public class InventoryServiceTests : IDisposable
{
    private readonly BloodLinkDbContext _context;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly InventoryService _service;

    private readonly Guid _facilityId = Guid.NewGuid();
    private readonly Guid _sourceFacilityId = Guid.NewGuid();
    private readonly Guid _requestingFacilityId = Guid.NewGuid();
    private readonly string _userId = "test-user-id";

    public InventoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<BloodLinkDbContext>()
            .UseInMemoryDatabase(databaseName: $"BloodLinkTest_{Guid.NewGuid()}")
            .Options;

        _context = new BloodLinkDbContext(options);
        _mockCurrentUserService = new Mock<ICurrentUserService>();

        SetupDefaultMockBehavior();

        _service = new InventoryService(_context, _mockCurrentUserService.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    private void SetupDefaultMockBehavior()
    {
        _mockCurrentUserService.Setup(s => s.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(s => s.UserId).Returns(_userId);
        _mockCurrentUserService.Setup(s => s.FacilityId).Returns(_facilityId);
        _mockCurrentUserService.Setup(s => s.IsActive).Returns(true);
        _mockCurrentUserService.Setup(s => s.IsInRole(It.IsAny<string>())).Returns(true);
        _mockCurrentUserService.Setup(s => s.BelongsToFacility(_facilityId)).Returns(true);
    }

    private void SeedApprovedFacility(Guid facilityId)
    {
        var facility = new Facility
        {
            Id = facilityId,
            Name = "Test Facility",
            FacilityType = FacilityType.Hospital,
            RegistrationNumber = "REG001",
            Region = "Test Region",
            City = "Test City",
            Address = "123 Test St",
            ContactEmail = "test@facility.com",
            ContactPhone = "555-0001",
            Status = FacilityStatus.Approved,
            CreatedByUserId = _userId,
            CreatedAtUtc = DateTime.UtcNow,
            ApprovedByUserId = _userId,
            ApprovedAtUtc = DateTime.UtcNow
        };

        _context.Facilities.Add(facility);
        _context.SaveChanges();
    }

    #region GetOwnInventoryAsync Tests

    [Fact]
    public async Task GetOwnInventoryAsync_ReturnsAllInventoryItemsForFacility()
    {
        // Arrange
        SeedApprovedFacility(_facilityId);

        var inventory1 = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 50,
            ReservedUnits = 10,
            LowStockThreshold = 15,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var inventory2 = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            BloodType = BloodType.ABNegative,
            TotalUnits = 5,
            ReservedUnits = 0,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.AddRange(inventory1, inventory2);
        _context.SaveChanges();

        // Act
        var result = await _service.GetOwnInventoryAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.BloodType == BloodType.OPositive && item.AvailableUnits == 40);
        Assert.Contains(result, item => item.BloodType == BloodType.ABNegative && item.AvailableUnits == 5);
        Assert.Contains(result, item => item.BloodType == BloodType.OPositive && item.UpdatedAtUtc is not null);
    }

    [Fact]
    public async Task GetOwnInventoryAsync_AllowsFacilityStaffViewOnlyRole()
    {
        SeedApprovedFacility(_facilityId);
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(false);
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityStaff")).Returns(true);

        var result = await _service.GetOwnInventoryAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOwnInventoryAsync_ThrowsWhenUserNotAuthenticated()
    {
        // Arrange
        _mockCurrentUserService.Setup(s => s.IsAuthenticated).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetOwnInventoryAsync());
    }

    [Fact]
    public async Task GetOwnInventoryAsync_ThrowsWhenUserNotActive()
    {
        // Arrange
        _mockCurrentUserService.Setup(s => s.IsActive).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetOwnInventoryAsync());
    }

    #endregion

    #region AdjustInventoryAsync Tests

    [Fact]
    public async Task AdjustInventoryAsync_CreatesNewInventoryAndTransaction()
    {
        // Arrange
        SeedApprovedFacility(_facilityId);
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);

        var request = new InventoryAdjustmentRequest(BloodType.OPositive, 20, "Stock received");

        // Act
        await _service.AdjustInventoryAsync(request);

        // Assert
        var inventory = await _context.BloodInventory
            .FirstOrDefaultAsync(bi => bi.FacilityId == _facilityId && bi.BloodType == BloodType.OPositive);

        Assert.NotNull(inventory);
        Assert.Equal(20, inventory.TotalUnits);

        var transaction = await _context.InventoryTransactions
            .FirstOrDefaultAsync(t => t.BloodInventoryId == inventory.Id);

        Assert.NotNull(transaction);
        Assert.Equal(InventoryTransactionType.StockIn, transaction.TransactionType);
        Assert.Equal(20, transaction.TotalUnitsChange);
        Assert.Equal(20, transaction.TotalAfter);
        Assert.Equal(0, transaction.ReservedAfter);
    }

    [Fact]
    public async Task AdjustInventoryAsync_ThrowsWhenResultingNegativeUnits()
    {
        // Arrange
        SeedApprovedFacility(_facilityId);
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);

        var inventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 5,
            ReservedUnits = 0,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.Add(inventory);
        _context.SaveChanges();

        var request = new InventoryAdjustmentRequest(BloodType.OPositive, -10, "Consumption");

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientInventoryException>(() => _service.AdjustInventoryAsync(request));
        Assert.Empty(_context.InventoryTransactions);
    }

    [Fact]
    public async Task AdjustInventoryAsync_RejectsZeroAndBlankReasonWithoutTransaction()
    {
        SeedApprovedFacility(_facilityId);
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AdjustInventoryAsync(new InventoryAdjustmentRequest(BloodType.OPositive, 0, "Stock count")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AdjustInventoryAsync(new InventoryAdjustmentRequest(BloodType.OPositive, 4, " ")));

        Assert.Empty(_context.InventoryTransactions);
    }

    [Fact]
    public async Task AdjustInventoryAsync_StaleRowVersionThrowsAndDoesNotCreateTransaction()
    {
        SeedApprovedFacility(_facilityId);
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);
        _context.BloodInventory.Add(new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 10,
            ReservedUnits = 2,
            LowStockThreshold = 5,
            RowVersion = [1, 2, 3],
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            _service.AdjustInventoryAsync(new InventoryAdjustmentRequest(BloodType.OPositive, 2, "Stock count", [9, 9, 9])));

        Assert.Empty(_context.InventoryTransactions);
        Assert.Equal(10, _context.BloodInventory.Single().TotalUnits);
    }

    [Fact]
    public async Task AdjustInventoryAsync_RejectsTotalBelowReservedUnits()
    {
        SeedApprovedFacility(_facilityId);
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);
        _context.BloodInventory.Add(new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 10,
            ReservedUnits = 6,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<InsufficientInventoryException>(() =>
            _service.AdjustInventoryAsync(new InventoryAdjustmentRequest(BloodType.OPositive, -5, "Consumption")));
        Assert.Empty(_context.InventoryTransactions);
    }

    [Fact]
    public async Task AdjustInventoryAsync_ThrowsWhenNotFacilityAdmin()
    {
        // Arrange
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(false);
        var request = new InventoryAdjustmentRequest(BloodType.OPositive, 10, "Test");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.AdjustInventoryAsync(request));
    }

    [Fact]
    public async Task AdjustInventoryAsync_ThrowsWhenFacilityNotApproved()
    {
        // Arrange
        var facility = new Facility
        {
            Id = _facilityId,
            Name = "Pending Facility",
            Status = FacilityStatus.Pending,
            CreatedByUserId = _userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Facilities.Add(facility);
        _context.SaveChanges();

        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);
        var request = new InventoryAdjustmentRequest(BloodType.OPositive, 10, "Test");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidFacilityStatusException>(() => _service.AdjustInventoryAsync(request));
    }

    #endregion

    #region GetTransactionHistoryAsync Tests

    [Fact]
    public async Task GetTransactionHistoryAsync_ReturnsTransactionsInDescendingOrder()
    {
        // Arrange
        SeedApprovedFacility(_facilityId);

        var inventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 50,
            ReservedUnits = 0,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.Add(inventory);
        _context.SaveChanges();

        var now = DateTime.UtcNow;
        var transaction1 = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            BloodInventoryId = inventory.Id,
            TransactionType = InventoryTransactionType.StockIn,
            TotalUnitsChange = 20,
            ReservedUnitsChange = 0,
            TotalAfter = 20,
            ReservedAfter = 0,
            Reason = "First transaction",
            PerformedByUserId = _userId,
            CreatedAtUtc = now.AddSeconds(-10)
        };

        var transaction2 = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            BloodInventoryId = inventory.Id,
            TransactionType = InventoryTransactionType.StockIn,
            TotalUnitsChange = 30,
            ReservedUnitsChange = 0,
            TotalAfter = 50,
            ReservedAfter = 0,
            Reason = "Second transaction",
            PerformedByUserId = _userId,
            CreatedAtUtc = now
        };

        _context.InventoryTransactions.AddRange(transaction1, transaction2);
        _context.SaveChanges();

        // Act
        var result = await _service.GetTransactionHistoryAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(transaction2.Id, result[0].Id);
        Assert.Equal("Second transaction", result[0].Reason);
        Assert.Equal(50, result[0].TotalAfter);
        Assert.Equal(0, result[0].ReservedAfter);
        Assert.Equal("System", result[0].ActorDisplayName);
    }

    #endregion

    #region GetLowStockAlertsAsync Tests

    [Fact]
    public async Task GetLowStockAlertsAsync_ReturnsBelowThresholdItems()
    {
        // Arrange
        SeedApprovedFacility(_facilityId);

        var inventory1 = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 5,
            ReservedUnits = 0,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var inventory2 = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            BloodType = BloodType.ABPositive,
            TotalUnits = 50,
            ReservedUnits = 0,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.AddRange(inventory1, inventory2);
        _context.SaveChanges();

        var request = new LowStockQueryRequest();

        // Act
        var result = await _service.GetLowStockAlertsAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(BloodType.OPositive, result[0].BloodType);
        Assert.Equal(5, result[0].AvailableUnits);
    }

    #endregion

    #region SearchAvailabilityAsync Tests

    [Fact]
    public async Task SearchAvailabilityAsync_ReturnsFacilitiesWithExactBloodType()
    {
        // Arrange
        SeedApprovedFacility(_facilityId);
        SeedApprovedFacility(_sourceFacilityId);

        var inventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 50,
            ReservedUnits = 10,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.Add(inventory);
        _context.SaveChanges();

        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);

        var request = new AvailabilitySearchRequest(BloodType.OPositive, 30);

        // Act
        var result = await _service.SearchAvailabilityAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(_sourceFacilityId, result[0].FacilityId);
        Assert.Equal(40, result[0].AvailableUnits);
        Assert.Equal(BloodType.OPositive, result[0].BloodType);
        Assert.Equal("Test Region", result[0].Region);
        Assert.Equal("Test City", result[0].City);
        Assert.True(result[0].UpdatedAtUtc > DateTime.MinValue);
    }

    [Fact]
    public async Task SearchAvailabilityAsync_ExcludesRequestingFacility()
    {
        // Arrange
        SeedApprovedFacility(_facilityId);

        var ownInventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 100,
            ReservedUnits = 0,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.Add(ownInventory);
        _context.SaveChanges();

        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);

        var request = new AvailabilitySearchRequest(BloodType.OPositive, 50);

        // Act
        var result = await _service.SearchAvailabilityAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // Own facility excluded
    }

    [Fact]
    public async Task SearchAvailabilityAsync_ExcludesPendingFacilities()
    {
        // Arrange
        SeedApprovedFacility(_facilityId);

        var pendingFacility = new Facility
        {
            Id = _sourceFacilityId,
            Name = "Pending Facility",
            Status = FacilityStatus.Pending,
            CreatedByUserId = _userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Facilities.Add(pendingFacility);
        _context.SaveChanges();

        var inventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 100,
            ReservedUnits = 0,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.Add(inventory);
        _context.SaveChanges();

        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);

        var request = new AvailabilitySearchRequest(BloodType.OPositive, 50);

        // Act
        var result = await _service.SearchAvailabilityAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // Pending facility excluded
    }

    [Fact]
    public async Task SearchAvailabilityAsync_RespectMinimumAvailableUnits()
    {
        // Arrange
        SeedApprovedFacility(_facilityId);
        SeedApprovedFacility(_sourceFacilityId);

        var inventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 100,
            ReservedUnits = 80, // Only 20 available
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.Add(inventory);
        _context.SaveChanges();

        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);

        var request = new AvailabilitySearchRequest(BloodType.OPositive, 30);

        // Act
        var result = await _service.SearchAvailabilityAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // Not enough available units
    }

    [Fact]
    public async Task SearchAvailabilityAsync_RequiresApprovedFacilityAdmin()
    {
        SeedApprovedFacility(_facilityId);
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(false);
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityStaff")).Returns(true);

        var request = new AvailabilitySearchRequest(BloodType.OPositive, 1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.SearchAvailabilityAsync(request));
    }

    [Fact]
    public async Task SearchAvailabilityAsync_ExcludesSuspendedFacilitiesAndZeroAvailability()
    {
        SeedApprovedFacility(_facilityId);
        SeedApprovedFacility(_sourceFacilityId);
        var suspendedFacilityId = Guid.NewGuid();
        _context.Facilities.Add(new Facility
        {
            Id = suspendedFacilityId,
            Name = "Suspended Facility",
            Status = FacilityStatus.Suspended,
            CreatedByUserId = _userId,
            CreatedAtUtc = DateTime.UtcNow
        });
        _context.BloodInventory.AddRange(
            new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = _sourceFacilityId,
                BloodType = BloodType.OPositive,
                TotalUnits = 6,
                ReservedUnits = 6,
                LowStockThreshold = 10,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = suspendedFacilityId,
                BloodType = BloodType.OPositive,
                TotalUnits = 20,
                ReservedUnits = 0,
                LowStockThreshold = 10,
                UpdatedAtUtc = DateTime.UtcNow
            });
        await _context.SaveChangesAsync();
        _mockCurrentUserService.Setup(s => s.IsInRole("FacilityAdmin")).Returns(true);

        var result = await _service.SearchAvailabilityAsync(new AvailabilitySearchRequest(BloodType.OPositive, 0));

        Assert.Empty(result);
    }

    #endregion

    #region ReserveForRequestAsync Tests

    [Fact]
    public async Task ReserveForRequestAsync_AtomicallyIncreasesReservedUnits()
    {
        // Arrange
        SeedApprovedFacility(_sourceFacilityId);
        _mockCurrentUserService.Setup(s => s.FacilityId).Returns(_sourceFacilityId);

        var inventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 100,
            ReservedUnits = 20,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.Add(inventory);

        var request = new BloodRequest
        {
            Id = Guid.NewGuid(),
            BloodNeedId = Guid.NewGuid(),
            RequestingFacilityId = _requestingFacilityId,
            SourceFacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            UnitsRequested = 30,
            Status = BloodRequestStatus.Sent,
            RequestedByAdminId = "admin-123",
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.BloodRequests.Add(request);
        _context.SaveChanges();

        // Act
        await _service.ReserveForRequestAsync(request.Id, request.UnitsRequested);

        // Assert
        var updatedInventory = await _context.BloodInventory.FirstOrDefaultAsync(bi => bi.Id == inventory.Id);
        Assert.NotNull(updatedInventory);
        Assert.Equal(50, updatedInventory.ReservedUnits);

        var transaction = await _context.InventoryTransactions
            .FirstOrDefaultAsync(t => t.BloodInventoryId == inventory.Id && t.TransactionType == InventoryTransactionType.Reserve);

        Assert.NotNull(transaction);
        Assert.Equal(30, transaction.ReservedUnitsChange);
    }

    [Fact]
    public async Task ReserveForRequestAsync_ThrowsWhenInsufficientAvailableUnits()
    {
        // Arrange
        SeedApprovedFacility(_sourceFacilityId);
        _mockCurrentUserService.Setup(s => s.FacilityId).Returns(_sourceFacilityId);
        var inventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 100,
            ReservedUnits = 80, // Only 20 available
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.Add(inventory);

        var request = new BloodRequest
        {
            Id = Guid.NewGuid(),
            BloodNeedId = Guid.NewGuid(),
            RequestingFacilityId = _requestingFacilityId,
            SourceFacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            UnitsRequested = 30,
            Status = BloodRequestStatus.Sent,
            RequestedByAdminId = "admin-123",
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.BloodRequests.Add(request);
        _context.SaveChanges();

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientInventoryException>(() => _service.ReserveForRequestAsync(request.Id, request.UnitsRequested));
    }

    [Fact]
    public async Task ReserveForRequestAsync_ThrowsWhenRequestNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _service.ReserveForRequestAsync(Guid.NewGuid(), 1));
    }

    #endregion

    #region ReleaseReservationAsync Tests

    [Fact]
    public async Task ReleaseReservationAsync_AtomicallyDecreasesReservedUnits()
    {
        // Arrange
        SeedApprovedFacility(_sourceFacilityId);
        _mockCurrentUserService.Setup(s => s.FacilityId).Returns(_requestingFacilityId);
        SeedApprovedFacility(_requestingFacilityId);

        var inventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 100,
            ReservedUnits = 30,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.Add(inventory);

        var request = new BloodRequest
        {
            Id = Guid.NewGuid(),
            BloodNeedId = Guid.NewGuid(),
            RequestingFacilityId = _requestingFacilityId,
            SourceFacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            UnitsRequested = 30,
            UnitsAccepted = 30,
            Status = BloodRequestStatus.Accepted,
            RequestedByAdminId = "admin-123",
            RespondedByAdminId = "admin-456",
            CreatedAtUtc = DateTime.UtcNow,
            RespondedAtUtc = DateTime.UtcNow
        };

        _context.BloodRequests.Add(request);
        _context.SaveChanges();

        // Act
        await _service.ReleaseReservationAsync(request.Id);

        // Assert
        var updatedInventory = await _context.BloodInventory.FirstOrDefaultAsync(bi => bi.Id == inventory.Id);
        Assert.NotNull(updatedInventory);
        Assert.Equal(0, updatedInventory.ReservedUnits);

        var transaction = await _context.InventoryTransactions
            .FirstOrDefaultAsync(t => t.BloodInventoryId == inventory.Id && t.TransactionType == InventoryTransactionType.Release);

        Assert.NotNull(transaction);
        Assert.Equal(-30, transaction.ReservedUnitsChange);
    }

    #endregion

    #region FulfilTransferAsync Tests

    [Fact]
    public async Task FulfilTransferAsync_AtomicallyTransfersStockBothFacilities()
    {
        // Arrange
        SeedApprovedFacility(_sourceFacilityId);
        SeedApprovedFacility(_requestingFacilityId);
        _mockCurrentUserService.Setup(s => s.FacilityId).Returns(_sourceFacilityId);

        var sourceInventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 100,
            ReservedUnits = 30,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var requestingInventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _requestingFacilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 50,
            ReservedUnits = 0,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.AddRange(sourceInventory, requestingInventory);

        var request = new BloodRequest
        {
            Id = Guid.NewGuid(),
            BloodNeedId = Guid.NewGuid(),
            RequestingFacilityId = _requestingFacilityId,
            SourceFacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            UnitsRequested = 30,
            UnitsAccepted = 30,
            Status = BloodRequestStatus.Accepted,
            RequestedByAdminId = "admin-123",
            RespondedByAdminId = "admin-456",
            CreatedAtUtc = DateTime.UtcNow,
            RespondedAtUtc = DateTime.UtcNow
        };

        _context.BloodRequests.Add(request);
        _context.SaveChanges();

        // Act
        await _service.FulfilTransferAsync(request.Id);

        // Assert
        var updatedSourceInventory = await _context.BloodInventory.FirstOrDefaultAsync(bi => bi.Id == sourceInventory.Id);
        Assert.NotNull(updatedSourceInventory);
        Assert.Equal(70, updatedSourceInventory.TotalUnits);
        Assert.Equal(0, updatedSourceInventory.ReservedUnits);

        var updatedRequestingInventory = await _context.BloodInventory.FirstOrDefaultAsync(bi => bi.Id == requestingInventory.Id);
        Assert.NotNull(updatedRequestingInventory);
        Assert.Equal(80, updatedRequestingInventory.TotalUnits);

        var transactions = await _context.InventoryTransactions
            .Where(t => t.ReferenceId == request.Id)
            .ToListAsync();

        Assert.Equal(2, transactions.Count);
        Assert.Single(transactions, t => t.TransactionType == InventoryTransactionType.TransferOut);
        Assert.Single(transactions, t => t.TransactionType == InventoryTransactionType.TransferIn);
    }

    [Fact]
    public async Task FulfilTransferAsync_CreatesInventoryIfNotExistsForRequesting()
    {
        // Arrange
        SeedApprovedFacility(_sourceFacilityId);
        SeedApprovedFacility(_requestingFacilityId);
        _mockCurrentUserService.Setup(s => s.FacilityId).Returns(_sourceFacilityId);

        var sourceInventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            TotalUnits = 100,
            ReservedUnits = 30,
            LowStockThreshold = 10,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.BloodInventory.Add(sourceInventory);

        var request = new BloodRequest
        {
            Id = Guid.NewGuid(),
            BloodNeedId = Guid.NewGuid(),
            RequestingFacilityId = _requestingFacilityId,
            SourceFacilityId = _sourceFacilityId,
            BloodType = BloodType.OPositive,
            UnitsRequested = 30,
            UnitsAccepted = 30,
            Status = BloodRequestStatus.Accepted,
            RequestedByAdminId = "admin-123",
            RespondedByAdminId = "admin-456",
            CreatedAtUtc = DateTime.UtcNow,
            RespondedAtUtc = DateTime.UtcNow
        };

        _context.BloodRequests.Add(request);
        _context.SaveChanges();

        // Act
        await _service.FulfilTransferAsync(request.Id);

        // Assert
        var newInventory = await _context.BloodInventory
            .FirstOrDefaultAsync(bi => bi.FacilityId == _requestingFacilityId && bi.BloodType == BloodType.OPositive);

        Assert.NotNull(newInventory);
        Assert.Equal(30, newInventory.TotalUnits);
    }

    #endregion
}
