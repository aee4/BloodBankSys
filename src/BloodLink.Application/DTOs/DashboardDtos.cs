namespace BloodLink.Application.DTOs;

public sealed record SystemDashboardDto(int PendingFacilities, int ApprovedFacilities, int SuspendedFacilities);
public sealed record FacilityAdminDashboardDto(int OpenNeeds, int SentRequests, int ReceivedRequests, int LowStockItems);
public sealed record StaffDashboardDto(int MyOpenNeeds, int UnreadNotifications);
