using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Tests.Services;

internal static class WorkflowTestSupport
{
    public static readonly Guid FacilityAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid FacilityBId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid FacilityCId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static BloodLinkDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BloodLinkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dbContext = new BloodLinkDbContext(options);
        dbContext.Roles.AddRange(
            new IdentityRole(RoleNames.SystemAdmin) { Id = RoleNames.SystemAdmin, NormalizedName = RoleNames.SystemAdmin.ToUpperInvariant() },
            new IdentityRole(RoleNames.FacilityAdmin) { Id = RoleNames.FacilityAdmin, NormalizedName = RoleNames.FacilityAdmin.ToUpperInvariant() },
            new IdentityRole(RoleNames.FacilityStaff) { Id = RoleNames.FacilityStaff, NormalizedName = RoleNames.FacilityStaff.ToUpperInvariant() });
        dbContext.Facilities.AddRange(
            new Facility { Id = FacilityAId, Name = "Facility A", RegistrationNumber = "A", Status = FacilityStatus.Approved },
            new Facility { Id = FacilityBId, Name = "Facility B", RegistrationNumber = "B", Status = FacilityStatus.Approved },
            new Facility { Id = FacilityCId, Name = "Facility C", RegistrationNumber = "C", Status = FacilityStatus.Pending });
        dbContext.SaveChanges();

        return dbContext;
    }

    public static ApplicationUser AddUser(
        BloodLinkDbContext dbContext,
        string id,
        string roleName,
        Guid? facilityId,
        bool isActive = true)
    {
        var user = new ApplicationUser
        {
            Id = id,
            UserName = $"{id}@example.test",
            Email = $"{id}@example.test",
            FacilityId = facilityId,
            IsActive = isActive
        };

        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(new IdentityUserRole<string> { UserId = id, RoleId = roleName });
        dbContext.SaveChanges();

        return user;
    }

    public static BloodNeed AddNeed(
        BloodLinkDbContext dbContext,
        Guid facilityId,
        string requestedByUserId,
        BloodNeedStatus status = BloodNeedStatus.PendingReview,
        int units = 4)
    {
        var need = new BloodNeed
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            RequestedByUserId = requestedByUserId,
            BloodType = BloodType.ONegative,
            UnitsNeeded = units,
            Urgency = UrgencyLevel.Urgent,
            NeededByUtc = DateTime.UtcNow.AddHours(8),
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.BloodNeeds.Add(need);
        dbContext.SaveChanges();
        return need;
    }

    public static BloodRequest AddRequest(
        BloodLinkDbContext dbContext,
        Guid needId,
        Guid requestingFacilityId,
        Guid sourceFacilityId,
        BloodRequestStatus status = BloodRequestStatus.Sent,
        int unitsRequested = 3,
        int? unitsAccepted = null,
        string requestedByAdminId = "admin-a")
    {
        var bloodRequest = new BloodRequest
        {
            Id = Guid.NewGuid(),
            BloodNeedId = needId,
            RequestingFacilityId = requestingFacilityId,
            SourceFacilityId = sourceFacilityId,
            BloodType = BloodType.ONegative,
            UnitsRequested = unitsRequested,
            UnitsAccepted = unitsAccepted,
            Status = status,
            RequestedByAdminId = requestedByAdminId,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.BloodRequests.Add(bloodRequest);
        dbContext.SaveChanges();
        return bloodRequest;
    }
}

internal sealed class FakeCurrentUserService : ICurrentUserService
{
    public string? UserId { get; set; }
    public bool IsAuthenticated { get; set; } = true;
    public List<string> RoleList { get; } = [];
    public IReadOnlyCollection<string> Roles => RoleList;
    public Guid? FacilityId { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsInRole(string roleName) => RoleList.Contains(roleName);
    public bool BelongsToFacility(Guid facilityId) => FacilityId == facilityId;
}

internal sealed class FakeInventoryService : IInventoryService
{
    public int ReserveCalls { get; private set; }
    public int ReleaseCalls { get; private set; }
    public int FulfilCalls { get; private set; }
    public bool FailReserve { get; set; }
    public bool FailRelease { get; set; }
    public bool FailFulfil { get; set; }

    public Task<IReadOnlyList<InventoryItemDto>> GetOwnInventoryAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InventoryItemDto>>([]);

    public Task AdjustInventoryAsync(InventoryAdjustmentRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionHistoryAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InventoryTransactionDto>>([]);

    public Task<IReadOnlyList<LowStockAlertDto>> GetLowStockAlertsAsync(LowStockQueryRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LowStockAlertDto>>([]);

    public Task<IReadOnlyList<AvailabilityResultDto>> SearchAvailabilityAsync(AvailabilitySearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AvailabilityResultDto>>([]);

    public Task ReserveForRequestAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        ReserveCalls++;
        return FailReserve ? Task.FromException(new InvalidOperationException("reserve failed")) : Task.CompletedTask;
    }

    public Task ReleaseReservationAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        ReleaseCalls++;
        return FailRelease ? Task.FromException(new InvalidOperationException("release failed")) : Task.CompletedTask;
    }

    public Task FulfilTransferAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        FulfilCalls++;
        return FailFulfil ? Task.FromException(new InvalidOperationException("fulfil failed")) : Task.CompletedTask;
    }
}
