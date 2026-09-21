# Database Setup

## Prerequisites

- .NET 8 SDK or the repository-pinned compatible SDK
- SQL Server or LocalDB
- `dotnet-ef` 8.0.x

## Configure and Apply

Store the connection string outside source control:

```powershell
dotnet user-secrets set --project src/BloodLink.Web "ConnectionStrings:BloodLinkDatabase" "Server=(localdb)\mssqllocaldb;Database=BloodLink_Development;Trusted_Connection=True;MultipleActiveResultSets=true"
dotnet ef migrations list --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
dotnet ef database update --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
```

Run `scripts/integrity_queries.sql` before upgrading a populated database. The Phase 2 migration repeats critical checks and fails without deleting data when cleanup is required.

## Provision Roles and Initial Administrator

Set `BloodLink__DatabaseInitialization__Enabled=true` for one controlled startup. Roles are safe to seed repeatedly. To create the first administrator, also provide all `BloodLink__BootstrapAdmin__*` values documented in `docs/DATABASE_GUIDE.md` through environment variables or a secret store. Remove or disable the switches immediately afterward.

## Verify

```powershell
dotnet ef migrations list --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
dotnet test tests/BloodLink.Infrastructure.Tests/BloodLink.Infrastructure.Tests.csproj
```

Confirm the latest migration is applied, the three canonical roles exist, and the bootstrap account has no `FacilityId` and only the `SystemAdmin` role.

## Rollback

Back up a populated database first. To roll back, target the migration immediately before Phase 2:

```powershell
dotnet ef database update 20260814075935_InitialCreate --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
```

Rollback removes Phase 2 constraints and indexes but does not delete operational rows or bootstrap identities. Remove an unwanted bootstrap account only through an audited administrative process.
