# Entity Relationship Map

```mermaid
erDiagram
    Facilities ||--o{ AspNetUsers : contains
    Facilities ||--o{ FacilityStaff : employs
    AspNetUsers ||--o| FacilityStaff : identifies
    AspNetUsers ||--o{ FacilityStaff : created_by
    Facilities ||--o{ BloodInventory : owns
    BloodInventory ||--o{ InventoryTransactions : records
    AspNetUsers ||--o{ InventoryTransactions : performed_by
    Facilities ||--o{ BloodNeeds : raises
    AspNetUsers ||--o{ BloodNeeds : requested_by
    BloodNeeds ||--o{ BloodRequests : produces
    Facilities ||--o{ BloodRequests : requesting
    Facilities ||--o{ BloodRequests : source
    BloodRequests ||--o{ BloodRequestStatusHistory : tracks
    AspNetUsers ||--o{ Notifications : receives
    Facilities ||--o{ AuditLogs : scopes
```

All displayed operational relationships use `NO ACTION` deletion. `BloodRequest` also references users for request, response, and fulfilment; status history references its changing user; audit logs optionally reference an actor. Identity role, claim, login, and token tables use the standard ASP.NET Core Identity schema.

Facility creator/approver values are audit identifiers, not relational links, to avoid the circular facility/initial-admin insertion dependency.
