# Team Handoffs

## Project Manager / Team Lead - Emmanuel Eyram Korku Agbetor

- Mission: own scope, contracts, workflow truth, integration order, and final acceptance.
- Owned folders: root solution/config files, `docs`.
- Owned documents: README, architecture, contracts, decisions, demo script, PR template.
- Required inputs: all team contract changes and scope decisions.
- Deliverables: approved contracts, issue board, PR flow, traceability, final demo scenario.
- Interfaces exposed: canonical names, statuses, routes, ownership rules.
- Dependencies: all feature teams.
- Acceptance criteria: no donor terminology, shared contracts merged before dependent work, fresh clone can restore/build/test/run.
- Required tests: traceability and final acceptance evidence.
- Handoff recipient: all team roles.
- Must not modify without coordination: owned feature implementations and migrations.

## UI/UX Designer - Eyram Mami Araba Kumah

- Mission: design responsive role-based experiences without inventing workflows.
- Owned folders: design documentation under `docs/design`.
- Owned documents: user flows, screen specs, component inventory, accessibility notes.
- Required inputs: blueprint routes, statuses, permissions.
- Deliverables: approved designs for onboarding, dashboards, facility/staff, inventory/search, needs, requests, notifications, and system review.
- Interfaces exposed: UI states and component guidance.
- Dependencies: PM, frontend developers.
- Acceptance criteria: canonical statuses/actions, clear role limits, 360px and desktop coverage.
- Required tests: design review and accessibility notes.
- Handoff recipient: Frontend Developers 1-3.
- Must not modify without coordination: entities, permissions, business transitions, database structure.

## Frontend Developer 1 - Selorm Sem

- Mission: implement facility onboarding, system facility review UI, facility profile, and staff management.
- Owned folders: `src/BloodLink.Web/Components/Facilities`, future Staff components, UI tests for facilities/staff.
- Owned documents: UI notes for onboarding and staff.
- Required inputs: Backend Developer 1 contracts, Security auth rules, UI/UX designs.
- Deliverables: `/facility/register`, `/facility/profile`, `/facility/staff`, `/facility/staff/create`, `/system/facilities`, `/system/facilities/{id}`.
- Interfaces exposed: validated UI forms and route usage.
- Dependencies: Backend Developer 1, Security, Database 1.
- Acceptance criteria: no public privileged role assignment, pending facilities blocked, own-facility staff only.
- Required tests: UI tests for facility/staff flows.
- Handoff recipient: QA and PM.
- Must not modify without coordination: Identity internals, service rules, migrations.

## Frontend Developer 2 - Fauziya Adjeley Adjei

- Mission: implement inventory, adjustment, history, low-stock, and availability search UI.
- Owned folders: `src/BloodLink.Web/Components/Inventory`, UI inventory tests.
- Owned documents: UI notes for inventory/search.
- Required inputs: Backend Developer 2 contracts and UI/UX designs.
- Deliverables: `/inventory`, `/inventory/adjust`, `/inventory/history`, `/inventory/search`.
- Interfaces exposed: inventory and search component behavior.
- Dependencies: Backend Developer 2, Security, DB teams.
- Acceptance criteria: all blood types display, staff view-only, available units from service, safe concurrency errors.
- Required tests: UI inventory/search tests.
- Handoff recipient: QA and Backend Developer 3 for request creation flow.
- Must not modify without coordination: DbContext, balance logic, reservation rules.

## Frontend Developer 3 - Eastwood Tweneboah Osei

- Mission: implement needs, requests, dashboard, and notifications UI.
- Owned folders: `src/BloodLink.Web/Components/Requests`, `Dashboard`, `Notifications`, and future Needs components.
- Owned documents: UI notes for needs/requests/dashboard/notifications.
- Required inputs: Backend Developer 3 contracts, Backend Developer 2 inventory integration, UI/UX designs.
- Deliverables: `/needs/new`, `/needs/mine`, `/needs`, `/needs/{id}`, `/requests/sent`, `/requests/received`, `/requests/{id}`, `/dashboard`, `/notifications`.
- Interfaces exposed: status timeline and role-aware actions.
- Dependencies: Backend Developer 3, Backend Developer 2, Security.
- Acceptance criteria: staff cannot search/send external requests, admins see valid own-facility actions, statuses from DTOs.
- Required tests: UI tests for needs, requests, dashboards, notifications.
- Handoff recipient: QA and PM.
- Must not modify without coordination: state machines, DbContext, inventory transfer logic.

## Backend Developer 1 - Poku Nancy

- Mission: implement facility lifecycle and staff management.
- Owned folders: `src/BloodLink.Application` facility/staff contracts, future infrastructure service folders for facilities/staff, facility/staff unit tests.
- Owned documents: facility and staff contract notes.
- Required inputs: PM contracts, Security identity abstractions, DB model.
- Deliverables: Facility service, Staff service, validation, tests.
- Interfaces exposed: `IFacilityService`, `IStaffService`.
- Dependencies: Security, Database 1, Frontend Developer 1.
- Acceptance criteria: Pending registration, SystemAdmin decisions, own-facility staff management, blocked suspended/pending operations.
- Required tests: unit tests for facility/staff.
- Handoff recipient: Frontend Developer 1 and QA.
- Must not modify without coordination: ApplicationUser, Identity configuration, policies, Razor pages, migrations.

## Backend Developer 2 - Jephthah Peprah

- Mission: implement inventory integrity, reservations, transactions, low stock, and exact-type search.
- Owned folders: `src/BloodLink.Domain/Entities/BloodInventory.cs`, `InventoryTransaction.cs`, inventory contracts/services/tests.
- Owned documents: inventory contract notes.
- Required inputs: DB concurrency guidance, Security facility scoping.
- Deliverables: `IInventoryService`, inventory service, stock transaction tests.
- Interfaces exposed: availability search and transfer operations.
- Dependencies: Backend Developer 3, Database 1/2, Frontend Developer 2.
- Acceptance criteria: TotalUnits >= ReservedUnits >= 0, immutable transactions, exact-type search, atomic transfer.
- Required tests: inventory unit and integration tests.
- Handoff recipient: Backend Developer 3, Frontend Developer 2, QA.
- Must not modify without coordination: migrations, Razor inventory pages, request statuses.

## Backend Developer 3 - Jedidiah Nii Saban Delali Annan

- Mission: implement BloodNeed, BloodRequest, dashboard, and notification state machines.
- Owned folders: need/request/notification/dashboard contracts, services, domain behavior, tests.
- Owned documents: request and notification contract notes.
- Required inputs: inventory service operations, Security facility scoping.
- Deliverables: `IBloodNeedService`, `IBloodRequestService`, `INotificationService`, `IDashboardService`, services, tests.
- Interfaces exposed: request lifecycle, timeline, notifications, dashboards.
- Dependencies: Backend Developer 2, Database 1/2, Frontend Developer 3.
- Acceptance criteria: valid transitions only, one active external request per need, inventory calls for accept/release/fulfil, history and notifications for transitions.
- Required tests: needs, requests, notification, dashboard unit tests.
- Handoff recipient: Frontend Developer 3 and QA.
- Must not modify without coordination: inventory balance code, migrations, Razor pages.

## Database Developer 1 - Salimah Salifu

- Mission: maintain EF Core model, migrations, seed process, ERD, and data dictionary.
- Owned folders: `src/BloodLink.Infrastructure/Data`, `Configurations`, `Migrations`, `Seed`.
- Owned documents: database setup, ERD, data dictionary.
- Required inputs: approved entities and contracts.
- Deliverables: DbContext, configurations, migrations, seed data, database docs.
- Interfaces exposed: database schema and migration chain.
- Dependencies: all backend owners and Security.
- Acceptance criteria: constraints, row versions, FKs, status storage, delete restrictions match blueprint.
- Required tests: migration and schema verification.
- Handoff recipient: Database Developer 2 and QA.
- Must not modify without coordination: business transitions or authorization policy.

## Database Developer 2 - Musharafa Moro

- Mission: verify integrity, concurrency, query performance, and database tests.
- Owned folders: database integration tests and performance docs.
- Owned documents: indexes, query review, backup/restore.
- Required inputs: migrations and service query patterns.
- Deliverables: integration tests, index review, rollback evidence.
- Interfaces exposed: query and performance guidance.
- Dependencies: Database Developer 1 and backend owners.
- Acceptance criteria: no silent stock overwrite, rollback on failed accept/cancel/fulfil, bounded lists, no N+1 regressions.
- Required tests: integration and concurrency tests.
- Handoff recipient: QA and backend owners.
- Must not modify without coordination: migration chain or feature semantics.

## Authentication & Security Developer - Isaac Morrison Quaye

- Mission: own Identity, role policies, current-user scoping, and security tests.
- Owned folders: `src/BloodLink.Infrastructure/Identity`, `src/BloodLink.Application/Security`, `src/BloodLink.Web/Authorization`, account components.
- Owned documents: security checklist and authorization matrix.
- Required inputs: role matrix and facility lifecycle rules.
- Deliverables: ApplicationUser, Identity setup, policies, sign-in/out, current-user service, security tests.
- Interfaces exposed: authorization policies and current user abstraction.
- Dependencies: Backend Developer 1, Database 1, frontend owners.
- Acceptance criteria: no self-assigned privileged role, inactive and blocked facilities cannot operate, no cross-facility ID tampering.
- Required tests: authorization and security tests.
- Handoff recipient: all feature teams and QA.
- Must not modify without coordination: facility lifecycle, inventory, request services, migrations.

## DevOps + QA/Test Engineer - Jennifer Banibensu

- Mission: make merges reproducible and prove the facility-only scope works securely.
- Owned folders: CI, acceptance tests, smoke tests, QA docs.
- Owned documents: test plan, traceability matrix, deployment guide, runbook, release notes.
- Required inputs: all feature PRs and setup docs.
- Deliverables: CI, smoke suite, acceptance evidence, release checklist.
- Interfaces exposed: verification gates and defect reports.
- Dependencies: all teams.
- Acceptance criteria: CI restores/builds/tests/fails correctly, clean deployment, no unexplained donor artifact.
- Required tests: acceptance and smoke tests.
- Handoff recipient: PM.
- Must not modify without coordination: feature code ownership areas except through reviewed fixes.
