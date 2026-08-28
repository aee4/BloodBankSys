# BloodLink Production & Operational Runbook

**Document Version:** 1.0.0  
**Date:** 16 August 2026  
**Author:** Jennifer Banibensu, DevOps & QA/Test Engineer (Student ID: 22013023)  
**Project:** BloodLink — Hospital & Blood Bank Inventory Coordination System  
**Handoff Recipient:** Emmanuel Eyram Korku Agbetor, Project Manager / Team Lead (Student ID: 22206812)  

---

## 1. Escalation Directory & Subsystem Ownership

When an incident occurs in staging or production, refer to the following escalation directory based on the affected subsystem:

| Subsystem / Area | Primary Owner | Student ID | Responsibilities & Focus | Escalation Contact |
|---|---|---|---|---|
| **Overall System / Release** | **Emmanuel Eyram Korku Agbetor** (PM) | `22206812` | Scope decisions, integration order, demo scenario, final acceptance | Tier 3 / Final Authority |
| **Facility & Staff Lifecycle** | **Poku Nancy** (Backend 1) | `22062728` | Facility registration, approval/rejection, staff management | Tier 2 Backend |
| **Inventory & Reservations** | **Jephthah Peprah** (Backend 2) | `22036173` | Stock integrity, reservation transactions, availability search | Tier 2 Backend |
| **Needs, Requests & Notifications** | **Jedidiah Annan** (Backend 3) | `22037871` | State machines, request handover, notifications, dashboards | Tier 2 Backend |
| **Database Schema & Migrations** | **Salimah Salifu** (Database 1) | `22242022` | EF Core migrations, constraints, seed execution, schema errors | Tier 2 Data |
| **Database Concurrency & Indexes** | **Musharafa Moro** (Database 2) | `22059797` | Concurrency conflicts, index optimizations, query timeouts | Tier 2 Data |
| **Authentication, Security & Roles** | **Isaac Morrison Quaye** (Security) | `22079872` | Identity policies, account lockouts, cookie security, auth scopes | Tier 2 Security |
| **UI & Client Components** | **Selorm Sem / Fauziya Adjei / Eastwood Osei** (Frontend 1-3) | Various | Razor component rendering, client validation, form errors | Tier 2 Frontend |
| **CI/CD & Operational Pipelines** | **Jennifer Banibensu** (DevOps & QA) | `22013023` | Pipeline failures, deployment automation, regression triage | Tier 1 Incident Triage |

---

## 2. Diagnostics & Incident Response

### 2.1 Viewing Application Logs
BloodLink outputs structured logs through Microsoft.Extensions.Logging:
* **Console / stdout:** Standard container / Kestrel output.
* **File / Windows Event Log:** Production log sinks configured via Serilog/NLog if attached.
* **Database Queries:** In development, EF Core commands are logged at `Information` level. In production, SQL errors and slow queries (>500ms) are logged at `Warning` and `Error` levels.

### 2.2 Diagnostic Commands
```bash
# Check if application host is running
curl -i https://localhost:7083/

# Verify local database connectivity using sqlcmd
sqlcmd -S "(localdb)\mssqllocaldb" -d "BloodLink_Development" -Q "SELECT COUNT(*) FROM Facilities; SELECT COUNT(*) FROM BloodInventory;"
```

---

## 3. Common Failure Scenarios & Resolutions

### Scenario A: Concurrency Conflict on Request Acceptance or Stock Adjustment
* **Symptoms:** User sees `"Another administrator modified this record simultaneously. Please reload."` (DbUpdateConcurrencyException).
* **Root Cause:** RowVersion token mismatch due to simultaneous updates to `BloodInventory` or `BloodRequest`.
* **Resolution:**
  1. Have the user refresh the page to reload the latest `TotalUnits`, `ReservedUnits`, and `RowVersion`.
  2. Retry the operation with current available stock.
  3. If persistent, check for automated jobs or multiple tabs acting concurrently.

### Scenario B: Unauthorized Access / Facility Tampering (`403 Forbidden` / `UnauthorizedAccessException`)
* **Symptoms:** FacilityAdmin or Staff receives an unauthorized error when attempting to view or act on a record.
* **Root Cause:** `ICurrentUserService` detected an attempt to access a need, request, or staff account belonging to a different `FacilityId`, or the user's facility is `Pending` or `Suspended`.
* **Resolution:**
  1. Inspect the `FacilityId` on the signed-in user's claims.
  2. Verify the facility's `Status` in the `Facilities` table (`Approved` required).
  3. Confirm the target resource belongs to the user's assigned facility.

### Scenario C: Database Migration Mismatch on Startup
* **Symptoms:** Application fails to boot with `SqlException: Invalid object name 'TableName'` or pending migration exceptions.
* **Root Cause:** Target database has not had the latest EF Core migrations applied.
* **Resolution:**
  1. Execute `dotnet ef database update` against the target database (see `DEPLOYMENT_GUIDE.md`).
  2. Verify all migration entries in `__EFMigrationsHistory` match `BloodLink.Infrastructure/Migrations/`.

---

## 4. Rollback & Disaster Recovery Procedures

### 4.1 Application Rollback
If a newly deployed build has fatal bugs:
1. Re-deploy the previously published artifacts from the release archive:
   ```bash
   cp -R /releases/v0.9.0/* /var/www/bloodlink/
   systemctl restart bloodlink.service
   ```
2. Verify application health check on `/`.

### 4.2 Database Rollback
If a bad migration was applied:
```bash
# Roll back to a specific previous migration by name
dotnet ef database update <PreviousMigrationName> --project src/BloodLink.Infrastructure/BloodLink.Infrastructure.csproj --startup-project src/BloodLink.Web/BloodLink.Web.csproj
```
*Note: Any rollback must be reviewed and approved by **Salimah Salifu (Database 1)**.*
