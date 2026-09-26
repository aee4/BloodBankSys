# API Contracts

Application contracts are implemented by the infrastructure services and exposed to the UI through scoped interfaces. Authorization is enforced by service methods as well as UI policies.

## Facilities and staff

Facility contracts include `RegisterFacilityRequest`, `FacilityDto`, `FacilityDecisionRequest`, `UpdateFacilityRequest`, and `IFacilityService`. Staff contracts include `CreateStaffRequest`, `StaffDto`, `ChangeStaffStatusRequest`, and `IStaffService`.

## Needs

`IBloodNeedService` supports creation, authorized detail (`GetAsync`), and persisted chronological status history (`GetTimelineAsync`). A creator may read their own need; a FacilityAdmin may read needs from their own approved facility. Detail includes exact blood type, units, urgency, status, reason, creator/time, and same-facility inventory context. Timeline entries come from `BloodNeedStatusHistory`, ordered oldest first with deterministic tie-breaking.

Internal fulfilment is coordinated by `IBloodNeedService` and `IInventoryService.ConsumeForNeedAsync`. It consumes the exact requested units from total stock, leaves reservations unchanged, and writes inventory, need history, audit, and notification evidence atomically.

## Requests and inventory

`IBloodRequestService` exposes participant-authorized list/detail and chronological persisted request history. `BloodRequestDto` includes linked need, participant facility IDs and display names, exact blood type, requested/accepted quantities, need urgency, status, notes, and lifecycle timestamps. History includes a safe actor display name and persisted action/status, note, and timestamp.

External request creation rechecks exact-type available inventory at submission; creation does not reserve stock. Acceptance reserves the accepted quantity. Only an active FacilityAdmin at the approved source facility may cancel an eligible `Sent` or `Accepted` request. The requester cannot cancel. Accepted cancellation releases exactly the accepted reservation atomically; fulfilment is also source-admin-only and transfers exactly the accepted quantity.

`IInventoryService` provides scoped inventory operations, availability search, and the need-consumption operation. `InventoryTransactionDto` carries before/after total and reserved balances for auditable mutations.

## Notifications and dashboards

`INotificationService` returns only the current recipient's notifications. Related-record references are included only for allowlisted entity types and records the recipient is authorized to access; otherwise the reference is null. Mark-read is recipient-scoped and idempotent.

`IDashboardService` returns role-specific snapshots: platform facility/review/request summaries for SystemAdmin; facility-scoped inventory, low-stock, open needs, request counts, unread count and recent activity for FacilityAdmin; and the staff member's need counts/recent needs and unread count for FacilityStaff. Data is service-scoped and deterministically ordered.

## Status Definitions

BloodNeed statuses: PendingReview, Searching, FulfilledInternally, FulfilledExternally, Rejected, Cancelled.

BloodRequest statuses: Sent, Accepted, Rejected, Fulfilled, Cancelled.

Status values are changed only through validated service transitions. Phase 5C Razor pages, routes, and navigation remain follow-on UI work.
