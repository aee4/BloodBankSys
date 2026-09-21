using BloodLink.Domain.Enums;

namespace BloodLink.Web.Components.Shared;

public static class BloodDisplay
{
    public static string BloodTypeLabel(BloodType bloodType) => bloodType switch
    {
        BloodType.APositive => "A+",
        BloodType.ANegative => "A-",
        BloodType.BPositive => "B+",
        BloodType.BNegative => "B-",
        BloodType.ABPositive => "AB+",
        BloodType.ABNegative => "AB-",
        BloodType.OPositive => "O+",
        BloodType.ONegative => "O-",
        _ => bloodType.ToString()
    };

    public static string FacilityTypeLabel(FacilityType facilityType) => facilityType switch
    {
        FacilityType.Hospital => "Hospital",
        FacilityType.BloodBank => "Blood Bank",
        _ => facilityType.ToString()
    };

    public static string UrgencyLabel(UrgencyLevel urgency) => urgency.ToString();

    public static string UrgencyTone(UrgencyLevel urgency) => urgency switch
    {
        UrgencyLevel.Emergency => "danger",
        UrgencyLevel.Urgent => "warning",
        _ => "neutral"
    };

    public static string BloodNeedStatusLabel(BloodNeedStatus status) => status switch
    {
        BloodNeedStatus.PendingReview => "Pending Review",
        BloodNeedStatus.FulfilledInternally => "Fulfilled Internally",
        BloodNeedStatus.FulfilledExternally => "Fulfilled Externally",
        _ => status.ToString()
    };

    public static string BloodNeedStatusTone(BloodNeedStatus status) => status switch
    {
        BloodNeedStatus.PendingReview => "warning",
        BloodNeedStatus.Searching => "neutral",
        BloodNeedStatus.FulfilledInternally or BloodNeedStatus.FulfilledExternally => "success",
        BloodNeedStatus.Rejected or BloodNeedStatus.Cancelled => "danger",
        _ => "neutral"
    };

    public static string TransactionTypeLabel(InventoryTransactionType type) => type switch
    {
        InventoryTransactionType.StockIn => "Stock In",
        InventoryTransactionType.Consumption => "Consumption",
        InventoryTransactionType.ManualAdjustment => "Manual Adjustment",
        InventoryTransactionType.Reserve => "Reserve",
        InventoryTransactionType.Release => "Release",
        InventoryTransactionType.TransferOut => "Transfer Out",
        InventoryTransactionType.TransferIn => "Transfer In",
        _ => type.ToString()
    };

    public static string ShortId(Guid id) => id.ToString("N")[..6].ToUpperInvariant();

    public static string FormatTimestamp(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public static string BloodRequestStatusLabel(BloodRequestStatus status) => status switch
    {
        BloodRequestStatus.Sent => "Sent",
        BloodRequestStatus.Accepted => "Accepted",
        BloodRequestStatus.Rejected => "Rejected",
        BloodRequestStatus.Fulfilled => "Fulfilled",
        BloodRequestStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    public static string BloodRequestStatusTone(BloodRequestStatus status) => status switch
    {
        BloodRequestStatus.Sent => "neutral",
        BloodRequestStatus.Accepted => "warning",
        BloodRequestStatus.Fulfilled => "success",
        BloodRequestStatus.Rejected or BloodRequestStatus.Cancelled => "danger",
        _ => "neutral"
    };

    public static string FacilityStatusLabel(FacilityStatus status) => status switch
    {
        FacilityStatus.Pending => "Pending Approval",
        FacilityStatus.Approved => "Approved",
        FacilityStatus.Rejected => "Rejected",
        FacilityStatus.Suspended => "Suspended",
        _ => status.ToString()
    };

    public static string FacilityStatusTone(FacilityStatus status) => status switch
    {
        FacilityStatus.Pending => "warning",
        FacilityStatus.Approved => "success",
        FacilityStatus.Rejected or FacilityStatus.Suspended => "danger",
        _ => "neutral"
    };

    public static string StaffStatusLabel(StaffStatus status) => status switch
    {
        StaffStatus.PendingActivation => "Pending Activation",
        StaffStatus.Active => "Active",
        StaffStatus.Inactive => "Inactive",
        _ => status.ToString()
    };

    public static string StaffStatusTone(StaffStatus status) => status switch
    {
        StaffStatus.PendingActivation => "warning",
        StaffStatus.Active => "success",
        StaffStatus.Inactive => "danger",
        _ => "neutral"
    };

    public static string NotificationTypeLabel(NotificationType type) => type switch
    {
        NotificationType.FacilityDecision => "Facility",
        NotificationType.NewNeed => "Need",
        NotificationType.NewExternalRequest => "Request",
        NotificationType.RequestResponse => "Request",
        NotificationType.RequestFulfilled => "Request",
        NotificationType.LowStock => "Inventory",
        NotificationType.AccountCreated => "Account",
        NotificationType.Security => "Security",
        _ => type.ToString()
    };

    public static string NotificationTypeIcon(NotificationType type) => type switch
    {
        NotificationType.FacilityDecision or NotificationType.AccountCreated => "building",
        NotificationType.NewNeed => "clipboard-list",
        NotificationType.NewExternalRequest or NotificationType.RequestResponse => "send",
        NotificationType.RequestFulfilled => "circle-check",
        NotificationType.LowStock => "alert-triangle",
        NotificationType.Security => "shield",
        _ => "bell"
    };
}