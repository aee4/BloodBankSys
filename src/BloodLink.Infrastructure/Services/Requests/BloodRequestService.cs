using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BloodLink.Infrastructure.Services.Requests;

public sealed class BloodRequestService(
    BloodLinkDbContext dbContext,
    ICurrentUserService currentUser,
    IInventoryService inventoryService) : IBloodRequestService
{
    private static readonly BloodRequestStatus[] FinalRequestStatuses =
    [
        BloodRequestStatus.Rejected,
        BloodRequestStatus.Fulfilled,
        BloodRequestStatus.Cancelled
    ];

    private static readonly BloodRequestStatus[] ActiveRequestStatuses =
    [
        BloodRequestStatus.Sent,
        BloodRequestStatus.Accepted
    ];

    public async Task<BloodRequestDto> CreateFromNeedAsync(CreateBloodRequestRequest request, CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var requestingFacilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, requestingFacilityId, cancellationToken);
        WorkflowValidation.EnsurePositiveUnits(request.UnitsRequested, nameof(request.UnitsRequested));
        WorkflowValidation.EnsureSafeNote(request.RequestNote);

        var need = await dbContext.BloodNeeds
            .SingleOrDefaultAsync(item => item.Id == request.BloodNeedId, cancellationToken)
            ?? throw new InvalidOperationException("The blood need was not found.");

        if (need.FacilityId != requestingFacilityId)
        {
            throw new UnauthorizedAccessException("You may create requests only for your own facility's needs.");
        }

        if (need.Status != BloodNeedStatus.Searching)
        {
            throw new InvalidOperationException("External requests can be created only from searching needs.");
        }

        if (request.SourceFacilityId == requestingFacilityId)
        {
            throw new ArgumentException("The source facility must differ from the requesting facility.");
        }

        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, request.SourceFacilityId, cancellationToken);

        if (request.UnitsRequested > need.UnitsNeeded)
        {
            throw new ArgumentException("Requested units cannot exceed the linked blood need.");
        }

        var activeRequestExists = await dbContext.BloodRequests.AnyAsync(
            item => item.BloodNeedId == need.Id && ActiveRequestStatuses.Contains(item.Status),
            cancellationToken);

        if (activeRequestExists)
        {
            throw new InvalidOperationException("Only one non-final request may exist for a blood need.");
        }

        var availability = await inventoryService.SearchAvailabilityAsync(
            new AvailabilitySearchRequest(need.BloodType, request.UnitsRequested), cancellationToken);
        if (!availability.Any(item => item.FacilityId == request.SourceFacilityId && item.AvailableUnits >= request.UnitsRequested))
        {
            throw new BloodLink.Domain.Exceptions.InsufficientInventoryException("The selected source no longer has enough available units for this request.");
        }

        var sourceFacilityName = await dbContext.Facilities.AsNoTracking()
            .Where(item => item.Id == request.SourceFacilityId).Select(item => item.Name).SingleAsync(cancellationToken);
        var requestingFacilityName = await dbContext.Facilities.AsNoTracking()
            .Where(item => item.Id == requestingFacilityId).Select(item => item.Name).SingleAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var bloodRequest = new BloodRequest
        {
            Id = Guid.NewGuid(),
            BloodNeedId = need.Id,
            RequestingFacilityId = requestingFacilityId,
            SourceFacilityId = request.SourceFacilityId,
            BloodType = need.BloodType,
            UnitsRequested = request.UnitsRequested,
            Status = BloodRequestStatus.Sent,
            RequestNote = TrimToNull(request.RequestNote),
            RequestedByAdminId = userId,
            CreatedAtUtc = nowUtc
        };

        await ExecuteAtomicTransitionAsync(async () =>
        {
            need.UpdatedAtUtc = nowUtc;
            dbContext.BloodRequests.Add(bloodRequest);
            AddHistory(bloodRequest.Id, null, BloodRequestStatus.Sent, bloodRequest.RequestNote, userId, nowUtc);
            AddAudit(bloodRequest, userId, "BloodRequestCreated", "Created external blood request.", nowUtc);
            await WorkflowNotifications.AddForActiveFacilityAdminsAsync(
                dbContext, request.SourceFacilityId, NotificationType.NewExternalRequest,
                "New external blood request", "Another approved facility sent a blood request for review.",
                nameof(BloodRequest), bloodRequest.Id, nowUtc, cancellationToken);
        }, cancellationToken);

        return ToDto(bloodRequest, requestingFacilityName, sourceFacilityName, need.Urgency);
    }

    public async Task<IReadOnlyList<BloodRequestDto>> ListSentAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        return await ProjectRequests(dbContext.BloodRequests.AsNoTracking()
            .Where(request => request.RequestingFacilityId == facilityId), cancellationToken);
    }

    public async Task<IReadOnlyList<BloodRequestDto>> ListReceivedAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        return await ProjectRequests(dbContext.BloodRequests.AsNoTracking()
            .Where(request => request.SourceFacilityId == facilityId), cancellationToken);
    }

    public async Task<BloodRequestDto?> GetAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        var bloodRequest = await dbContext.BloodRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == bloodRequestId, cancellationToken);

        if (bloodRequest is null)
        {
            return null;
        }

        if (bloodRequest.RequestingFacilityId != facilityId && bloodRequest.SourceFacilityId != facilityId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this request.");
        }

        return await ProjectRequest(bloodRequestId, cancellationToken);
    }

    public async Task<IReadOnlyList<RequestTimelineItemDto>> GetTimelineAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        if (await GetAsync(bloodRequestId, cancellationToken) is null)
        {
            return [];
        }

        return await (
                from history in dbContext.BloodRequestStatusHistory.AsNoTracking()
                join actor in dbContext.Users.AsNoTracking() on history.ChangedByUserId equals actor.Id
                where history.BloodRequestId == bloodRequestId
                orderby history.ChangedAtUtc, history.Id
                select new RequestTimelineItemDto(
                    history.FromStatus, history.ToStatus,
                    DisplayName(actor.FirstName, actor.LastName, actor.Email), history.Note, history.ChangedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task AcceptAsync(RequestResponseRequest request, CancellationToken cancellationToken = default)
    {
        await ExecuteAtomicTransitionAsync(async () =>
        {
            var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
            var bloodRequest = await LoadForSourceAdminAsync(request.BloodRequestId, cancellationToken);

            if (bloodRequest.Status != BloodRequestStatus.Sent)
            {
                throw new InvalidOperationException("Only sent requests may be accepted.");
            }

            if (request.UnitsAccepted is not { } unitsAccepted)
            {
                throw new ArgumentException("Accepted units are required.");
            }

            WorkflowValidation.EnsurePositiveUnits(unitsAccepted, nameof(request.UnitsAccepted));
            WorkflowValidation.EnsureSafeNote(request.ResponseNote);
            if (unitsAccepted > bloodRequest.UnitsRequested)
            {
                throw new ArgumentException("Accepted units cannot exceed requested units.");
            }

            await inventoryService.ReserveForRequestAsync(bloodRequest.Id, unitsAccepted, deferSave: true, cancellationToken);

            var nowUtc = DateTime.UtcNow;
            bloodRequest.Status = BloodRequestStatus.Accepted;
            bloodRequest.UnitsAccepted = unitsAccepted;
            bloodRequest.RespondedByAdminId = userId;
            bloodRequest.RespondedAtUtc = nowUtc;
            bloodRequest.ResponseNote = TrimToNull(request.ResponseNote);
            AddHistory(bloodRequest.Id, BloodRequestStatus.Sent, BloodRequestStatus.Accepted, bloodRequest.ResponseNote, userId, nowUtc);
            AddAudit(bloodRequest, userId, "BloodRequestAccepted", $"Accepted {unitsAccepted} units.", nowUtc);
            await AddRequestingSideNotificationAsync(bloodRequest, NotificationType.RequestResponse, "Blood request accepted", "A source facility accepted your blood request.", nowUtc, cancellationToken);
        }, cancellationToken);
    }

    public async Task RejectAsync(RequestResponseRequest request, CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var bloodRequest = await LoadForSourceAdminAsync(request.BloodRequestId, cancellationToken);

        if (bloodRequest.Status != BloodRequestStatus.Sent)
        {
            throw new InvalidOperationException("Only sent requests may be rejected.");
        }

        if (string.IsNullOrWhiteSpace(request.ResponseNote))
        {
            throw new ArgumentException("A rejection reason is required.");
        }

        WorkflowValidation.EnsureSafeNote(request.ResponseNote);

        var nowUtc = DateTime.UtcNow;
        var previousStatus = bloodRequest.Status;
        bloodRequest.Status = BloodRequestStatus.Rejected;
        bloodRequest.RespondedByAdminId = userId;
        bloodRequest.RespondedAtUtc = nowUtc;
        bloodRequest.ResponseNote = request.ResponseNote.Trim();
        AddHistory(bloodRequest.Id, previousStatus, BloodRequestStatus.Rejected, bloodRequest.ResponseNote, userId, nowUtc);
        AddAudit(bloodRequest, userId, "BloodRequestRejected", "Rejected external blood request.", nowUtc);
        await AddRequestingSideNotificationAsync(bloodRequest, NotificationType.RequestResponse, "Blood request rejected", "A source facility rejected your blood request.", nowUtc, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        await ExecuteAtomicTransitionAsync(async () =>
        {
            var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
            var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
            await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);
            var bloodRequest = await dbContext.BloodRequests.SingleOrDefaultAsync(
                    item => item.Id == bloodRequestId && item.SourceFacilityId == facilityId,
                    cancellationToken)
                ?? throw new InvalidOperationException("The blood request was not found.");

            var previousStatus = bloodRequest.Status;
            if (previousStatus == BloodRequestStatus.Accepted)
            {
                await inventoryService.ReleaseReservationAsync(bloodRequest.Id, deferSave: true, cancellationToken);
            }
            else if (previousStatus != BloodRequestStatus.Sent)
            {
                throw new InvalidOperationException("Only sent or accepted requests may be cancelled.");
            }

            var nowUtc = DateTime.UtcNow;
            bloodRequest.Status = BloodRequestStatus.Cancelled;
            AddHistory(bloodRequest.Id, previousStatus, BloodRequestStatus.Cancelled, "Request cancelled.", userId, nowUtc);
            AddAudit(bloodRequest, userId, "BloodRequestCancelled", "Cancelled blood request.", nowUtc);
            await AddOppositeSideCancellationNotificationAsync(bloodRequest, bloodRequest.SourceFacilityId, nowUtc, cancellationToken);
        }, cancellationToken);
    }

    public async Task FulfilAsync(FulfilRequestRequest request, CancellationToken cancellationToken = default)
    {
        await ExecuteAtomicTransitionAsync(async () =>
        {
            var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
            var bloodRequest = await LoadForSourceAdminAsync(request.BloodRequestId, cancellationToken);
            if (bloodRequest.Status != BloodRequestStatus.Accepted)
            {
                throw new InvalidOperationException("Only accepted requests may be fulfilled.");
            }

            WorkflowValidation.EnsureSafeNote(request.Note);
            var need = await dbContext.BloodNeeds.SingleOrDefaultAsync(item => item.Id == bloodRequest.BloodNeedId, cancellationToken)
                ?? throw new InvalidOperationException("The linked blood need was not found.");
            if (need.Status != BloodNeedStatus.Searching)
            {
                throw new InvalidOperationException("The linked blood need must still be searching before this request can be fulfilled.");
            }

            await inventoryService.FulfilTransferAsync(bloodRequest.Id, deferSave: true, cancellationToken);

            var nowUtc = DateTime.UtcNow;
            bloodRequest.Status = BloodRequestStatus.Fulfilled;
            bloodRequest.FulfilledByAdminId = userId;
            bloodRequest.FulfilledAtUtc = nowUtc;
            need.Status = BloodNeedStatus.FulfilledExternally;
            need.UpdatedAtUtc = nowUtc;
            dbContext.BloodNeedStatusHistory.Add(new BloodNeedStatusHistory
            {
                Id = Guid.NewGuid(),
                BloodNeedId = need.Id,
                FromStatus = BloodNeedStatus.Searching,
                ToStatus = BloodNeedStatus.FulfilledExternally,
                Note = TrimToNull(request.Note),
                ChangedByUserId = userId,
                ChangedAtUtc = nowUtc
            });
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = userId,
                Action = "BloodNeedStatusChanged",
                EntityType = nameof(BloodNeed),
                EntityId = need.Id,
                FacilityId = need.FacilityId,
                Summary = "Blood need status changed from Searching to FulfilledExternally.",
                CreatedAtUtc = nowUtc
            });
            AddHistory(bloodRequest.Id, BloodRequestStatus.Accepted, BloodRequestStatus.Fulfilled, TrimToNull(request.Note), userId, nowUtc);
            AddAudit(bloodRequest, userId, "BloodRequestFulfilled", $"Transferred {bloodRequest.UnitsAccepted} units.", nowUtc);
            await AddRequestingSideNotificationAsync(bloodRequest, NotificationType.RequestFulfilled, "Blood request fulfilled", "A source facility marked your blood request fulfilled.", nowUtc, cancellationToken);
            WorkflowNotifications.AddForUsers(dbContext, [need.RequestedByUserId], NotificationType.FacilityDecision,
                "Blood need fulfilled externally", "Your need was fulfilled by an external facility.", nameof(BloodNeed), need.Id, nowUtc);
        }, cancellationToken);
    }

    private async Task ExecuteAtomicTransitionAsync(Func<Task> transition, CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational())
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            await transition();
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            dbContext.ChangeTracker.Clear();
            throw new Domain.Exceptions.ConcurrencyException("The request or inventory changed concurrently. Please retry.", ex);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            dbContext.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private void AddAudit(BloodRequest request, string actorUserId, string action, string summary, DateTime nowUtc)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = nameof(BloodRequest),
            EntityId = request.Id,
            FacilityId = currentUser.FacilityId ?? request.SourceFacilityId,
            Summary = summary,
            CreatedAtUtc = nowUtc
        });
    }

    private async Task<BloodRequest> LoadForSourceAdminAsync(Guid bloodRequestId, CancellationToken cancellationToken)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        var bloodRequest = await dbContext.BloodRequests
            .SingleOrDefaultAsync(item => item.Id == bloodRequestId, cancellationToken)
            ?? throw new InvalidOperationException("The blood request was not found.");

        if (bloodRequest.SourceFacilityId != facilityId)
        {
            throw new UnauthorizedAccessException("Only the source facility admin may perform this action.");
        }

        return bloodRequest;
    }

    private async Task AddRequestingSideNotificationAsync(
        BloodRequest bloodRequest,
        NotificationType notificationType,
        string title,
        string message,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var recipientIds = await WorkflowNotifications.GetActiveFacilityAdminIdsAsync(
            dbContext,
            bloodRequest.RequestingFacilityId,
            cancellationToken);

        WorkflowNotifications.AddForUsers(
            dbContext,
            recipientIds.Append(bloodRequest.RequestedByAdminId),
            notificationType,
            title,
            message,
            nameof(BloodRequest),
            bloodRequest.Id,
            nowUtc);
    }

    private async Task AddOppositeSideCancellationNotificationAsync(
        BloodRequest bloodRequest,
        Guid actorFacilityId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var recipientFacilityId = actorFacilityId == bloodRequest.RequestingFacilityId
            ? bloodRequest.SourceFacilityId
            : bloodRequest.RequestingFacilityId;

        await WorkflowNotifications.AddForActiveFacilityAdminsAsync(
            dbContext,
            recipientFacilityId,
            NotificationType.RequestResponse,
            "Blood request cancelled",
            "A facility cancelled a blood request.",
            nameof(BloodRequest),
            bloodRequest.Id,
            nowUtc,
            cancellationToken);
    }

    private void AddHistory(
        Guid bloodRequestId,
        BloodRequestStatus? fromStatus,
        BloodRequestStatus toStatus,
        string? note,
        string changedByUserId,
        DateTime changedAtUtc)
    {
        dbContext.BloodRequestStatusHistory.Add(new BloodRequestStatusHistory
        {
            Id = Guid.NewGuid(),
            BloodRequestId = bloodRequestId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Note = TrimToNull(note),
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = changedAtUtc
        });
    }

    private async Task<IReadOnlyList<BloodRequestDto>> ProjectRequests(
        IQueryable<BloodRequest> requests,
        CancellationToken cancellationToken) =>
        await (from request in requests
               join need in dbContext.BloodNeeds.AsNoTracking() on request.BloodNeedId equals need.Id
               join requesting in dbContext.Facilities.AsNoTracking() on request.RequestingFacilityId equals requesting.Id
               join source in dbContext.Facilities.AsNoTracking() on request.SourceFacilityId equals source.Id
               orderby request.CreatedAtUtc descending, request.Id
               select new BloodRequestDto(
                   request.Id, request.BloodNeedId, request.RequestingFacilityId, requesting.Name,
                   request.SourceFacilityId, source.Name, request.BloodType, request.UnitsRequested,
                   request.UnitsAccepted, need.Urgency, request.Status, request.RequestNote,
                   request.ResponseNote, request.CreatedAtUtc, request.RespondedAtUtc, request.FulfilledAtUtc))
        .ToListAsync(cancellationToken);

    private async Task<BloodRequestDto?> ProjectRequest(Guid requestId, CancellationToken cancellationToken) =>
        (await ProjectRequests(dbContext.BloodRequests.AsNoTracking().Where(request => request.Id == requestId), cancellationToken))
        .SingleOrDefault();

    private static BloodRequestDto ToDto(BloodRequest request, string requestingName, string sourceName, UrgencyLevel priority) =>
        new(request.Id, request.BloodNeedId, request.RequestingFacilityId, requestingName, request.SourceFacilityId,
            sourceName, request.BloodType, request.UnitsRequested, request.UnitsAccepted, priority, request.Status,
            request.RequestNote, request.ResponseNote, request.CreatedAtUtc, request.RespondedAtUtc, request.FulfilledAtUtc);

    private static string DisplayName(string? firstName, string? lastName, string? email)
    {
        var name = string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        return name.Length > 0 ? name : email ?? "Facility user";
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
