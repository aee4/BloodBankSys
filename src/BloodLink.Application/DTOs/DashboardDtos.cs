using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

public sealed record SystemDashboardDto(int PendingFacilities, int ApprovedFacilities, int SuspendedFacilities)
{
    public int ActiveRequests { get; init; }
    public IReadOnlyList<DashboardFacilityItemDto> PendingReviews { get; init; } = [];
    public IReadOnlyList<DashboardActivityDto> RecentActivity { get; init; } = [];
}

public sealed record FacilityAdminDashboardDto(int OpenNeeds, int SentRequests, int ReceivedRequests, int LowStockItems)
{
    public long TotalInventoryUnits { get; init; }
    public long AvailableInventoryUnits { get; init; }
    public int UnreadNotifications { get; init; }
    public IReadOnlyList<DashboardNeedItemDto> PendingNeeds { get; init; } = [];
    public IReadOnlyList<DashboardActivityDto> RecentActivity { get; init; } = [];
}

public sealed record StaffDashboardDto(int MyOpenNeeds, int UnreadNotifications)
{
    public int PendingReviewNeeds { get; init; }
    public int SearchingNeeds { get; init; }
    public IReadOnlyList<DashboardNeedItemDto> RecentNeeds { get; init; } = [];
}

public sealed record DashboardFacilityItemDto(Guid Id, string Name, string City, string Region, DateTime CreatedAtUtc);
public sealed record DashboardNeedItemDto(Guid Id, BloodType BloodType, int UnitsNeeded, UrgencyLevel Urgency, BloodNeedStatus Status, DateTime CreatedAtUtc);
public sealed record DashboardActivityDto(string Action, string Summary, DateTime CreatedAtUtc, string? EntityType, Guid? EntityId);
