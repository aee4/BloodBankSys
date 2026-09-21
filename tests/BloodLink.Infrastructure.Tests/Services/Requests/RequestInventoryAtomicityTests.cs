using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Domain.Exceptions;
using BloodLink.Infrastructure.Services.Inventory;
using BloodLink.Infrastructure.Services.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace BloodLink.Infrastructure.Tests.Services.Requests;

public sealed class RequestInventoryAtomicityTests
{
    [Fact]
    public async Task PartialAcceptance_Cancellation_ReleasesExactlyAcceptedUnits()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var (request, inventory, service) = CreateScenario(dbContext, totalUnits: 10);

        await service.AcceptAsync(new RequestResponseRequest(request.Id, 6, "Partial supply"));

        Assert.Equal(6, inventory.ReservedUnits);
        Assert.Equal(6, request.UnitsAccepted);
        Assert.Equal(BloodRequestStatus.Accepted, request.Status);
        Assert.Contains(dbContext.InventoryTransactions, item =>
            item.TransactionType == InventoryTransactionType.Reserve &&
            item.ReservedUnitsChange == 6 && item.ReservedAfter == 6);

        await service.CancelAsync(request.Id);

        var storedInventory = await dbContext.BloodInventory.SingleAsync(item => item.Id == inventory.Id);
        Assert.Equal(0, storedInventory.ReservedUnits);
        Assert.Equal(10, storedInventory.TotalUnits);
        Assert.Equal(BloodRequestStatus.Cancelled, request.Status);
        Assert.Contains(dbContext.InventoryTransactions, item =>
            item.TransactionType == InventoryTransactionType.Release &&
            item.ReservedUnitsChange == -6 && item.ReservedAfter == 0);
        Assert.Equal(2, dbContext.AuditLogs.Count(item => item.EntityId == request.Id));
    }

    [Fact]
    public async Task PartialAcceptance_Fulfilment_TransfersExactlyAcceptedUnits()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var (request, sourceInventory, service) = CreateScenario(dbContext, totalUnits: 10);

        await service.AcceptAsync(new RequestResponseRequest(request.Id, 6, null));
        await service.FulfilAsync(new FulfilRequestRequest(request.Id, "Handover confirmed"));

        var destinationInventory = await dbContext.BloodInventory.SingleAsync(item =>
            item.FacilityId == WorkflowTestSupport.FacilityAId && item.BloodType == BloodType.ONegative);
        var need = await dbContext.BloodNeeds.SingleAsync(item => item.Id == request.BloodNeedId);

        Assert.Equal(4, sourceInventory.TotalUnits);
        Assert.Equal(0, sourceInventory.ReservedUnits);
        Assert.Equal(6, destinationInventory.TotalUnits);
        Assert.Equal(BloodRequestStatus.Fulfilled, request.Status);
        Assert.Equal(BloodNeedStatus.FulfilledExternally, need.Status);
        Assert.Contains(dbContext.InventoryTransactions, item =>
            item.TransactionType == InventoryTransactionType.TransferOut &&
            item.TotalUnitsChange == -6 && item.ReservedUnitsChange == -6 &&
            item.TotalAfter == 4 && item.ReservedAfter == 0);
        Assert.Contains(dbContext.InventoryTransactions, item =>
            item.TransactionType == InventoryTransactionType.TransferIn &&
            item.TotalUnitsChange == 6 && item.TotalAfter == 6);
    }

    [Fact]
    public async Task PartialAcceptance_RejectsWhenAcceptedUnitsExceedAvailableStock()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var (request, inventory, service) = CreateScenario(dbContext, totalUnits: 5);

        await Assert.ThrowsAsync<InsufficientInventoryException>(() =>
            service.AcceptAsync(new RequestResponseRequest(request.Id, 6, null)));

        Assert.Equal(0, inventory.ReservedUnits);
        Assert.Equal(BloodRequestStatus.Sent, request.Status);
        Assert.Empty(dbContext.InventoryTransactions.Where(item => item.ReferenceId == request.Id));
        Assert.Empty(dbContext.AuditLogs.Where(item => item.EntityId == request.Id));
    }

    [Fact]
    public async Task DuplicateTransitions_DoNotMutateInventoryTwice()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var (request, inventory, service) = CreateScenario(dbContext, totalUnits: 10);

        await service.AcceptAsync(new RequestResponseRequest(request.Id, 6, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcceptAsync(new RequestResponseRequest(request.Id, 6, null)));
        Assert.Equal(6, inventory.ReservedUnits);

        await service.CancelAsync(request.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(request.Id));
        var storedInventory = await dbContext.BloodInventory.SingleAsync(item => item.Id == inventory.Id);
        Assert.Equal(0, storedInventory.ReservedUnits);
        Assert.Equal(10, storedInventory.TotalUnits);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("cancel")]
    [InlineData("fulfil")]
    public async Task FailedSave_RollsBackEntireTransition(string transition)
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        var interceptor = new FailingSaveInterceptor();
        var options = new DbContextOptionsBuilder<BloodLink.Infrastructure.Data.BloodLinkDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .AddInterceptors(interceptor)
            .Options;

        Guid requestId;
        Guid inventoryId;
        Guid needId;
        await using (var dbContext = WorkflowTestSupport.CreateDbContext(options))
        {
            var (request, inventory, service) = CreateScenario(dbContext, totalUnits: 10);
            requestId = request.Id;
            inventoryId = inventory.Id;
            needId = request.BloodNeedId;

            if (transition is "cancel" or "fulfil")
            {
                await service.AcceptAsync(new RequestResponseRequest(request.Id, 6, null));
            }

            interceptor.Fail = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => transition switch
            {
                "accept" => service.AcceptAsync(new RequestResponseRequest(request.Id, 6, null)),
                "cancel" => service.CancelAsync(request.Id),
                _ => service.FulfilAsync(new FulfilRequestRequest(request.Id, null))
            });
        }

        await using var verificationContext = new BloodLink.Infrastructure.Data.BloodLinkDbContext(options);
        var storedRequest = await verificationContext.BloodRequests.SingleAsync(item => item.Id == requestId);
        var storedInventory = await verificationContext.BloodInventory.SingleAsync(item => item.Id == inventoryId);
        var storedNeed = await verificationContext.BloodNeeds.SingleAsync(item => item.Id == needId);

        if (transition == "accept")
        {
            Assert.Equal(BloodRequestStatus.Sent, storedRequest.Status);
            Assert.Equal(0, storedInventory.ReservedUnits);
            Assert.DoesNotContain(verificationContext.InventoryTransactions, item => item.ReferenceId == requestId);
            Assert.DoesNotContain(verificationContext.AuditLogs, item => item.EntityId == requestId);
        }
        else
        {
            Assert.Equal(BloodRequestStatus.Accepted, storedRequest.Status);
            Assert.Equal(6, storedInventory.ReservedUnits);
            Assert.Equal(10, storedInventory.TotalUnits);
            Assert.Equal(BloodNeedStatus.Searching, storedNeed.Status);
            Assert.Single(verificationContext.AuditLogs.Where(item => item.EntityId == requestId));
        }
    }

    private static (BloodRequest Request, BloodInventory Inventory, BloodRequestService Service) CreateScenario(
        BloodLink.Infrastructure.Data.BloodLinkDbContext dbContext,
        int totalUnits)
    {
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching, 10);
        var request = WorkflowTestSupport.AddRequest(
            dbContext,
            need.Id,
            WorkflowTestSupport.FacilityAId,
            WorkflowTestSupport.FacilityBId,
            unitsRequested: 10);
        var inventory = new BloodInventory
        {
            Id = Guid.NewGuid(),
            FacilityId = WorkflowTestSupport.FacilityBId,
            BloodType = BloodType.ONegative,
            TotalUnits = totalUnits,
            ReservedUnits = 0,
            LowStockThreshold = 2,
            UpdatedAtUtc = DateTime.UtcNow
        };
        dbContext.BloodInventory.Add(inventory);
        dbContext.SaveChanges();

        var currentUser = new FakeCurrentUserService
        {
            UserId = "admin-b",
            FacilityId = WorkflowTestSupport.FacilityBId
        };
        currentUser.RoleList.Add(RoleNames.FacilityAdmin);
        var inventoryService = new InventoryService(dbContext, currentUser);
        return (request, inventory, new BloodRequestService(dbContext, currentUser, inventoryService));
    }

    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public bool Fail { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            Fail
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Injected save failure."))
                : ValueTask.FromResult(result);
    }
}
