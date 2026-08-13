# Architecture

## Layer Responsibilities

- Domain: canonical entities and enums. No infrastructure or UI dependencies.
- Application: DTOs, interfaces, role constants, policy names, and use-case contracts.
- Infrastructure: EF Core, SQL Server, Identity, repository implementations, service implementations, notifications, migrations, and seed data.
- Web: Blazor Server components, page routing, UI states, authentication middleware, and authorization policy usage.

## Allowed Dependencies

- `BloodLink.Web` references `BloodLink.Application` and `BloodLink.Infrastructure`.
- `BloodLink.Infrastructure` references `BloodLink.Application` and `BloodLink.Domain`.
- `BloodLink.Application` references `BloodLink.Domain`.
- `BloodLink.Domain` references no project.

## Authentication and Authorization Boundary

ASP.NET Core Identity is configured in Infrastructure and consumed by Web. Service methods must enforce authorization using the signed-in user's role, active status, FacilityId, facility status, and relationship to the target record. UI hiding is only presentation.

## Database Access Boundary

Only Infrastructure accesses EF Core and SQL Server. Web components must use Application contracts and must never update inventory balances or request statuses directly.

## Notification Boundary

Notifications are created by application services after business state changes. Notification records are not the source of truth; linked domain records determine actual status.

## Shared Contracts

DTOs and interfaces live in Application. EF entities must not be returned directly to Razor components.

## Cross-Team Conflict Prevention

- Shared entities, enums, DTO names, routes, status maps, policies, and migrations require owner review.
- Only Database Developer 1 commits EF migrations.
- Only Security owns Identity configuration, ApplicationUser, policies, and current-user implementation.
- Feature folders are owned by the team role listed in `TEAM_OWNERSHIP.md`.
