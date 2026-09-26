using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BloodLink.Infrastructure.Services.Needs;

public sealed class BloodNeedService(
    BloodLinkDbContext dbContext,
    ICurrentUserService currentUser,
    IInventoryService inventoryService) : IBloodNeedService
{
    public BloodNeedService(BloodLinkDbContext dbContext, ICurrentUserService currentUser)
        : this(dbContext, currentUser, new BloodLink.Infrastructure.Services.Inventory.InventoryService(dbContext, currentUser))
    {
    }

    private static readonly BloodRequestStatus[] ActiveRequestStatuses =
    [BloodRequestStatus.Sent, BloodRequestStatus.Accepted];

    public async Task<BloodNeedDto> CreateAsync(CreateBloodNeedRequest request, CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityStaff);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        WorkflowValidation.EnsurePositiveUnits(request.UnitsNeeded, nameof(request.UnitsNeeded));
        WorkflowValidation.EnsureCanonicalEnum(request.BloodType, nameof(request.BloodType));
        WorkflowValidation.EnsureCanonicalEnum(request.Urgency, nameof(request.Urgency));
        WorkflowValidation.EnsureSafeNote(request.Note);

        var nowUtc = DateTime.UtcNow;
        var neededByUtc = request.NeededByUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.NeededByUtc, DateTimeKind.Utc)
            : request.NeededByUtc.ToUniversalTime();
        if (neededByUtc <= nowUtc)
        {
            throw new ArgumentException("Needed-by time must be in the future.");
        }

        var need = new BloodNeed
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            RequestedByUserId = userId,
            BloodType = request.BloodType,
            UnitsNeeded = request.UnitsNeeded,
            Urgency = request.Urgency,
            NeededByUtc = neededByUtc,
            Note = TrimToNull(request.Note),
            Status = BloodNeedStatus.PendingReview,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        dbContext.BloodNeeds.Add(need);
        AddHistory(need, null, BloodNeedStatus.PendingReview, need.Note, userId, nowUtc);
        AddAudit(need, userId, "BloodNeedSubmitted", "Submitted internal blood need.", nowUtc);
        await WorkflowNotifications.AddForActiveFacilityAdminsAsync(
            dbContext, facilityId, NotificationType.NewNeed, "New internal blood need",
            "A staff member submitted a blood need for review.", nameof(BloodNeed), need.Id, nowUtc, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(need);
    }

    public async Task<BloodNeedDetailDto?> GetAsync(Guid bloodNeedId, CancellationToken cancellationToken = default)
    {
        var need = await AuthorizeReadAsync(bloodNeedId, cancellationToken);
        if (need is null)
        {
            return null;
        }

        var facility = await dbContext.Facilities.AsNoTracking()
            .SingleAsync(item => item.Id == need.FacilityId, cancellationToken);
        var creator = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == need.RequestedByUserId)
            .Select(user => new { user.FirstName, user.LastName, user.Email })
            .SingleOrDefaultAsync(cancellationToken);
        var inventory = await dbContext.BloodInventory.AsNoTracking()
            .Where(item => item.FacilityId == need.FacilityId && item.BloodType == need.BloodType)
            .Select(item => new { item.TotalUnits, item.ReservedUnits })
            .SingleOrDefaultAsync(cancellationToken);

        return new BloodNeedDetailDto(
            need.Id, need.FacilityId, facility.Name, need.BloodType, need.UnitsNeeded, need.Urgency, need.Status,
            need.NeededByUtc, need.Note, need.DecisionReason, DisplayName(creator?.FirstName, creator?.LastName, creator?.Email),
            need.CreatedAtUtc, need.UpdatedAtUtc, inventory?.TotalUnits, inventory?.ReservedUnits,
            inventory is null ? null : inventory.TotalUnits - inventory.ReservedUnits);
    }

    public async Task<IReadOnlyList<BloodNeedTimelineItemDto>> GetTimelineAsync(Guid bloodNeedId, CancellationToken cancellationToken = default)
    {
        if (await AuthorizeReadAsync(bloodNeedId, cancellationToken) is null)
        {
            return [];
        }

        return await (
                from history in dbContext.BloodNeedStatusHistory.AsNoTracking()
                join actor in dbContext.Users.AsNoTracking() on history.ChangedByUserId equals actor.Id
                where history.BloodNeedId == bloodNeedId
                orderby history.ChangedAtUtc, history.Id
                select new BloodNeedTimelineItemDto(
                    history.FromStatus,
                    history.ToStatus,
                    DisplayName(actor.FirstName, actor.LastName, actor.Email),
                    history.Note,
                    history.ChangedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BloodNeedDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityStaff);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);
        return await dbContext.BloodNeeds.AsNoTracking()
            .Where(need => need.FacilityId == facilityId && need.RequestedByUserId == userId)
            .OrderByDescending(need => need.CreatedAtUtc).ThenBy(need => need.Id)
            .Select(need => new BloodNeedDto(need.Id, need.FacilityId, need.BloodType, need.UnitsNeeded, need.Urgency, need.Status, need.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BloodNeedDto>> ListOwnFacilityAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);
        return await dbContext.BloodNeeds.AsNoTracking()
            .Where(need => need.FacilityId == facilityId)
            .OrderByDescending(need => need.CreatedAtUtc).ThenBy(need => need.Id)
            .Select(need => new BloodNeedDto(need.Id, need.FacilityId, need.BloodType, need.UnitsNeeded, need.Urgency, need.Status, need.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public Task StartSearchAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsFacilityAdminAsync(request, BloodNeedStatus.PendingReview, BloodNeedStatus.Searching, false, cancellationToken);

    public async Task FulfilInternallyAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var need = await LoadNeedForFacilityAdminAsync(request.BloodNeedId, cancellationToken);
        if (need.Status is not (BloodNeedStatus.PendingReview or BloodNeedStatus.Searching))
        {
            throw new InvalidOperationException("Only pending or searching needs may be fulfilled internally.");
        }
        if (need.UnitsNeeded <= 0)
        {
            throw new InvalidOperationException("The need has an invalid unit count.");
        }
        await EnsureNoActiveExternalRequestAsync(need, cancellationToken);

        await ExecuteAtomicAsync(async () =>
        {
            await inventoryService.ConsumeForNeedAsync(
                need.Id, need.BloodType, need.UnitsNeeded, "Internal fulfilment of blood need.", deferSave: true, cancellationToken);
            Transition(need, BloodNeedStatus.FulfilledInternally, request.Reason, userId, DateTime.UtcNow);
            NotifyNeedCreator(need, "Blood need fulfilled internally", "Your need was fulfilled from facility inventory.");
        }, cancellationToken);
    }

    public Task RejectAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsFacilityAdminAsync(request, BloodNeedStatus.PendingReview, BloodNeedStatus.Rejected, true, cancellationToken);

    public async Task CancelAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var need = await dbContext.BloodNeeds.SingleOrDefaultAsync(item => item.Id == request.BloodNeedId, cancellationToken)
            ?? throw new InvalidOperationException("The blood need was not found.");
        var isAdmin = currentUser.IsInRole(RoleNames.FacilityAdmin) && currentUser.BelongsToFacility(need.FacilityId);
        var isCreator = need.RequestedByUserId == userId && currentUser.IsInRole(RoleNames.FacilityStaff)
            && currentUser.BelongsToFacility(need.FacilityId);
        if (!isCreator && !isAdmin)
        {
            throw new UnauthorizedAccessException("You are not authorized to cancel this blood need.");
        }
        if (isCreator && need.Status != BloodNeedStatus.PendingReview && !isAdmin)
        {
            throw new InvalidOperationException("The creator may cancel only before admin action.");
        }
        if (need.Status is not (BloodNeedStatus.PendingReview or BloodNeedStatus.Searching))
        {
            throw new InvalidOperationException("This blood need cannot be cancelled from its current status.");
        }
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A cancellation reason is required.");
        }
        WorkflowValidation.EnsureSafeNote(request.Reason);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, need.FacilityId, cancellationToken);
        await EnsureNoActiveExternalRequestAsync(need, cancellationToken);

        await ExecuteAtomicAsync(async () =>
        {
            Transition(need, BloodNeedStatus.Cancelled, request.Reason, userId, DateTime.UtcNow);
            if (isCreator)
            {
                await WorkflowNotifications.AddForActiveFacilityAdminsAsync(
                    dbContext, need.FacilityId, NotificationType.FacilityDecision, "Blood need cancelled",
                    "A staff member cancelled a pending blood need.", nameof(BloodNeed), need.Id, DateTime.UtcNow, cancellationToken);
            }
            else
            {
                NotifyNeedCreator(need, "Blood need cancelled", "Your facility administrator cancelled the need.");
            }
        }, cancellationToken);
    }

    private async Task TransitionAsFacilityAdminAsync(
        NeedDecisionRequest request, BloodNeedStatus expectedStatus, BloodNeedStatus nextStatus,
        bool requiresReason, CancellationToken cancellationToken)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var need = await LoadNeedForFacilityAdminAsync(request.BloodNeedId, cancellationToken);
        if (need.Status != expectedStatus)
        {
            throw new InvalidOperationException($"Blood need must be {expectedStatus} to perform this action.");
        }
        if (requiresReason && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A reason is required.");
        }
        WorkflowValidation.EnsureSafeNote(request.Reason);

        await ExecuteAtomicAsync(() =>
        {
            Transition(need, nextStatus, request.Reason, userId, DateTime.UtcNow);
            var title = nextStatus == BloodNeedStatus.Searching ? "Blood need is being sourced" : "Blood need decision";
            var message = nextStatus == BloodNeedStatus.Searching
                ? "Your facility administrator began searching for an external source."
                : "Your facility administrator rejected the blood need.";
            NotifyNeedCreator(need, title, message);
            return Task.CompletedTask;
        }, cancellationToken);
    }

    private async Task<BloodNeed?> AuthorizeReadAsync(Guid bloodNeedId, CancellationToken cancellationToken)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var need = await dbContext.BloodNeeds.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == bloodNeedId, cancellationToken);
        if (need is null)
        {
            return null;
        }

        var isAdmin = currentUser.IsInRole(RoleNames.FacilityAdmin) && currentUser.BelongsToFacility(need.FacilityId);
        var isCreator = need.RequestedByUserId == userId && currentUser.IsInRole(RoleNames.FacilityStaff)
            && currentUser.BelongsToFacility(need.FacilityId);
        if (!isAdmin && !isCreator)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this blood need.");
        }
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, need.FacilityId, cancellationToken);
        return need;
    }

    private async Task<BloodNeed> LoadNeedForFacilityAdminAsync(Guid bloodNeedId, CancellationToken cancellationToken)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);
        var need = await dbContext.BloodNeeds.SingleOrDefaultAsync(item => item.Id == bloodNeedId, cancellationToken)
            ?? throw new InvalidOperationException("The blood need was not found.");
        if (need.FacilityId != facilityId)
        {
            throw new UnauthorizedAccessException("You are not authorized to act on this blood need.");
        }
        return need;
    }

    private async Task EnsureNoActiveExternalRequestAsync(BloodNeed need, CancellationToken cancellationToken)
    {
        if (need.Status == BloodNeedStatus.Searching && await dbContext.BloodRequests.AnyAsync(
                request => request.BloodNeedId == need.Id && ActiveRequestStatuses.Contains(request.Status), cancellationToken))
        {
            throw new InvalidOperationException("Resolve or cancel the active external request before changing this need.");
        }
    }

    private void NotifyNeedCreator(BloodNeed need, string title, string message)
    {
        WorkflowNotifications.AddForUsers(dbContext, [need.RequestedByUserId], NotificationType.FacilityDecision,
            title, message, nameof(BloodNeed), need.Id, DateTime.UtcNow);
    }

    private void Transition(BloodNeed need, BloodNeedStatus nextStatus, string? note, string actorId, DateTime nowUtc)
    {
        var previous = need.Status;
        need.Status = nextStatus;
        need.DecisionReason = TrimToNull(note);
        need.UpdatedAtUtc = nowUtc;
        AddHistory(need, previous, nextStatus, note, actorId, nowUtc);
        AddAudit(need, actorId, "BloodNeedStatusChanged", $"Blood need status changed from {previous} to {nextStatus}.", nowUtc);
    }

    private void AddHistory(BloodNeed need, BloodNeedStatus? fromStatus, BloodNeedStatus toStatus, string? note, string actorId, DateTime nowUtc) =>
        dbContext.BloodNeedStatusHistory.Add(new BloodNeedStatusHistory
        {
            Id = Guid.NewGuid(),
            BloodNeedId = need.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Note = TrimToNull(note),
            ChangedByUserId = actorId,
            ChangedAtUtc = nowUtc
        });

    private void AddAudit(BloodNeed need, string actorId, string action, string summary, DateTime nowUtc) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            Action = action,
            EntityType = nameof(BloodNeed),
            EntityId = need.Id,
            FacilityId = need.FacilityId,
            Summary = summary,
            CreatedAtUtc = nowUtc
        });

    private async Task ExecuteAtomicAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational())
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
        try
        {
            await operation();
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException exception)
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw new BloodLink.Domain.Exceptions.ConcurrencyException("The need or inventory changed concurrently. Reload and retry.", exception);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private static string DisplayName(string? firstName, string? lastName, string? email)
    {
        var name = string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        return name.Length > 0 ? name : email ?? "Facility user";
    }

    private static BloodNeedDto ToDto(BloodNeed need) =>
        new(need.Id, need.FacilityId, need.BloodType, need.UnitsNeeded, need.Urgency, need.Status, need.CreatedAtUtc);

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
