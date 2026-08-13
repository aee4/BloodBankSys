# BloodLink

BloodLink is a Blazor Server application for approved hospitals and blood banks to coordinate blood inventory, internal blood needs, and facility-to-facility emergency requests.

## Problem

Facilities often need a controlled way to see their own stock, identify shortages, request exact blood types from another approved facility, and keep traceable inventory and audit records. BloodLink provides that software coordination layer only.

## Scope

BloodLink has no donor functionality. Donor registration, donor profiles, donation appointments, donor dashboards, donor matching, donation history, and donor notifications are outside scope.

The system does not make medical compatibility decisions and does not manage laboratory testing, transport, transfusion, payments, maps, live chat, or patient records.

## Technology Stack

- Blazor Server / ASP.NET Core
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- Bootstrap
- xUnit
- .NET 8 target framework, with the installed .NET 10 SDK pinned in `global.json`

## Roles

- SystemAdmin: approves, rejects, suspends, restores facilities, and reviews platform activity.
- FacilityAdmin: manages one approved hospital or blood bank, its staff, inventory, needs review, network search, and external requests.
- FacilityStaff: views own-facility inventory and submits or tracks internal blood needs.

## Main Workflow

1. A facility registers and creates its first FacilityAdmin account.
2. A SystemAdmin approves or rejects the facility.
3. FacilityAdmin creates staff for the approved facility.
4. FacilityStaff submits an internal blood need when local stock is insufficient.
5. FacilityAdmin searches other approved facilities for exact blood type availability.
6. FacilityAdmin submits an external BloodRequest.
7. Source FacilityAdmin accepts, rejects, cancels, or fulfils according to the state rules.
8. Inventory transactions, notifications, request history, and audit logs preserve traceability.

## Project Structure

- `src/BloodLink.Domain`: entities and enums only.
- `src/BloodLink.Application`: DTOs, contracts, interfaces, and validation boundary.
- `src/BloodLink.Infrastructure`: EF Core, SQL Server, Identity, notifications, repositories, and service implementations.
- `src/BloodLink.Web`: Blazor Server UI and authorization wiring.
- `tests`: project-specific test shells.
- `docs`: blueprint, architecture, contracts, ownership, workflow, database, and testing documents.
- `scripts`: development and database setup guides.

## Local Setup

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/BloodLink.Web/BloodLink.Web.csproj
```

Use user secrets or environment variables for sensitive values. The checked-in connection string targets local development only.

## Useful Documents

- [Project blueprint](docs/PROJECT_BLUEPRINT.md)
- [Project structure](docs/PROJECT_STRUCTURE.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Domain model](docs/DOMAIN_MODEL.md)
- [Workflows](docs/WORKFLOWS.md)
- [Access control matrix](docs/ACCESS_CONTROL_MATRIX.md)
- [Team handoffs](docs/TEAM_HANDOFFS.md)
- [Team ownership](docs/TEAM_OWNERSHIP.md)
- [API contracts](docs/API_CONTRACTS.md)
- [Database guide](docs/DATABASE_GUIDE.md)
- [Testing strategy](docs/TESTING_STRATEGY.md)

## Current Status

Foundation only. The solution compiles and launches with minimal placeholder UI. Complete application features, migrations, seed data, dashboards, and production authentication flows are deferred to owned implementation phases.
