using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Services.Needs;

public sealed class BloodNeedService(
    BloodLinkDbContext dbContext,
    ICurrentUserService currentUser) : IBloodNeedService
{
    private static readonly BloodNeedStatus[] FinalStatuses =
    [
        BloodNeedStatus.FulfilledInternally,
        BloodNeedStatus.FulfilledExternally,
        BloodNeedStatus.Rejected,
        BloodNeedStatus.Cancelled
    ];

    private static readonly BloodRequestStatus[] ActiveRequestStatuses =
    [
        BloodRequestStatus.Sent,
        BloodRequestStatus.Accepted
    ];

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
        await WorkflowNotifications.AddForActiveFacilityAdminsAsync(
            dbContext,
            facilityId,
            NotificationType.NewNeed,
            "New internal blood need",
            "A staff member submitted a blood need for review.",
            nameof(BloodNeed),
            need.Id,
            nowUtc,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(need);
    }

    public async Task<IReadOnlyList<BloodNeedDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityStaff);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        return await dbContext.BloodNeeds
            .AsNoTracking()
            .Where(need => need.FacilityId == facilityId && need.RequestedByUserId == userId)
            .OrderByDescending(need => need.CreatedAtUtc)
            .Select(need => new BloodNeedDto(need.Id, need.FacilityId, need.BloodType, need.UnitsNeeded, need.Urgency, need.Status, need.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BloodNeedDto>> ListOwnFacilityAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = ServiceGuards.RequireFacilityRole(currentUser, RoleNames.FacilityAdmin);
        await ServiceGuards.RequireApprovedFacilityAsync(dbContext, facilityId, cancellationToken);

        return await dbContext.BloodNeeds
            .AsNoTracking()
            .Where(need => need.FacilityId == facilityId)
            .OrderByDescending(need => need.CreatedAtUtc)
            .Select(need => new BloodNeedDto(need.Id, need.FacilityId, need.BloodType, need.UnitsNeeded, need.Urgency, need.Status, need.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public Task StartSearchAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsFacilityAdminAsync(request, BloodNeedStatus.PendingReview, BloodNeedStatus.Searching, requiresReason: false, cancellationToken);

    public async Task FulfilInternallyAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var need = await LoadNeedForFacilityAdminAsync(request.BloodNeedId, cancellationToken);
        EnsureNotFinal(need);

        if (need.Status is not (BloodNeedStatus.PendingReview or BloodNeedStatus.Searching))
        {
            throw new InvalidOperationException("Only pending or searching needs may be fulfilled internally.");
        }

        await EnsureNoActiveExternalRequestAsync(need, cancellationToken);
        ApplyNeedTransition(need, BloodNeedStatus.FulfilledInternally, request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task RejectAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsFacilityAdminAsync(request, BloodNeedStatus.PendingReview, BloodNeedStatus.Rejected, requiresReason: true, cancellationToken);

    public async Task CancelAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var userId = ServiceGuards.RequireAuthenticatedActiveUser(currentUser);
        var need = await dbContext.BloodNeeds.SingleOrDefaultAsync(item => item.Id == request.BloodNeedId, cancellationToken)
            ?? throw new InvalidOperationException("The blood need was not found.");

        EnsureNotFinal(need);
        var isCreator = need.RequestedByUserId == userId;
        var isAdminForNeedFacility = currentUser.IsInRole(RoleNames.FacilityAdmin) && currentUser.BelongsToFacility(need.FacilityId);

        if (!isCreator && !isAdminForNeedFacility)
        {
            throw new UnauthorizedAccessException("You are not authorized to cancel this blood need.");
        }

        if (isCreator && need.Status != BloodNeedStatus.PendingReview && !isAdminForNeedFacility)
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
        ApplyNeedTransition(need, BloodNeedStatus.Cancelled, request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task TransitionAsFacilityAdminAsync(
        NeedDecisionRequest request,
        BloodNeedStatus expectedStatus,
        BloodNeedStatus nextStatus,
        bool requiresReason,
        CancellationToken cancellationToken)
    {
        var need = await LoadNeedForFacilityAdminAsync(request.BloodNeedId, cancellationToken);
        EnsureNotFinal(need);

        if (need.Status != expectedStatus)
        {
            throw new InvalidOperationException($"Blood need must be {expectedStatus} to perform this action.");
        }

        if (requiresReason && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A reason is required.");
        }

        WorkflowValidation.EnsureSafeNote(request.Reason);
        ApplyNeedTransition(need, nextStatus, request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
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

    private static void ApplyNeedTransition(BloodNeed need, BloodNeedStatus nextStatus, string? reason)
    {
        need.Status = nextStatus;
        need.DecisionReason = TrimToNull(reason);
        need.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void EnsureNotFinal(BloodNeed need)
    {
        if (FinalStatuses.Contains(need.Status))
        {
            throw new InvalidOperationException("Final blood needs cannot transition again.");
        }
    }

    private async Task EnsureNoActiveExternalRequestAsync(BloodNeed need, CancellationToken cancellationToken)
    {
        if (need.Status != BloodNeedStatus.Searching)
        {
            return;
        }

        var hasActiveRequest = await dbContext.BloodRequests.AnyAsync(
            request => request.BloodNeedId == need.Id && ActiveRequestStatuses.Contains(request.Status),
            cancellationToken);

        if (hasActiveRequest)
        {
            throw new InvalidOperationException("Resolve or cancel the active external blood request before changing this searching blood need.");
        }
    }

    private static BloodNeedDto ToDto(BloodNeed need) =>
        new(need.Id, need.FacilityId, need.BloodType, need.UnitsNeeded, need.Urgency, need.Status, need.CreatedAtUtc);

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
