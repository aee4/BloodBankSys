# Team Ownership

| Area | Primary owner | Contributors | Required input | Output/handoff | Approval required |
| ---- | ------------- | ------------ | -------------- | -------------- | ----------------- |
| Scope, workflow, statuses, route names | Project Manager / Team Lead | All owners | Blueprint and PR feedback | Approved project contracts | PM |
| `docs` root documents | Project Manager / Team Lead | All owners | Feature evidence | Maintained source of truth | PM |
| `docs/design` | UI/UX Designer | Frontend developers | Canonical routes and states | User flows and screen specs | UI/UX + PM |
| `src/BloodLink.Domain/Enums` | Project Manager / Team Lead | Backend owners | Blueprint status tables | Canonical enums | PM + affected backend |
| Facility entity and contracts | Backend Developer 1 | Database 1, Security | Facility workflow | Facility service contracts | PM + DB + Security |
| Staff contracts and services | Backend Developer 1 | Security, Frontend 1 | Identity abstractions | Staff service contracts | Security |
| Inventory entities and contracts | Backend Developer 2 | Database 1/2, Backend 3 | Stock rules | Inventory/search/transfer contracts | DB + Backend 3 |
| Need/request entities and contracts | Backend Developer 3 | Backend 2, Database 1/2 | Status machines | Need/request service contracts | PM + Backend 2 |
| Notifications and dashboards | Backend Developer 3 | Frontend 3, QA | Request and inventory events | Notification/dashboard contracts | PM |
| `src/BloodLink.Infrastructure/Data` | Database Developer 1 | Backend owners, Security | Approved entities | DbContext, configurations, migrations, seed | DB1 |
| Database integrity and query performance | Database Developer 2 | Database 1, backend owners | Migrations and queries | Integration tests and index guidance | DB2 + DB1 |
| `src/BloodLink.Infrastructure/Identity` | Authentication & Security Developer | Database 1 | Role matrix | Identity setup and ApplicationUser | Security |
| `src/BloodLink.Web/Authorization` | Authentication & Security Developer | Frontend owners | Policy names | Authorization helpers | Security |
| Facility/staff UI | Frontend Developer 1 | UI/UX, Backend 1, Security | Facility/staff contracts | Onboarding and staff pages | UI/UX + Backend 1 |
| Inventory/search UI | Frontend Developer 2 | UI/UX, Backend 2 | Inventory contracts | Inventory pages | UI/UX + Backend 2 |
| Needs/requests/dashboard/notifications UI | Frontend Developer 3 | UI/UX, Backend 3, Backend 2 | Need/request contracts | Workflow pages | UI/UX + Backend 3 |
| Tests and CI | DevOps + QA/Test Engineer | All teams | Feature contracts and acceptance criteria | CI, smoke, acceptance evidence | QA + PM |
| `scripts` setup guides | DevOps + QA/Test Engineer | Database 1, Security | Setup requirements | Reproducible setup docs | QA |
