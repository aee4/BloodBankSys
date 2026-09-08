using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Data;

namespace BloodLink.Infrastructure.Services.NeedServices;

/// <summary>
/// Service for managing internal blood needs and their lifecycle.
/// Owned by Backend Developer 3.
/// </summary>
public sealed class BloodNeedService : IBloodNeedService
{
    private readonly BloodLinkDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventoryService _inventoryService;

    public BloodNeedService(
        BloodLinkDbContext context,
        ICurrentUserService currentUserService,
        IInventoryService inventoryService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
    }

    public Task<BloodNeedDto> CreateAsync(CreateBloodNeedRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - CreateAsync");
    }

    public Task<IReadOnlyList<BloodNeedDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - GetMineAsync");
    }

    public Task<IReadOnlyList<BloodNeedDto>> ListOwnFacilityAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - ListOwnFacilityAsync");
    }

    public Task StartSearchAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - StartSearchAsync");
    }

    public Task FulfilInternallyAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - FulfilInternallyAsync");
    }

    public Task RejectAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - RejectAsync");
    }

    public Task CancelAsync(NeedDecisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - CancelAsync");
    }
}
