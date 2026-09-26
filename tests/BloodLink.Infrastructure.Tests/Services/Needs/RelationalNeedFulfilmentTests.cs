using System.Data.Common;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using BloodLink.Infrastructure.Services.Inventory;
using BloodLink.Infrastructure.Services.Needs;
using BloodLink.Infrastructure.Services.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace BloodLink.Infrastructure.Tests.Services.Needs;

public sealed class RelationalNeedFulfilmentTests
{
    private static readonly Guid FacilityId = Guid.Parse("41111111-1111-1111-1111-111111111111");
    private static readonly Guid FacilityBId = Guid.Parse("42222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task InternalFulfilment_CommitsInventoryAndEvidenceTogether()
    {
        await using var database = await CreateDatabaseAsync();
        var admin = Admin();
        await using var db = database.CreateContext();
        var service = new BloodNeedService(db, admin, new InventoryService(db, admin));

        await service.FulfilInternallyAsync(new NeedDecisionRequest(database.NeedId, "Supplied from local stock."));

        var need = await db.BloodNeeds.SingleAsync(item => item.Id == database.NeedId);
        var stock = await db.BloodInventory.SingleAsync(item => item.Id == database.InventoryId);
        var transaction = await db.InventoryTransactions.SingleAsync();
        Assert.Equal(BloodNeedStatus.FulfilledInternally, need.Status);
        Assert.Equal((7, 4), (stock.TotalUnits, stock.ReservedUnits));
        Assert.Equal((-5, 12, 4, 7, 4), (transaction.TotalUnitsChange, transaction.TotalBefore,
            transaction.ReservedBefore, transaction.TotalAfter, transaction.ReservedAfter));
        Assert.Single(await db.BloodNeedStatusHistory.ToListAsync(), item => item.BloodNeedId == database.NeedId);
        Assert.Single(await db.AuditLogs.ToListAsync(), item => item.EntityId == database.NeedId);
        Assert.Single(await db.Notifications.ToListAsync(), item => item.RecipientUserId == "staff-user");
    }

    [Fact]
    public async Task InternalFulfilment_RollsBackEarlierSqlWritesWhenLaterWriteFails()
    {
        var interceptor = new FailAfterFirstWriteInterceptor();
        await using var database = await CreateDatabaseAsync(interceptor);
        await using (var db = database.CreateContext())
        {
            var admin = Admin();
            var service = new BloodNeedService(db, admin, new InventoryService(db, admin));
            interceptor.Fail = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.FulfilInternallyAsync(new NeedDecisionRequest(database.NeedId, null)));
        }

        interceptor.Fail = false;
        await using var verify = database.CreateContext();
        Assert.Equal(BloodNeedStatus.PendingReview,
            (await verify.BloodNeeds.SingleAsync(item => item.Id == database.NeedId)).Status);
        var stock = await verify.BloodInventory.SingleAsync(item => item.Id == database.InventoryId);
        Assert.Equal((12, 4), (stock.TotalUnits, stock.ReservedUnits));
        Assert.Empty(await verify.InventoryTransactions.ToListAsync());
        Assert.Empty(await verify.BloodNeedStatusHistory.ToListAsync());
        Assert.Empty(await verify.AuditLogs.ToListAsync());
        Assert.Empty(await verify.Notifications.ToListAsync());
    }

    [Fact]
    public async Task CompetingInternalFulfilments_CannotConsumeTheSameInventoryVersionTwice()
    {
        await using var database = await CreateDatabaseAsync();
        var gate = new MutationGate();
        await using var firstDb = database.CreateContext();
        await using var secondDb = database.CreateContext();
        var firstAdmin = Admin();
        var secondAdmin = Admin();
        var firstInventory = new InventoryService(firstDb, firstAdmin);
        var secondInventory = new InventoryService(secondDb, secondAdmin);
        var first = new BloodNeedService(firstDb, firstAdmin, new PausingInventoryService(firstInventory, gate));
        var second = new BloodNeedService(secondDb, secondAdmin, new PausingInventoryService(secondInventory, gate));

        var outcomes = await Task.WhenAll(
            RunAsync(first, database.NeedId),
            RunAsync(second, database.NeedId));

        Assert.Single(outcomes, exception => exception is null);
        Assert.Single(outcomes, exception => exception is BloodLink.Domain.Exceptions.ConcurrencyException);
        await using var verify = database.CreateContext();
        Assert.Equal(BloodNeedStatus.FulfilledInternally,
            (await verify.BloodNeeds.SingleAsync(item => item.Id == database.NeedId)).Status);
        var stock = await verify.BloodInventory.SingleAsync(item => item.Id == database.InventoryId);
        Assert.Equal((7, 4), (stock.TotalUnits, stock.ReservedUnits));
        Assert.Single(await verify.InventoryTransactions.ToListAsync());
        Assert.Single(await verify.BloodNeedStatusHistory.ToListAsync());
        Assert.Single(await verify.AuditLogs.ToListAsync(), item => item.EntityId == database.NeedId);
    }

    [Fact]
    public async Task ExternalRequest_RechecksAvailabilityAndPartialCancellationReleasesExactlyAcceptedUnits()
    {
        await using var database = await CreateDatabaseAsync();
        await SetNeedSearchingAsync(database);
        await using var db = database.CreateContext();
        var requester = Admin("admin-user", FacilityId);
        var requests = new BloodRequestService(db, requester, new InventoryService(db, requester));
        var sourceStock = await db.BloodInventory.SingleAsync(item => item.Id == database.SourceInventoryId);
        sourceStock.TotalUnits = 5;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BloodLink.Domain.Exceptions.InsufficientInventoryException>(() =>
            requests.CreateFromNeedAsync(new CreateBloodRequestRequest(database.NeedId, FacilityBId, 10, null)));
        Assert.Empty(await db.BloodRequests.ToListAsync());
        sourceStock.TotalUnits = 10;
        await db.SaveChangesAsync();
        var request = await requests.CreateFromNeedAsync(new CreateBloodRequestRequest(database.NeedId, FacilityBId, 10, null));
        Assert.Equal(0, (await db.BloodInventory.SingleAsync(item => item.Id == database.SourceInventoryId)).ReservedUnits);

        var source = Admin("source-admin", FacilityBId);
        var sourceService = new BloodRequestService(db, source, new InventoryService(db, source));
        await sourceService.AcceptAsync(new RequestResponseRequest(request.Id, 6, "Partial supply."));
        Assert.Equal(6, (await db.BloodInventory.SingleAsync(item => item.Id == database.SourceInventoryId)).ReservedUnits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => requests.CancelAsync(request.Id));
        await sourceService.CancelAsync(request.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sourceService.CancelAsync(request.Id));

        var inventory = await db.BloodInventory.SingleAsync(item => item.Id == database.SourceInventoryId);
        Assert.Equal((10, 0), (inventory.TotalUnits, inventory.ReservedUnits));
        Assert.Equal(BloodRequestStatus.Cancelled, (await requests.GetAsync(request.Id))!.Status);
        var transactions = await db.InventoryTransactions.Where(item => item.ReferenceId == request.Id).ToListAsync();
        Assert.Equal(2, transactions.Count);
        Assert.Contains(transactions, item => item.TransactionType == InventoryTransactionType.Reserve
            && item.ReservedUnitsChange == 6 && item.ReservedAfter == 6);
        Assert.Contains(transactions, item => item.TransactionType == InventoryTransactionType.Release
            && item.ReservedUnitsChange == -6 && item.ReservedAfter == 0);
        var release = Assert.Single(transactions, item => item.TransactionType == InventoryTransactionType.Release);
        Assert.Equal((10, 6, 10, 0), (release.TotalBefore, release.ReservedBefore, release.TotalAfter, release.ReservedAfter));
        Assert.Single(await db.BloodRequestStatusHistory.Where(item => item.BloodRequestId == request.Id
            && item.ToStatus == BloodRequestStatus.Cancelled).ToListAsync());
        Assert.Single(await db.AuditLogs.Where(item => item.EntityId == request.Id
            && item.Action == "BloodRequestCancelled").ToListAsync());
        Assert.Single(await db.Notifications.Where(item => item.Title == "Blood request cancelled"
            && item.RecipientUserId == "admin-user").ToListAsync());
        Assert.Equal(BloodNeedStatus.Searching, (await db.BloodNeeds.SingleAsync(item => item.Id == database.NeedId)).Status);
    }

    [Fact]
    public async Task Cancellation_RequestingAdminCannotCancelSentOrAcceptedRequest()
    {
        await using var database = await CreateDatabaseAsync();
        await SetNeedSearchingAsync(database);
        await using var db = database.CreateContext();
        var requester = Admin("admin-user", FacilityId);
        var source = Admin("source-admin", FacilityBId);
        var requesterService = new BloodRequestService(db, requester, new InventoryService(db, requester));
        var sourceService = new BloodRequestService(db, source, new InventoryService(db, source));
        var sent = await requesterService.CreateFromNeedAsync(new CreateBloodRequestRequest(database.NeedId, FacilityBId, 5, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => requesterService.CancelAsync(sent.Id));
        await sourceService.AcceptAsync(new RequestResponseRequest(sent.Id, 3, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => requesterService.CancelAsync(sent.Id));

        var request = await db.BloodRequests.SingleAsync(item => item.Id == sent.Id);
        Assert.Equal(BloodRequestStatus.Accepted, request.Status);
        Assert.Equal(3, (await db.BloodInventory.SingleAsync(item => item.Id == database.SourceInventoryId)).ReservedUnits);
        Assert.Empty(await db.BloodRequestStatusHistory.Where(item => item.BloodRequestId == sent.Id
            && item.ToStatus == BloodRequestStatus.Cancelled).ToListAsync());
        Assert.Empty(await db.AuditLogs.Where(item => item.EntityId == sent.Id
            && item.Action == "BloodRequestCancelled").ToListAsync());
        Assert.Empty(await db.InventoryTransactions.Where(item => item.ReferenceId == sent.Id
            && item.TransactionType == InventoryTransactionType.Release).ToListAsync());
    }

    [Fact]
    public async Task Cancellation_SourceAdminCancelsSentWithoutChangingInventory()
    {
        await using var database = await CreateDatabaseAsync();
        await SetNeedSearchingAsync(database);
        await using var db = database.CreateContext();
        var requester = Admin("admin-user", FacilityId);
        var source = Admin("source-admin", FacilityBId);
        var requesterService = new BloodRequestService(db, requester, new InventoryService(db, requester));
        var sourceService = new BloodRequestService(db, source, new InventoryService(db, source));
        var request = await requesterService.CreateFromNeedAsync(new CreateBloodRequestRequest(database.NeedId, FacilityBId, 5, null));

        await sourceService.CancelAsync(request.Id);

        Assert.Equal(BloodRequestStatus.Cancelled, (await db.BloodRequests.SingleAsync(item => item.Id == request.Id)).Status);
        Assert.Equal((10, 0), ((await db.BloodInventory.SingleAsync(item => item.Id == database.SourceInventoryId)).TotalUnits,
            (await db.BloodInventory.SingleAsync(item => item.Id == database.SourceInventoryId)).ReservedUnits));
        Assert.Empty(await db.InventoryTransactions.Where(item => item.ReferenceId == request.Id).ToListAsync());
        Assert.Single(await db.BloodRequestStatusHistory.Where(item => item.BloodRequestId == request.Id
            && item.ToStatus == BloodRequestStatus.Cancelled).ToListAsync());
        Assert.Single(await db.AuditLogs.Where(item => item.EntityId == request.Id
            && item.Action == "BloodRequestCancelled").ToListAsync());
        Assert.Single(await db.Notifications.Where(item => item.Title == "Blood request cancelled"
            && item.RecipientUserId == "admin-user").ToListAsync());
    }

    [Fact]
    public async Task Cancellation_InjectedFailureRollsBackReservationAndAllEvidence()
    {
        var interceptor = new FailAfterFirstWriteInterceptor();
        await using var database = await CreateDatabaseAsync(interceptor);
        await SetNeedSearchingAsync(database);
        Guid requestId;
        int historyCount;
        int auditCount;
        int notificationCount;
        await using (var db = database.CreateContext())
        {
            var requester = Admin("admin-user", FacilityId);
            var source = Admin("source-admin", FacilityBId);
            var requesterService = new BloodRequestService(db, requester, new InventoryService(db, requester));
            var sourceService = new BloodRequestService(db, source, new InventoryService(db, source));
            var request = await requesterService.CreateFromNeedAsync(new CreateBloodRequestRequest(database.NeedId, FacilityBId, 5, null));
            requestId = request.Id;
            await sourceService.AcceptAsync(new RequestResponseRequest(request.Id, 3, null));
            historyCount = await db.BloodRequestStatusHistory.CountAsync(item => item.BloodRequestId == request.Id);
            auditCount = await db.AuditLogs.CountAsync(item => item.EntityId == request.Id);
            notificationCount = await db.Notifications.CountAsync();

            interceptor.Fail = true;
            await Assert.ThrowsAsync<DbUpdateException>(() => sourceService.CancelAsync(request.Id));
        }

        interceptor.Fail = false;
        await using var verify = database.CreateContext();
        Assert.Equal(BloodRequestStatus.Accepted, (await verify.BloodRequests.SingleAsync(item => item.Id == requestId)).Status);
        Assert.Equal(3, (await verify.BloodInventory.SingleAsync(item => item.Id == database.SourceInventoryId)).ReservedUnits);
        Assert.Equal(historyCount, await verify.BloodRequestStatusHistory.CountAsync(item => item.BloodRequestId == requestId));
        Assert.Equal(auditCount, await verify.AuditLogs.CountAsync(item => item.EntityId == requestId));
        Assert.Equal(notificationCount, await verify.Notifications.CountAsync());
        Assert.Single(await verify.InventoryTransactions.Where(item => item.ReferenceId == requestId
            && item.TransactionType == InventoryTransactionType.Reserve).ToListAsync());
        Assert.Empty(await verify.InventoryTransactions.Where(item => item.ReferenceId == requestId
            && item.TransactionType == InventoryTransactionType.Release).ToListAsync());
    }

    [Fact]
    public async Task Cancellation_CompetingSourceAdminsCannotReleaseReservationTwice()
    {
        await using var database = await CreateDatabaseAsync();
        await SetNeedSearchingAsync(database);
        Guid requestId;
        await using (var db = database.CreateContext())
        {
            var requester = Admin("admin-user", FacilityId);
            var source = Admin("source-admin", FacilityBId);
            var requesterService = new BloodRequestService(db, requester, new InventoryService(db, requester));
            var sourceService = new BloodRequestService(db, source, new InventoryService(db, source));
            var request = await requesterService.CreateFromNeedAsync(new CreateBloodRequestRequest(database.NeedId, FacilityBId, 5, null));
            requestId = request.Id;
            await sourceService.AcceptAsync(new RequestResponseRequest(request.Id, 3, null));
        }

        var gate = new MutationGate();
        await using var firstDb = database.CreateContext();
        await using var secondDb = database.CreateContext();
        var firstAdmin = Admin("source-admin", FacilityBId);
        var secondAdmin = Admin("source-admin", FacilityBId);
        var firstService = new BloodRequestService(firstDb, firstAdmin,
            new PausingInventoryService(new InventoryService(firstDb, firstAdmin), gate, pauseRelease: true));
        var secondService = new BloodRequestService(secondDb, secondAdmin,
            new PausingInventoryService(new InventoryService(secondDb, secondAdmin), gate, pauseRelease: true));

        var outcomes = await Task.WhenAll(CancelAsync(firstService, requestId), CancelAsync(secondService, requestId));

        Assert.Single(outcomes, exception => exception is null);
        Assert.Single(outcomes, exception => exception is BloodLink.Domain.Exceptions.ConcurrencyException);
        await using var verify = database.CreateContext();
        Assert.Equal(BloodRequestStatus.Cancelled, (await verify.BloodRequests.SingleAsync(item => item.Id == requestId)).Status);
        Assert.Equal(0, (await verify.BloodInventory.SingleAsync(item => item.Id == database.SourceInventoryId)).ReservedUnits);
        Assert.Single(await verify.InventoryTransactions.Where(item => item.ReferenceId == requestId
            && item.TransactionType == InventoryTransactionType.Release).ToListAsync());
        Assert.Equal(1, await verify.BloodRequestStatusHistory.CountAsync(item => item.BloodRequestId == requestId
            && item.ToStatus == BloodRequestStatus.Cancelled));
        Assert.Equal(1, await verify.AuditLogs.CountAsync(item => item.EntityId == requestId
            && item.Action == "BloodRequestCancelled"));
        Assert.Single(await verify.Notifications.Where(item => item.Title == "Blood request cancelled").ToListAsync());
    }

    private static async Task<Exception?> CancelAsync(IBloodRequestService service, Guid requestId)
    {
        try
        {
            await service.CancelAsync(requestId);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    [Fact]
    public async Task ExternalRequest_FulfilmentTransfersExactlyPartiallyAcceptedUnits()
    {
        await using var database = await CreateDatabaseAsync();
        await SetNeedSearchingAsync(database);
        await using var db = database.CreateContext();
        var requester = Admin("admin-user", FacilityId);
        var requestService = new BloodRequestService(db, requester, new InventoryService(db, requester));
        var request = await requestService.CreateFromNeedAsync(new CreateBloodRequestRequest(database.NeedId, FacilityBId, 10, null));
        var source = Admin("source-admin", FacilityBId);
        var sourceService = new BloodRequestService(db, source, new InventoryService(db, source));
        await sourceService.AcceptAsync(new RequestResponseRequest(request.Id, 6, null));
        await sourceService.FulfilAsync(new FulfilRequestRequest(request.Id, "Handover complete."));

        var sourceInventory = await db.BloodInventory.SingleAsync(item => item.Id == database.SourceInventoryId);
        var destinationInventory = await db.BloodInventory.SingleAsync(item =>
            item.FacilityId == FacilityId && item.BloodType == BloodType.ONegative);
        var transferOut = await db.InventoryTransactions.SingleAsync(item => item.TransactionType == InventoryTransactionType.TransferOut);
        var transferIn = await db.InventoryTransactions.SingleAsync(item => item.TransactionType == InventoryTransactionType.TransferIn);
        Assert.Equal((4, 0), (sourceInventory.TotalUnits, sourceInventory.ReservedUnits));
        Assert.Equal(18, destinationInventory.TotalUnits);
        Assert.Equal((-6, 10, 6, 4, 0), (transferOut.TotalUnitsChange, transferOut.TotalBefore, transferOut.ReservedBefore,
            transferOut.TotalAfter, transferOut.ReservedAfter));
        Assert.Equal((6, 12, 4, 18, 4), (transferIn.TotalUnitsChange, transferIn.TotalBefore, transferIn.ReservedBefore,
            transferIn.TotalAfter, transferIn.ReservedAfter));
        Assert.Equal(BloodRequestStatus.Fulfilled, (await requestService.GetAsync(request.Id))!.Status);
        Assert.Equal(BloodNeedStatus.FulfilledExternally,
            (await db.BloodNeeds.SingleAsync(item => item.Id == database.NeedId)).Status);
    }

    private static async Task<Exception?> RunAsync(IBloodNeedService service, Guid needId)
    {
        try
        {
            await service.FulfilInternallyAsync(new NeedDecisionRequest(needId, null));
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static FakeCurrentUserService Admin()
    {
        return new FakeCurrentUserService { UserId = "admin-user", FacilityId = FacilityId };
    }

    private static FakeCurrentUserService Admin(string userId, Guid facilityId) =>
        new() { UserId = userId, FacilityId = facilityId };

    private static async Task SetNeedSearchingAsync(RelationalDatabase database)
    {
        await using var db = database.CreateContext();
        var need = await db.BloodNeeds.SingleAsync(item => item.Id == database.NeedId);
        need.UnitsNeeded = 10;
        need.Status = BloodNeedStatus.Searching;
        await db.SaveChangesAsync();
    }

    private static async Task<RelationalDatabase> CreateDatabaseAsync(IInterceptor? interceptor = null)
    {
        var baseConnection = Environment.GetEnvironmentVariable("BLOODLINK_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            throw new InvalidOperationException("Set BLOODLINK_TEST_SQLSERVER to run disposable SQL Server workflow tests.");
        }

        var connection = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = $"BloodLink_Phase5CA_{Guid.NewGuid():N}"
        };
        var database = new RelationalDatabase(connection.ConnectionString, interceptor);
        await using var db = database.CreateContext();
        await db.Database.MigrateAsync();
        await SeedAsync(db);
        return database;
    }

    private static async Task SeedAsync(BloodLinkDbContext db)
    {
        db.Roles.AddRange(
            new IdentityRole(RoleNames.FacilityAdmin) { Id = RoleNames.FacilityAdmin, NormalizedName = RoleNames.FacilityAdmin.ToUpperInvariant() },
            new IdentityRole(RoleNames.FacilityStaff) { Id = RoleNames.FacilityStaff, NormalizedName = RoleNames.FacilityStaff.ToUpperInvariant() },
            new IdentityRole(RoleNames.SystemAdmin) { Id = RoleNames.SystemAdmin, NormalizedName = RoleNames.SystemAdmin.ToUpperInvariant() });
        db.Facilities.Add(new Facility
        {
            Id = FacilityId,
            Name = "Relational Facility",
            RegistrationNumber = "RELATIONAL-5CA",
            Status = FacilityStatus.Approved,
            Region = "Region",
            City = "City",
            Address = "Address",
            ContactEmail = "facility@example.test",
            ContactPhone = "0000000000",
            CreatedByUserId = "system-user"
        });
        db.Facilities.Add(new Facility
        {
            Id = FacilityBId,
            Name = "Relational Source",
            RegistrationNumber = "RELATIONAL-5CB",
            Status = FacilityStatus.Approved,
            Region = "Region",
            City = "City",
            Address = "Address",
            ContactEmail = "source@example.test",
            ContactPhone = "0000000000",
            CreatedByUserId = "system-user"
        });
        db.Users.AddRange(
            NewUser("admin-user", "Admin", FacilityId),
            NewUser("staff-user", "Staff", FacilityId),
            NewUser("source-admin", "Source", FacilityBId));
        db.UserRoles.AddRange(
            new IdentityUserRole<string> { UserId = "admin-user", RoleId = RoleNames.FacilityAdmin },
            new IdentityUserRole<string> { UserId = "staff-user", RoleId = RoleNames.FacilityStaff },
            new IdentityUserRole<string> { UserId = "source-admin", RoleId = RoleNames.FacilityAdmin });
        db.BloodNeeds.Add(new BloodNeed
        {
            Id = RelationalDatabase.NeedIdValue,
            FacilityId = FacilityId,
            RequestedByUserId = "staff-user",
            BloodType = BloodType.ONegative,
            UnitsNeeded = 5,
            Urgency = UrgencyLevel.Urgent,
            NeededByUtc = DateTime.UtcNow.AddDays(1),
            Status = BloodNeedStatus.PendingReview,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.BloodInventory.Add(new BloodInventory
        {
            Id = RelationalDatabase.InventoryIdValue,
            FacilityId = FacilityId,
            BloodType = BloodType.ONegative,
            TotalUnits = 12,
            ReservedUnits = 4,
            LowStockThreshold = 1,
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.BloodInventory.Add(new BloodInventory
        {
            Id = RelationalDatabase.SourceInventoryIdValue,
            FacilityId = FacilityBId,
            BloodType = BloodType.ONegative,
            TotalUnits = 10,
            ReservedUnits = 0,
            LowStockThreshold = 1,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static ApplicationUser NewUser(string id, string firstName, Guid facilityId) => new()
    {
        Id = id,
        UserName = $"{id}@example.test",
        NormalizedUserName = $"{id}@example.test".ToUpperInvariant(),
        Email = $"{id}@example.test",
        NormalizedEmail = $"{id}@example.test".ToUpperInvariant(),
        SecurityStamp = Guid.NewGuid().ToString(),
        ConcurrencyStamp = Guid.NewGuid().ToString(),
        FirstName = firstName,
        LastName = "User",
        FacilityId = facilityId,
        IsActive = true
    };

    private sealed class RelationalDatabase(string connectionString, IInterceptor? interceptor) : IAsyncDisposable
    {
        public static readonly Guid NeedIdValue = Guid.NewGuid();
        public static readonly Guid InventoryIdValue = Guid.NewGuid();
        public static readonly Guid SourceInventoryIdValue = Guid.NewGuid();
        public Guid NeedId => NeedIdValue;
        public Guid InventoryId => InventoryIdValue;
        public Guid SourceInventoryId => SourceInventoryIdValue;

        public BloodLinkDbContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<BloodLinkDbContext>()
                .UseSqlServer(connectionString, sql => sql.MaxBatchSize(1));
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            return new BloodLinkDbContext(builder.Options);
        }

        public async ValueTask DisposeAsync()
        {
            await using var db = CreateContext();
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public string? UserId { get; init; }
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => [RoleNames.FacilityAdmin];
        public Guid? FacilityId { get; init; }
        public bool IsActive => true;
        public bool IsInRole(string roleName) => roleName == RoleNames.FacilityAdmin;
        public bool BelongsToFacility(Guid facilityId) => FacilityId == facilityId;
    }

    private sealed class MutationGate
    {
        private int arrivals;
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref arrivals) == 2) release.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class PausingInventoryService(IInventoryService inner, MutationGate gate, bool pauseRelease = false) : IInventoryService
    {
        public Task<IReadOnlyList<InventoryItemDto>> GetOwnInventoryAsync(CancellationToken token = default) => inner.GetOwnInventoryAsync(token);
        public Task AdjustInventoryAsync(InventoryAdjustmentRequest request, CancellationToken token = default) => inner.AdjustInventoryAsync(request, token);
        public Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionHistoryAsync(CancellationToken token = default) => inner.GetTransactionHistoryAsync(token);
        public Task<IReadOnlyList<LowStockAlertDto>> GetLowStockAlertsAsync(LowStockQueryRequest request, CancellationToken token = default) => inner.GetLowStockAlertsAsync(request, token);
        public Task<IReadOnlyList<AvailabilityResultDto>> SearchAvailabilityAsync(AvailabilitySearchRequest request, CancellationToken token = default) => inner.SearchAvailabilityAsync(request, token);
        public Task ReserveForRequestAsync(Guid requestId, int units, bool deferSave = false, CancellationToken token = default) => inner.ReserveForRequestAsync(requestId, units, deferSave, token);
        public async Task ConsumeForNeedAsync(Guid needId, BloodType type, int units, string reason, bool deferSave = false, CancellationToken token = default)
        {
            await inner.ConsumeForNeedAsync(needId, type, units, reason, deferSave, token);
            await gate.ArriveAsync(token);
        }
        public async Task ReleaseReservationAsync(Guid requestId, bool deferSave = false, CancellationToken token = default)
        {
            await inner.ReleaseReservationAsync(requestId, deferSave, token);
            if (pauseRelease) await gate.ArriveAsync(token);
        }
        public Task FulfilTransferAsync(Guid requestId, bool deferSave = false, CancellationToken token = default) => inner.FulfilTransferAsync(requestId, deferSave, token);
    }

    private sealed class FailAfterFirstWriteInterceptor : DbCommandInterceptor
    {
        private int writeCommands;
        public bool Fail { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) =>
            ShouldFail(command)
                ? ValueTask.FromException<InterceptionResult<DbDataReader>>(new InvalidOperationException("Injected relational write failure."))
                : base.ReaderExecutingAsync(command, eventData, result, cancellationToken);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ShouldFail(command)
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Injected relational write failure."))
                : base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);

        private bool ShouldFail(DbCommand command) => Fail
            && (command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                || command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                || command.CommandText.Contains("MERGE", StringComparison.OrdinalIgnoreCase))
            && Interlocked.Increment(ref writeCommands) > 1;
    }
}
