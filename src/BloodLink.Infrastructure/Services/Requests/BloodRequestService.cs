using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

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

        dbContext.BloodRequests.Add(bloodRequest);
        AddHistory(bloodRequest.Id, null, BloodRequestStatus.Sent, bloodRequest.RequestNote, userId, nowUtc);

        await WorkflowNotifications.AddForActiveFacilityAdminsAsync(
            dbContext,
            request.SourceFacilityId,
            NotificationType.NewExternalRequest,
            "New external blood request",
            "Another approved facility sent a blood request for review.",
            nameof(BloodRequest),
            bloodRequest.Id,
            nowUtc,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(bloodRequest);
    }

    public async Task<IReadOnlyList<BloodRequestDto>> ListSentAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        return await dbContext.BloodRequests
            .AsNoTracking()
            .Where(request => request.RequestingFacilityId == facilityId)
            .OrderByDescending(request => request.CreatedAtUtc)
            .Select(request => new BloodRequestDto(request.Id, request.BloodNeedId, request.RequestingFacilityId, request.SourceFacilityId, request.BloodType, request.UnitsRequested, request.UnitsAccepted, request.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BloodRequestDto>> ListReceivedAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        return await dbContext.BloodRequests
            .AsNoTracking()
            .Where(request => request.SourceFacilityId == facilityId)
            .OrderByDescending(request => request.CreatedAtUtc)
            .Select(request => new BloodRequestDto(request.Id, request.BloodNeedId, request.RequestingFacilityId, request.SourceFacilityId, request.BloodType, request.UnitsRequested, request.UnitsAccepted, request.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<BloodRequestDto?> GetAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        var bloodRequest = await dbContext.BloodRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == bloodRequestId, cancellationToken);

        if (bloodRequest is null)
        {
            return null;
        }

        if (bloodRequest.RequestingFacilityId != facilityId && bloodRequest.SourceFacilityId != facilityId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this request.");
        }

        return ToDto(bloodRequest);
    }

    public async Task AcceptAsync(RequestResponseRequest request, CancellationToken cancellationToken = default)
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

        await inventoryService.ReserveForRequestAsync(bloodRequest.Id, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var previousStatus = bloodRequest.Status;
        bloodRequest.Status = BloodRequestStatus.Accepted;
        bloodRequest.UnitsAccepted = unitsAccepted;
        bloodRequest.RespondedByAdminId = userId;
        bloodRequest.RespondedAtUtc = nowUtc;
        bloodRequest.ResponseNote = TrimToNull(request.ResponseNote);
        AddHistory(bloodRequest.Id, previousStatus, BloodRequestStatus.Accepted, bloodRequest.ResponseNote, userId, nowUtc);
        await AddRequestingSideNotificationAsync(bloodRequest, NotificationType.RequestResponse, "Blood request accepted", "A source facility accepted your blood request.", nowUtc, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
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
        await AddRequestingSideNotificationAsync(bloodRequest, NotificationType.RequestResponse, "Blood request rejected", "A source facility rejected your blood request.", nowUtc, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        var bloodRequest = await dbContext.BloodRequests
            .SingleOrDefaultAsync(item => item.Id == bloodRequestId, cancellationToken)
            ?? throw new InvalidOperationException("The blood request was not found.");

        if (bloodRequest.RequestingFacilityId != facilityId && bloodRequest.SourceFacilityId != facilityId)
        {
            throw new UnauthorizedAccessException("You are not authorized to cancel this request.");
        }

        if (bloodRequest.Status == BloodRequestStatus.Accepted)
        {
            await inventoryService.ReleaseReservationAsync(bloodRequest.Id, cancellationToken);
        }
        else if (bloodRequest.Status != BloodRequestStatus.Sent)
        {
            throw new InvalidOperationException("Only sent or accepted requests may be cancelled.");
        }

        var nowUtc = DateTime.UtcNow;
        var previousStatus = bloodRequest.Status;
        bloodRequest.Status = BloodRequestStatus.Cancelled;
        AddHistory(bloodRequest.Id, previousStatus, BloodRequestStatus.Cancelled, "Request cancelled.", userId, nowUtc);
        await AddOppositeSideCancellationNotificationAsync(bloodRequest, facilityId, nowUtc, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FulfilAsync(FulfilRequestRequest request, CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var bloodRequest = await LoadForSourceAdminAsync(request.BloodRequestId, cancellationToken);

        if (bloodRequest.Status != BloodRequestStatus.Accepted)
        {
            throw new InvalidOperationException("Only accepted requests may be fulfilled.");
        }

        WorkflowValidation.EnsureSafeNote(request.Note);

        var need = await dbContext.BloodNeeds
            .SingleOrDefaultAsync(item => item.Id == bloodRequest.BloodNeedId, cancellationToken)
            ?? throw new InvalidOperationException("The linked blood need was not found.");

        if (need.Status != BloodNeedStatus.Searching)
        {
            throw new InvalidOperationException("The linked blood need must still be searching before this request can be fulfilled.");
        }

        await inventoryService.FulfilTransferAsync(bloodRequest.Id, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var previousStatus = bloodRequest.Status;
        bloodRequest.Status = BloodRequestStatus.Fulfilled;
        bloodRequest.FulfilledByAdminId = userId;
        bloodRequest.FulfilledAtUtc = nowUtc;
        need.Status = BloodNeedStatus.FulfilledExternally;
        need.UpdatedAtUtc = nowUtc;
        AddHistory(bloodRequest.Id, previousStatus, BloodRequestStatus.Fulfilled, TrimToNull(request.Note), userId, nowUtc);
        await AddRequestingSideNotificationAsync(bloodRequest, NotificationType.RequestFulfilled, "Blood request fulfilled", "A source facility marked your blood request fulfilled.", nowUtc, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
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

    private static BloodRequestDto ToDto(BloodRequest request) =>
        new(
            request.Id,
            request.BloodNeedId,
            request.RequestingFacilityId,
            request.SourceFacilityId,
            request.BloodType,
            request.UnitsRequested,
            request.UnitsAccepted,
            request.Status);

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
