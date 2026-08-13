# BloodLink Project Blueprint

## Scope

BloodLink is a hospital and blood bank inventory coordination system. It supports approved facilities, facility administrators, and authorized facility staff.

BloodLink has no donors, donor accounts, donation history, eligibility logic, donor notifications, donor matching, or donation workflows.

## Goals

- Register hospitals and blood banks as facilities.
- Allow SystemAdmin approval, rejection, suspension, and restoration of facilities.
- Allow FacilityAdmin users to manage staff for their own facility.
- Track exact blood-type inventory for each approved facility.
- Allow FacilityStaff to submit internal blood needs.
- Allow FacilityAdmin users to search exact-type availability at other approved facilities.
- Support external facility-to-facility BloodRequest workflows.
- Preserve traceability through request history, inventory transactions, notifications, and audit logs.

## Non-Goals

- Donor features of any kind.
- Automatic blood compatibility decisions.
- Patient records, diagnoses, clinical history, cross-matching results, transfusion management, payments, maps, ambulance dispatch, live chat, or AI.
- Public detailed inventory viewing.
- Public creation of privileged roles.

## Actors

- SystemAdmin: platform administrator.
- FacilityAdmin: administrator for one approved hospital or blood bank.
- FacilityStaff: staff member belonging to one approved facility.
- Facility: a registered hospital or blood bank.
- Requesting Facility: facility that needs blood.
- Source Facility: facility asked to supply blood.

## Functional Requirements

- Facility onboarding creates a Pending facility and first FacilityAdmin account.
- SystemAdmin approves or rejects onboarding.
- Approved FacilityAdmin users create and manage own-facility staff.
- FacilityStaff users view own-facility inventory and create internal BloodNeed records.
- BloodNeed records include exact BloodType, units, urgency, needed-by time, and a non-identifying note.
- FacilityAdmin users may fulfil, reject, cancel, or move a BloodNeed to Searching.
- FacilityAdmin users search approved active facilities by exact BloodType and minimum AvailableUnits.
- Search excludes the requesting facility and pending or suspended facilities.
- FacilityAdmin users create one active external BloodRequest at a time from a BloodNeed.
- Source FacilityAdmin users accept, reject, cancel, or fulfil requests according to status rules.
- Acceptance reserves units atomically.
- Cancellation releases reservation if one exists.
- Fulfilment transfers stock atomically and marks the linked BloodNeed fulfilled externally.
- Every stock change creates an immutable InventoryTransaction.
- Every request transition creates BloodRequestStatusHistory.
- Notifications are in-app messages and not the source of truth.
- AuditLog stores security and high-impact business actions.

## Non-Functional Requirements

- All timestamps are stored in UTC.
- UI may display Africa/Accra time.
- Units are positive whole numbers.
- Inventory cannot become negative.
- ReservedUnits cannot exceed TotalUnits.
- Service methods enforce role, active user, FacilityId, facility status, and record relationship checks.
- No controller or Razor component updates inventory balances directly.
- Migrations are owned by Database Developer 1 after shared model approval.
- UI must handle loading, empty, success, validation error, service error, unauthorized, facility pending, facility rejected, facility suspended, low stock, and concurrency conflict states.
- Mobile layout must support 360px without horizontal scrolling.

## Entities

- ApplicationUser
- Facility
- FacilityStaff
- BloodInventory
- InventoryTransaction
- BloodNeed
- BloodRequest
- BloodRequestStatusHistory
- Notification
- AuditLog

## Canonical Enums

- UserRole: SystemAdmin, FacilityAdmin, FacilityStaff
- FacilityType: Hospital, BloodBank
- FacilityStatus: Pending, Approved, Rejected, Suspended
- StaffStatus: PendingActivation, Active, Inactive
- BloodType: APositive, ANegative, BPositive, BNegative, ABPositive, ABNegative, OPositive, ONegative
- UrgencyLevel: Routine, Urgent, Emergency
- BloodNeedStatus: PendingReview, Searching, FulfilledInternally, FulfilledExternally, Rejected, Cancelled
- BloodRequestStatus: Sent, Accepted, Rejected, Fulfilled, Cancelled
- InventoryTransactionType: StockIn, Consumption, ManualAdjustment, Reserve, Release, TransferOut, TransferIn
- NotificationType: FacilityDecision, NewNeed, NewExternalRequest, RequestResponse, RequestFulfilled, LowStock, AccountCreated, Security

## Workflows

- Facility onboarding and SystemAdmin decision.
- Staff creation and activation.
- Internal BloodNeed submission and review.
- Insufficient-stock escalation to network search.
- External BloodRequest creation and source response.
- Reservation, cancellation release, fulfilment transfer, and later consumption adjustment.
- Notification creation.
- Audit logging.

## Pages and Modules

- Public/account: `/`, `/facility/register`, `/account/login`, `/account/forgot-password`, `/account/access-denied`
- System administration: `/system/facilities`, `/system/facilities/{id}`, `/system/audit`, `/system/dashboard`
- Facility administration: `/facility/profile`, `/facility/staff`, `/facility/staff/create`
- Inventory: `/inventory`, `/inventory/adjust`, `/inventory/history`, `/inventory/search`
- Internal needs: `/needs/new`, `/needs/mine`, `/needs`, `/needs/{id}`
- External requests: `/requests/sent`, `/requests/received`, `/requests/{id}`
- Notifications: `/notifications`
- Dashboard: `/dashboard`

## Authorization Rules

Service methods must verify role, active status, FacilityId, facility status, and relationship to the target record. Hidden UI controls are not authorization.

Ordinary FacilityStaff users do not receive facility administration privileges. FacilityAdmin users operate only within their own facility except when viewing requests where their facility is the requesting or source facility.

## Team Roles

- Project Manager / Team Lead
- UI/UX Designer
- Frontend Developer 1 - Facility Onboarding & Staff Management
- Frontend Developer 2 - Inventory & Availability Search
- Frontend Developer 3 - Needs, Requests, Dashboard & Notifications
- Backend Developer 1 - Facility & Staff Management
- Backend Developer 2 - Inventory & Availability Search
- Backend Developer 3 - Needs, Requests, Dashboard & Notifications
- Database Developer 1 - Schema, EF Core & Migrations
- Database Developer 2 - Integrity, Queries & Performance
- Authentication & Security Developer
- DevOps + QA/Test Engineer

## Acceptance Criteria

- Facility registration, approval, rejection, suspension, and restoration follow the role rules.
- FacilityAdmin manages only own-facility staff.
- FacilityStaff can view own inventory and submit or track own internal needs.
- FacilityAdmin can adjust own inventory, search exact-type availability, and create external requests from needs.
- Source FacilityAdmin can accept only with sufficient available units.
- Acceptance, cancellation, and fulfilment are atomic.
- Inventory transactions, histories, notifications, dashboards, and audit logs are facility-scoped.
- Protected pages and service operations reject anonymous, wrong-role, inactive, and cross-facility access.
- Fresh environment can restore, migrate after approved migrations, seed, run, and test from documentation.
- No donor entity, donor page, donor service, donor table, donor role, donor test, or donor workflow exists.

## Risks and Assumptions

- The entity skeleton is intentionally not a final EF model.
- Migrations are deferred until cross-team model approval.
- The checked-in connection string is local-only.
- ApplicationUser lives in Infrastructure because it depends on ASP.NET Identity.
- The blueprint uses BloodNeed, while the prompt used BloodRequestItem and InterFacilityRequest. The foundation follows the blueprint's canonical BloodNeed and BloodRequest terminology.
