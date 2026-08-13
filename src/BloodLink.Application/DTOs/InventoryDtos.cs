using BloodLink.Domain.Enums;

namespace BloodLink.Application.DTOs;

public sealed record InventoryItemDto(Guid Id, Guid FacilityId, BloodType BloodType, int TotalUnits, int ReservedUnits, int AvailableUnits, int LowStockThreshold);
public sealed record InventoryAdjustmentRequest(BloodType BloodType, int TotalUnitsChange, string Reason);
public sealed record InventoryTransactionDto(Guid Id, BloodType BloodType, InventoryTransactionType TransactionType, int TotalUnitsChange, int ReservedUnitsChange, DateTime CreatedAtUtc);
public sealed record AvailabilitySearchRequest(BloodType BloodType, int MinimumAvailableUnits);
public sealed record AvailabilityResultDto(Guid FacilityId, string FacilityName, FacilityType FacilityType, string City, BloodType BloodType, int AvailableUnits);
