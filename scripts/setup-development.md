# Development Setup

These steps start from a fresh clone on Windows. Run commands from the repository root.

## 1. Prerequisites

Install Git, the x64 .NET 8 SDK, the x64 ASP.NET Core 8 runtime, and SQL Server LocalDB or another development SQL Server. The repository remains on `net8.0` and includes a local EF Core 8 tool manifest.

`global.json` requests SDK `8.0.400` and sets `rollForward` to `latestFeature`. The .NET CLI may therefore select the highest installed compatible feature band within the .NET 8 major/minor line, including compatible .NET 8 servicing and feature-band updates. This policy does not authorize selection of .NET 9 or .NET 10. A compatible x64 .NET 8 SDK must still be installed, and installing a later major version alone does not supply the required ASP.NET Core 8 runtime.

```powershell
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

The CLI must report `Architecture: x64`; an `8.0.x` SDK and both `Microsoft.NETCore.App 8.0.x` and `Microsoft.AspNetCore.App 8.0.x` must appear. On Windows, install missing components with official Microsoft installers or:

```powershell
winget install --id Microsoft.DotNet.SDK.8 --exact --architecture x64
winget install --id Microsoft.DotNet.AspNetCore.8 --exact --architecture x64 --force
```

## 2. Restore And Build

```powershell
git clone https://github.com/aee4/BloodBankSys.git
Set-Location BloodBankSys
dotnet tool restore
dotnet restore BloodLink.sln
dotnet build BloodLink.sln --no-restore
```

## 3. Configure Local Secrets

The Web project is initialized for .NET user secrets. Running the initialization command again is harmless if the ID already exists.

```powershell
dotnet user-secrets init --project src/BloodLink.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=BloodLink_Development;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" --project src/BloodLink.Web
```

The LocalDB string is a credential-free Windows example. Choose a unique disposable database name for verification. Other SQL Server instances are supported, but credentials belong in user secrets or `ConnectionStrings__DefaultConnection`, never tracked files.

Missing configuration stops database initialization, EF commands, or the first database-backed operation with an error naming `ConnectionStrings:DefaultConnection` without printing its value.

## 4. Apply Migrations

```powershell
dotnet ef migrations list --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
dotnet ef database update --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
dotnet ef migrations has-pending-model-changes --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web
```

The final command must report no model changes. Confirm the two expected rows in `__EFMigrationsHistory` using SQL Server Object Explorer or `sqlcmd`. The application never applies migrations or creates/deletes databases automatically.

## 5. Provision Roles And The First Administrator

Role initialization creates exactly `SystemAdmin`, `FacilityAdmin`, and `FacilityStaff`. Store temporary bootstrap values in user secrets:

```powershell
dotnet user-secrets set "BloodLink:DatabaseInitialization:Enabled" "true" --project src/BloodLink.Web
dotnet user-secrets set "BloodLink:BootstrapAdmin:Enabled" "true" --project src/BloodLink.Web
dotnet user-secrets set "BloodLink:BootstrapAdmin:Email" "<ADMIN_EMAIL>" --project src/BloodLink.Web
dotnet user-secrets set "BloodLink:BootstrapAdmin:Password" "<STRONG_TEMPORARY_PASSWORD>" --project src/BloodLink.Web
dotnet user-secrets set "BloodLink:BootstrapAdmin:FirstName" "<FIRST_NAME>" --project src/BloodLink.Web
dotnet user-secrets set "BloodLink:BootstrapAdmin:LastName" "<LAST_NAME>" --project src/BloodLink.Web
```

The password must satisfy Identity's eight-character minimum and must not be a known placeholder. Start the application once, confirm provisioning, then disable initialization and remove bootstrap identity values:

```powershell
dotnet user-secrets set "BloodLink:DatabaseInitialization:Enabled" "false" --project src/BloodLink.Web
dotnet user-secrets set "BloodLink:BootstrapAdmin:Enabled" "false" --project src/BloodLink.Web
dotnet user-secrets remove "BloodLink:BootstrapAdmin:Email" --project src/BloodLink.Web
dotnet user-secrets remove "BloodLink:BootstrapAdmin:Password" --project src/BloodLink.Web
dotnet user-secrets remove "BloodLink:BootstrapAdmin:FirstName" --project src/BloodLink.Web
dotnet user-secrets remove "BloodLink:BootstrapAdmin:LastName" --project src/BloodLink.Web
```

Rerunning initialization is idempotent: roles and the configured administrator are not duplicated. An unrelated existing account is never elevated.

## 6. Run And Verify

```powershell
dotnet run --project src/BloodLink.Web
```

The launch profile listens on `https://localhost:7080` and `http://localhost:5080`; use the exact `Now listening on` URL printed by the process. In another terminal:

```powershell
curl.exe --head --max-redirs 0 http://localhost:5080/
curl.exe --silent --show-error --output NUL --write-out "%{http_code}" https://localhost:7080/
```

The HTTP request must report `HTTP/1.1 307 Temporary Redirect`; the HTTPS request must print `200` when the development certificate is trusted. To diagnose an untrusted local development certificate only, run `curl.exe --insecure --silent --show-error --output NUL --write-out "%{http_code}" https://localhost:7080/`. Never use `--insecure` for production verification. The HTTPS check uses `GET` and discards the response body because the application endpoint does not support `HEAD`.

Windows PowerShell 5.1 can alternatively verify the final response while following the redirect:

```powershell
$response = Invoke-WebRequest `
    -Uri "http://localhost:5080/" `
    -UseBasicParsing
$response.StatusCode
```

Because `Invoke-WebRequest` follows redirects by default, this alternative normally reports the final HTTPS `200` response rather than the initial `307`.

Stop the server with `Ctrl+C`.

```powershell
dotnet test BloodLink.sln
dotnet format BloodLink.sln --verify-no-changes
dotnet list BloodLink.sln package --vulnerable --include-transitive
git diff --check
```

## Troubleshooting

- Missing ASP.NET Core: install the x64 ASP.NET Core 8 runtime and confirm `Microsoft.AspNetCore.App 8.0.x` under `C:\Program Files\dotnet`.
- Wrong architecture: `dotnet --info` must report x64; check `where.exe dotnet` for an x86 executable earlier on `PATH`.
- Missing connection string: set `ConnectionStrings:DefaultConnection` or use `ConnectionStrings__DefaultConnection`.
- LocalDB unavailable: install SQL Server Express LocalDB, run `sqllocaldb info`, or configure another development SQL Server.
- Migration failure: verify the target database, inspect the migration error, correct invalid data, and rerun. Never bypass constraints.
- Bootstrap does nothing: both initialization switches must be true for the first run, and all bootstrap fields are required.
- HTTPS certificate: run `dotnet dev-certs https --clean`, then `dotnet dev-certs https --trust`. HTTP remains available for smoke testing.
- Port occupied: stop the conflict or run `dotnet run --project src/BloodLink.Web --urls http://localhost:0` and use the printed URL.
- Duplicate EF InMemory warning: this known test-project warning is deferred to Phase 4 and does not block setup.

## Disposable Cleanup

Before deletion, verify the exact database name and that it was created for local testing. Then target that explicit name from `master`; never use a wildcard or shared database. Example for `BloodLink_Development`:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d master -E -Q "SELECT name FROM sys.databases WHERE name = N'BloodLink_Development'"
sqlcmd -S "(localdb)\MSSQLLocalDB" -d master -E -Q "ALTER DATABASE [BloodLink_Development] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [BloodLink_Development]"
```
