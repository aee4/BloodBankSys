# Domain Model

The blueprint's canonical model uses `BloodNeed` for internal needs and `BloodRequest` for facility-to-facility requests. The prompt's expected `BloodRequestItem` and `InterFacilityRequest` concepts are therefore represented by `BloodNeed`, `BloodRequest`, and request DTOs rather than additional entity classes.

## ApplicationUser

- Purpose: ASP.NET Identity user for SystemAdmin, FacilityAdmin, or FacilityStaff.
- Important fields: Id, FirstName, LastName, Email, PhoneNumber, FacilityId, IsActive, MustChangePassword, CreatedAtUtc, LastLoginAtUtc.
- Relationships: belongs to a Facility except SystemAdmin; creates needs, administers requests, receives notifications, and performs audited actions.
- Owning team: Authentication & Security Developer.
- Validation responsibility: Security and Identity services.
- Security/privacy: no passwords outside Identity; FacilityId is nullable only for SystemAdmin.

## Facility

- Purpose: registered hospital or blood bank.
- Important fields: Name, FacilityType, RegistrationNumber, Region, City, Address, ContactEmail, ContactPhone, Status, RejectionReason, CreatedByUserId, ApprovedByUserId.
- Relationships: has users, staff records, inventory, needs, sent requests, received requests, audit records.
- Owning team: Backend Developer 1, with PM contract review.
- Validation responsibility: Facility service and EF unique constraints.
- Security/privacy: operational access requires Approved status.

## FacilityStaff

- Purpose: links a user to staff lifecycle status for one facility.
- Important fields: FacilityId, UserId, Status, CreatedByAdminId, CreatedAtUtc, DeactivatedAtUtc, StatusReason.
- Relationships: belongs to Facility and ApplicationUser.
- Owning team: Backend Developer 1 with Security review.
- Validation responsibility: Staff service.
- Security/privacy: FacilityAdmin can manage only own-facility staff.

## BloodInventory

- Purpose: current stock for one facility and exact blood type.
- Important fields: FacilityId, BloodType, TotalUnits, ReservedUnits, LowStockThreshold, UpdatedAtUtc, RowVersion.
- Relationships: belongs to Facility and has InventoryTransaction records.
- Owning team: Backend Developer 2.
- Validation responsibility: Inventory service and EF concurrency.
- Security/privacy: availability outside the facility is exposed only through approved search results.

## InventoryTransaction

- Purpose: immutable audit of inventory and reservation changes.
- Important fields: BloodInventoryId, TransactionType, TotalUnitsChange, ReservedUnitsChange, TotalAfter, ReservedAfter, Reason, ReferenceType, ReferenceId, PerformedByUserId, CreatedAtUtc.
- Relationships: belongs to BloodInventory and may reference a BloodRequest or adjustment.
- Owning team: Backend Developer 2.
- Validation responsibility: Inventory service.
- Security/privacy: never hard-delete; reason must avoid patient-identifying details.

## BloodNeed

- Purpose: internal staff need inside a requesting facility.
- Important fields: FacilityId, RequestedByUserId, BloodType, UnitsNeeded, Urgency, NeededByUtc, Note, Status, DecisionReason, CreatedAtUtc, UpdatedAtUtc, RowVersion.
- Relationships: belongs to Facility and ApplicationUser; has BloodRequest records.
- Owning team: Backend Developer 3.
- Validation responsibility: BloodNeed service.
- Security/privacy: no patient names, identifiers, diagnoses, clinical history, or cross-match results.

## BloodRequest

- Purpose: external request from one facility to another, linked to a BloodNeed.
- Important fields: BloodNeedId, RequestingFacilityId, SourceFacilityId, BloodType, UnitsRequested, UnitsAccepted, Status, RequestNote, ResponseNote, RequestedByAdminId, RespondedByAdminId, FulfilledByAdminId, timestamps, RowVersion.
- Relationships: links requesting and source facilities; has status history.
- Owning team: Backend Developer 3 with Backend Developer 2 for inventory effects.
- Validation responsibility: BloodRequest service.
- Security/privacy: visible only to involved facilities and authorized platform oversight.

## BloodRequestStatusHistory

- Purpose: immutable request timeline.
- Important fields: BloodRequestId, FromStatus, ToStatus, Note, ChangedByUserId, ChangedAtUtc.
- Relationships: belongs to BloodRequest.
- Owning team: Backend Developer 3.
- Validation responsibility: BloodRequest service.
- Security/privacy: notes must be safe summaries.

## Notification

- Purpose: in-app message for one user.
- Important fields: RecipientUserId, NotificationType, Title, Message, RelatedEntityType, RelatedEntityId, IsRead, CreatedAtUtc, ReadAtUtc.
- Relationships: belongs to ApplicationUser and may link to business records.
- Owning team: Backend Developer 3.
- Validation responsibility: Notification service.
- Security/privacy: not the source of truth and must not expose cross-facility details.

## AuditLog

- Purpose: security and high-impact business audit record.
- Important fields: ActorUserId, Action, EntityType, EntityId, FacilityId, Summary, IpAddress, CreatedAtUtc.
- Relationships: optionally linked to a user, facility, and entity.
- Owning team: Authentication & Security Developer with QA review.
- Validation responsibility: security and feature services.
- Security/privacy: stores safe summaries only, never secrets or patient data.
