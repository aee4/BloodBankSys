# BloodLink QA Release Acceptance Checklist

**Document Version:** 1.0.0  
**Date:** 16 August 2026  
**Author:** Jennifer Banibensu, DevOps & QA/Test Engineer (Student ID: 22013023)  
**Project:** BloodLink — Hospital & Blood Bank Inventory Coordination System  
**Handoff Recipient:** Emmanuel Eyram Korku Agbetor, Project Manager / Team Lead (Student ID: 22206812)  

---

## 1. Master Release Acceptance Criteria (Blueprint Section 13)

| # | Acceptance Criterion | Evidence / Proving Test File | Verification Status |
|---|---|---|---|
| 1 | **Facility Onboarding & Governance:** A facility can register; only SystemAdmin can approve/reject/suspend it; pending/rejected/suspended facilities cannot operate. | `OnboardingAcceptanceTests.cs` (staged skeleton with TODOs for Backend 1 delivery) | `[STAGED - WAITING BE1]` |
| 2 | **Staff Isolation:** An approved FacilityAdmin can create/deactivate own-facility staff and cannot touch another facility's users. | `StaffAcceptanceTests.cs` (staged skeleton with TODOs for Backend 1 delivery) | `[STAGED - WAITING BE1]` |
| 3 | **Staff Permissions:** FacilityStaff can view own inventory and submit/track internal needs but cannot search or create external requests. | `InternalNeedAcceptanceTests.cs`<br>`RoleContractTests.cs` | `[VERIFIED PASS]` |
| 4 | **Needs & Search:** FacilityAdmin can adjust own inventory, search exact-type availability, and create one active external request from a need. | `InternalNeedAcceptanceTests.cs`<br>`EndToEndRequestFlowAcceptanceTests.cs` | `[VERIFIED PASS]` |
| 5 | **Atomic Reservation:** Source FacilityAdmin can accept only with sufficient available units; acceptance reserves stock atomically. | `RequestResponseAcceptanceTests.cs`<br>`ReservationAcceptanceTests.cs` | `[VERIFIED PASS]` |
| 6 | **Cancellation & Fulfilment Transfer:** Cancellation releases reservations; fulfilment atomically transfers stock and completes the linked need. | `CancellationAcceptanceTests.cs`<br>`FulfilmentAcceptanceTests.cs` | `[VERIFIED PASS]` |
| 7 | **Stock Integrity & Immutability:** Inventory never becomes negative (`Total >= Reserved >= 0`), and all changes produce immutable transaction records. | `InventoryAcceptanceTests.cs` (staged skeleton with TODOs for Backend 2 delivery) | `[STAGED - WAITING BE2]` |
| 8 | **Timelines & Dashboards:** Request histories, notifications, dashboards, and audit trails are correct and facility-scoped. | `NotificationsAcceptanceTests.cs`<br>`DashboardsAcceptanceTests.cs`<br>`AuditEvidenceAcceptanceTests.cs` | `[VERIFIED PASS]` |
| 9 | **Multi-Layer Authorization:** All protected pages and service operations reject anonymous, wrong-role, inactive, and cross-facility access. | `RequestResponseAcceptanceTests.cs`<br>`SmokeTests.cs`<br>`BloodNeedServiceTests.cs` | `[VERIFIED PASS]` |
| 10 | **Reproducible Environments:** A fresh environment can restore, migrate, seed, run, format check, and test from documented commands. | CI pipeline (`.github/workflows/ci.yml`), `DEPLOYMENT_GUIDE.md` | `[VERIFIED PASS]` |
| 11 | **Zero Donor Artifacts:** No donor entity, donor page, donor service, donor table, donor role, donor test, or donor wording remains in the codebase. | `CanonicalVocabularyTests.cs`<br>Repository static scan | `[VERIFIED PASS]` |

---

## 2. Pre-Release Sign-Off Table

| Role | Name | Status | Signature / Notes |
|---|---|---|---|
| **DevOps & QA Engineer** | Jennifer Banibensu | **Completed** | Full acceptance test suite, CI workflow, and QA documentation prepared. |
| **Project Manager / Team Lead** | Emmanuel Eyram Korku Agbetor | **Pending Final Review** | Final handoff and release review. |
