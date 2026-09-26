# Data Dictionary

All domain primary keys are `uniqueidentifier`; Identity user and role keys are `nvarchar(450)`. All timestamps are UTC. Domain enums are stored as `int`.

| Table | Purpose and key fields | Integrity |
|---|---|---|
| `Facilities` | Facility profile, registration, location, contact, status, actor IDs, timestamps | Unique `Name`; unique `RegistrationNumber`; bounded strings; status and region/city indexes |
| `AspNetUsers` | Identity account plus names, optional `FacilityId`, activation and password-change state | Facility FK `NO ACTION`; names max 100; facility index |
| `FacilityStaff` | One staff membership for an Identity user | Unique `UserId`; facility, user, and creator FKs `NO ACTION`; facility/status index |
| `BloodInventory` | Per-facility stock by exact blood type | Unique facility/type; total, reserved, threshold non-negative; reserved <= total; `rowversion` |
| `InventoryTransactions` | Immutable stock-change ledger with before/after total and reserved balances | Inventory and actor FKs `NO ACTION`; a non-zero change; resulting balances valid |
| `BloodNeeds` | Internal request for an exact blood type and quantity | Facility/requester FKs `NO ACTION`; units > 0; `rowversion` |
| `BloodRequests` | Facility-to-facility request linked to a need | Need, facility, and actor FKs `NO ACTION`; positive quantities; accepted <= requested; facilities differ; `rowversion` |
| `BloodRequestStatusHistory` | Append-only request transition history | Request and actor FKs `NO ACTION`; request/time index |
| `BloodNeedStatusHistory` | Append-only need transition history | Need and actor FKs `NO ACTION`; need/time index |
| `Notifications` | User notification and optional related entity reference | Recipient FK `NO ACTION`; recipient/read/time index |
| `AuditLogs` | Append-only platform audit record | Optional facility/actor FKs `NO ACTION`; facility/time and entity indexes |

## String Bounds

- Identity names, facility region/city: 100
- Facility name: 200; registration number: 100
- Facility address: 500; email: 256; phone: 30; rejection reason: 500
- Notes, decisions, responses, notification message, audit summary: 1000
- Notification title, audit action/entity type: 200
- Reference and related-entity types: 100; IP address: 45
- Identity actor IDs: 450

`AvailableUnits` is derived as `TotalUnits - ReservedUnits` and is not stored. Polymorphic trace references are not foreign keys.
