# API Contracts

BloodLink currently defines Application-layer contracts only. Full service implementations are deferred.

## Facilities

- `RegisterFacilityRequest`
- `FacilityDto`
- `FacilityDecisionRequest`
- `UpdateFacilityRequest`
- `IFacilityService`

## Staff

- `CreateStaffRequest`
- `StaffDto`
- `ChangeStaffStatusRequest`
- `IStaffService`

## Inventory

- `InventoryItemDto`
- `InventoryAdjustmentRequest`
- `InventoryTransactionDto`
- `AvailabilitySearchRequest`
- `AvailabilityResultDto`
- `IInventoryService`

## Needs

- `CreateBloodNeedRequest`
- `BloodNeedDto`
- `NeedDecisionRequest`
- `IBloodNeedService`

## Requests

- `CreateBloodRequestRequest`
- `BloodRequestDto`
- `RequestResponseRequest`
- `FulfilRequestRequest`
- `RequestTimelineItemDto`
- `IBloodRequestService`

## Notifications

- `NotificationDto`
- `UnreadNotificationCountDto`
- `INotificationService`

## Dashboards

- `SystemDashboardDto`
- `FacilityAdminDashboardDto`
- `StaffDashboardDto`
- `IDashboardService`

## Status Definitions

BloodNeed statuses: PendingReview, Searching, FulfilledInternally, FulfilledExternally, Rejected, Cancelled.

BloodRequest statuses: Sent, Accepted, Rejected, Fulfilled, Cancelled.

Status values must be changed only through service methods that validate the transition and actor.
