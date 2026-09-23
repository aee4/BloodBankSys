# Database Setup

BloodLink uses SQL Server and the standard `ConnectionStrings:DefaultConnection` key. No connection string is tracked and the application never migrates, creates, or deletes a database automatically.

## Configure

```powershell
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=BloodLink_Development;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" --project src/BloodLink.Web
```

Environment-variable equivalent: `ConnectionStrings__DefaultConnection=<LOCAL_CONNECTION_STRING>`.

## Apply And Verify

```powershell
dotnet ef migrations list --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
dotnet ef database update --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
dotnet ef migrations has-pending-model-changes --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
```

Run `scripts/integrity_queries.sql` before and after upgrading a populated database. The Phase 2 migration fails without deleting data when cleanup is required. See `scripts/setup-development.md` for secure role/bootstrap provisioning and explicitly verified disposable-database cleanup.
