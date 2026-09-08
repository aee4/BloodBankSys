using BloodLink.Application.DTOs;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Data;

namespace BloodLink.Infrastructure.Services.RequestServices;

/// <summary>
/// Service for managing external blood requests between facilities.
/// Coordinates with InventoryService for reserve/release/fulfil operations.
/// Owned by Backend Developer 3.
/// </summary>
public sealed class BloodRequestService : IBloodRequestService
{
    private readonly BloodLinkDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventoryService _inventoryService;

    public BloodRequestService(
        BloodLinkDbContext context,
        ICurrentUserService currentUserService,
        IInventoryService inventoryService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
    }

    public Task<BloodRequestDto> CreateFromNeedAsync(CreateBloodRequestRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - CreateFromNeedAsync");
    }

    public Task<IReadOnlyList<BloodRequestDto>> ListSentAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - ListSentAsync");
    }

    public Task<IReadOnlyList<BloodRequestDto>> ListReceivedAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - ListReceivedAsync");
    }

    public Task<BloodRequestDto?> GetAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - GetAsync");
    }

    public Task AcceptAsync(RequestResponseRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - AcceptAsync");
    }

    public Task RejectAsync(RequestResponseRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - RejectAsync");
    }

    public Task CancelAsync(Guid bloodRequestId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - CancelAsync");
    }

    public Task FulfilAsync(FulfilRequestRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Backend Developer 3 - FulfilAsync");
    }
}
