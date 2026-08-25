# Data dictionary

## Facility

| Column | Type | Nullability | Notes |
| --- | --- | --- | --- |
| Id | uniqueidentifier | No | Primary key. |
| Name | nvarchar(200) | No | Human name of the approved facility. Unique with registration number. |
| FacilityType | int | No | Enum: Hospital = 0, BloodBank = 1. |
| RegistrationNumber | nvarchar(100) | No | External registration identifier. Unique with facility name. |
| Region | nvarchar(100) | No | Regional division. |
| City | nvarchar(100) | No | City or municipality. |
| Address | nvarchar(500) | No | Street or mail address. |
| ContactEmail | nvarchar(256) | No | Operational contact email. |
| ContactPhone | nvarchar(30) | No | Operational contact phone. |
| Status | int | No | Enum: Pending = 0, Approved = 1, Rejected = 2, Suspended = 3. |
| RejectionReason | nvarchar(500) | Yes | Required when a facility is rejected or suspended. |
| CreatedByUserId | nvarchar(450) | No | User who created the facility record. |
| ApprovedByUserId | nvarchar(450) | Yes | Identity approver when the facility is approved. |
| CreatedAtUtc | datetime2 | No | Record creation timestamp. |
| ApprovedAtUtc | datetime2 | Yes | Approval timestamp. |

## FacilityStaff

| Column | Type | Nullability | Notes |
| --- | --- | --- | --- |
| Id | uniqueidentifier | No | Primary key. |
| FacilityId | uniqueidentifier | No | FK to Facility. Restrictive delete. |
| UserId | nvarchar(450) | No | FK to AspNetUsers. Unique per facility and user. |
| Status | int | No | Enum: PendingActivation = 0, Active = 1, Inactive = 2. |
| CreatedByAdminId | nvarchar(450) | No | Admin who created the relationship. |
| CreatedAtUtc | datetime2 | No | Relationship creation time. |
| DeactivatedAtUtc | datetime2 | Yes | Deactivation timestamp. |
| StatusReason | nvarchar(500) | Yes | Reason for status change. |

## BloodInventory

| Column | Type | Nullability | Notes |
| --- | --- | --- | --- |
| Id | uniqueidentifier | No | Primary key. |
| FacilityId | uniqueidentifier | No | FK to Facility. Restrictive delete. |
| BloodType | int | No | Enum and exact match; unique with facility. |
| TotalUnits | int | No | Total current units. |
| ReservedUnits | int | No | Units currently reserved. |
| LowStockThreshold | int | No | Threshold for low-stock alerts. |
| UpdatedAtUtc | datetime2 | No | Last stock change time. |
| RowVersion | rowversion | No | Concurrency token. |

## InventoryTransaction

| Column | Type | Nullability | Notes |
| --- | --- | --- | --- |
| Id | uniqueidentifier | No | Primary key. |
| BloodInventoryId | uniqueidentifier | No | FK to BloodInventory. Restrictive delete. |
| TransactionType | int | No | Enum for stock movements. |
| TotalUnitsChange | int | No | Net change in total stock. |
| ReservedUnitsChange | int | No | Net change in reserved stock. |
| TotalAfter | int | No | Total units after the change. |
| ReservedAfter | int | No | Reserved units after the change. |
| Reason | nvarchar(500) | No | Safe summary of the movement. |
| ReferenceType | nvarchar(100) | Yes | Related entity or domain concept. |
| ReferenceId | uniqueidentifier | Yes | Optional reference key. |
| PerformedByUserId | nvarchar(450) | No | User who performed the action. |
| CreatedAtUtc | datetime2 | No | Transaction timestamp. |

## BloodNeed

| Column | Type | Nullability | Notes |
| --- | --- | --- | --- |
| Id | uniqueidentifier | No | Primary key. |
| FacilityId | uniqueidentifier | No | Requesting facility. FK to Facility. |
| RequestedByUserId | nvarchar(450) | No | User who created the need. |
| BloodType | int | No | Requested exact blood type. |
| UnitsNeeded | int | No | Required units. |
| Urgency | int | No | Enum: Routine, Urgent, Emergency. |
| NeededByUtc | datetime2 | No | Deadline for fulfillment. |
| Note | nvarchar(1000) | Yes | Safe operational note without patient data. |
| Status | int | No | Enum: PendingReview, Searching, FulfilledInternally, FulfilledExternally, Rejected, Cancelled. |
| DecisionReason | nvarchar(1000) | Yes | Safe explanation for a decision. |
| CreatedAtUtc | datetime2 | No | Creation timestamp. |
| UpdatedAtUtc | datetime2 | No | Last update timestamp. |
| RowVersion | rowversion | No | Concurrency token. |

## BloodRequest

| Column | Type | Nullability | Notes |
| --- | --- | --- | --- |
| Id | uniqueidentifier | No | Primary key. |
| BloodNeedId | uniqueidentifier | No | FK to BloodNeed. |
| RequestingFacilityId | uniqueidentifier | No | Facility initiating the request. |
| SourceFacilityId | uniqueidentifier | No | Facility fulfilling or being asked to fulfill. |
| BloodType | int | No | Exact requested blood type. |
| UnitsRequested | int | No | Requested units. |
| UnitsAccepted | int | Yes | Accepted units when a response is made. |
| Status | int | No | Enum: Sent, Accepted, Rejected, Fulfilled, Cancelled. |
| RequestNote | nvarchar(1000) | Yes | Request explanation. |
| ResponseNote | nvarchar(1000) | Yes | Response summary. |
| RequestedByAdminId | nvarchar(450) | No | Requesting facility admin. |
| RespondedByAdminId | nvarchar(450) | Yes | Admin who responded. |
| FulfilledByAdminId | nvarchar(450) | Yes | Admin who completed fulfillment. |
| CreatedAtUtc | datetime2 | No | Request creation time. |
| RespondedAtUtc | datetime2 | Yes | Response time. |
| FulfilledAtUtc | datetime2 | Yes | Fulfillment timestamp. |
| RowVersion | rowversion | No | Concurrency token. |

## BloodRequestStatusHistory

| Column | Type | Nullability | Notes |
| --- | --- | --- | --- |
| Id | uniqueidentifier | No | Primary key. |
| BloodRequestId | uniqueidentifier | No | FK to BloodRequest. |
| FromStatus | int | Yes | Previous status value. |
| ToStatus | int | No | New status value. |
| Note | nvarchar(1000) | Yes | Safe transition note. |
| ChangedByUserId | nvarchar(450) | No | Identity actor. |
| ChangedAtUtc | datetime2 | No | Transition timestamp. |

## Notification

| Column | Type | Nullability | Notes |
| --- | --- | --- | --- |
| Id | uniqueidentifier | No | Primary key. |
| RecipientUserId | nvarchar(450) | No | Linked user. |
| NotificationType | int | No | Enum. |
| Title | nvarchar(200) | No | Short title. |
| Message | nvarchar(1000) | No | Message text. |
| RelatedEntityType | nvarchar(100) | Yes | Domain type label. |
| RelatedEntityId | uniqueidentifier | Yes | Optional linked entity. |
| IsRead | bit | No | Read/unread flag. |
| CreatedAtUtc | datetime2 | No | Creation timestamp. |
| ReadAtUtc | datetime2 | Yes | Read timestamp. |

## AuditLog

| Column | Type | Nullability | Notes |
| --- | --- | --- | --- |
| Id | uniqueidentifier | No | Primary key. |
| ActorUserId | nvarchar(450) | Yes | Optional actor. |
| Action | nvarchar(200) | No | Audit action label. |
| EntityType | nvarchar(200) | No | Business entity name. |
| EntityId | uniqueidentifier | Yes | Optional referenced entity. |
| FacilityId | uniqueidentifier | Yes | Optional facility context. |
| Summary | nvarchar(1000) | No | Safe summary of the event. |
| IpAddress | nvarchar(45) | Yes | Optional source IP. |
| CreatedAtUtc | datetime2 | No | Audit timestamp. |

## Identity tables

The model reuses ASP.NET Identity tables for roles and users. This is the canonical user and role source. The project scope intentionally excludes donor tables, donor data, and donor workflows.
