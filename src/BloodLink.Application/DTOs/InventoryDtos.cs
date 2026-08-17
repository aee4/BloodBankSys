using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

/// <summary>
/// Represents current blood inventory for a facility and blood type.
/// </summary>
public sealed record InventoryItemDto(
    Guid Id,
    Guid FacilityId,
    BloodType BloodType,
    int TotalUnits,
    int ReservedUnits,
    int AvailableUnits,
    int LowStockThreshold);

/// <summary>
/// Request to adjust inventory stock (stock-in, consumption, or manual adjustment).
/// </summary>
public sealed record InventoryAdjustmentRequest(
    BloodType BloodType,
    int TotalUnitsChange,
    string Reason);

/// <summary>
/// Immutable record of an inventory transaction.
/// </summary>
public sealed record InventoryTransactionDto(
    Guid Id,
    BloodType BloodType,
    InventoryTransactionType TransactionType,
    int TotalUnitsChange,
    int ReservedUnitsChange,
    DateTime CreatedAtUtc);

/// <summary>
/// Request to search for blood availability at other approved facilities.
/// </summary>
public sealed record AvailabilitySearchRequest(
    BloodType BloodType,
    int MinimumAvailableUnits);

/// <summary>
/// Result of an availability search at a facility.
/// </summary>
public sealed record AvailabilityResultDto(
    Guid FacilityId,
    string FacilityName,
    FacilityType FacilityType,
    string City,
    BloodType BloodType,
    int AvailableUnits);

/// <summary>
/// Request to get low-stock items for a facility.
/// </summary>
public sealed record LowStockQueryRequest(int DaysLookAhead = 7);

/// <summary>
/// Low-stock alert result.
/// </summary>
public sealed record LowStockAlertDto(
    BloodType BloodType,
    int AvailableUnits,
    int LowStockThreshold,
    DateTime UpdatedAtUtc);
