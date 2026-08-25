# Database setup

## Prerequisites

- .NET 8 SDK
- SQL Server LocalDB or another SQL Server instance reachable from your development machine
- A connection string named `BloodLinkDatabase` in [src/BloodLink.Web/appsettings.Development.json](../../src/BloodLink.Web/appsettings.Development.json) or environment variables

## One-step migration flow

From the repository root:

```bash
dotnet restore
dotnet ef database update --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
```

This applies the ordered migration chain and creates the schema in a fresh database.

## Seed behavior

When the web app starts, the infrastructure seed step does the following idempotently:

- creates the Identity roles `SystemAdmin`, `FacilityAdmin`, and `FacilityStaff`
- creates safe demo facilities for approved hospital and blood bank scenarios
- creates corresponding demo users with no stored credentials
- creates one blood inventory row for every approved facility and each canonical blood type

The seed logic is intentionally non-destructive and safe for repeat runs.

## No-donor boundary

The migration chain, seed data, and documentation intentionally avoid donor tables, donor columns, donor relationships, and donor workflows. The canonical model is facility-driven only.
