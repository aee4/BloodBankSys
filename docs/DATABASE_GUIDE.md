# Database Guide

BloodLink uses SQL Server, EF Core migrations, and ASP.NET Core Identity. The canonical model is `BloodLinkDbContext`; generated migrations are the only supported schema-change mechanism. Do not apply ad hoc constraint scripts.

## Integrity Policy

- Operational foreign keys use `NO ACTION`; workflow records are not cascade-deleted.
- `BloodInventory`, `BloodNeed`, and `BloodRequest` use SQL Server `rowversion` concurrency tokens.
- Inventory counts cannot be negative, and reserved units cannot exceed total units.
- Requests require positive requested units; accepted units, when supplied, are positive and no greater than requested units.
- A request's source and requesting facilities must differ.
- Facility name, facility registration number, `(FacilityId, BloodType)` inventory, and `FacilityStaff.UserId` are unique.
- Enum values are stored as integers to preserve the established schema contract.

`Facility.CreatedByUserId` and `ApprovedByUserId` are bounded actor identifiers rather than foreign keys. Facility onboarding creates the facility and first administrator together, so a reverse facility-to-user FK would introduce a circular insert dependency. Operational user references are foreign keys.

## Migrations

```powershell
dotnet tool restore
dotnet ef migrations list --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
dotnet ef database update --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
dotnet ef migrations has-pending-model-changes --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
```

`20260921230224_EnforceCanonicalDatabaseIntegrity` is additive: it adds bounds, indexes, foreign keys, and checks without deleting data. It deliberately fails before changing the schema if existing rows violate key invariants or new uniqueness rules. Correct the reported data through an approved operational process and rerun; never modify the migration to discard records.

Apply migrations as an explicit deployment step. Application startup does not call `Migrate` or `EnsureCreated`.

Runtime and EF tooling use `ConnectionStrings:DefaultConnection`. Supply it through Web-project user secrets or `ConnectionStrings__DefaultConnection`; there is no tracked fallback. A credential-free LocalDB example and the complete clean-clone sequence are in `scripts/setup-development.md`.

## Initialization

Initialization is opt-in. With `BloodLink__DatabaseInitialization__Enabled=true`, startup idempotently ensures these roles exist: `SystemAdmin`, `FacilityAdmin`, and `FacilityStaff`.

The first SystemAdmin is also opt-in and requires all settings:

```text
BloodLink__BootstrapAdmin__Enabled=true
BloodLink__BootstrapAdmin__Email=admin@example.org
BloodLink__BootstrapAdmin__Password=<secret from a secret store>
BloodLink__BootstrapAdmin__FirstName=System
BloodLink__BootstrapAdmin__LastName=Administrator
```

The account is facility-less and receives only `SystemAdmin`. Known placeholder passwords are rejected. An existing account is never silently elevated. Disable both initialization switches after successful provisioning, and never commit credentials to configuration or documentation.

See [database setup](database/setup.md), [ERD](database/erd.md), and [data dictionary](database/data-dictionary.md).
