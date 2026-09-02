# BloodLink MVP Release Notes

**Release Version:** 1.0.0-MVP  
**Release Date:** 16 August 2026  
**Author:** Jennifer Banibensu, DevOps & QA/Test Engineer (Student ID: 22013023)  
**Project:** BloodLink — Hospital & Blood Bank Inventory Coordination System  
**Audience:** Development Team, Project Stakeholders, and Pilot Facility Administrators  

---

## 1. Executive Summary

BloodLink 1.0.0-MVP is the initial release of the **Hospital & Blood Bank Inventory Coordination System**. The platform enables participating healthcare facilities and blood banks to manage real-time inventory balances and coordinate urgent inter-facility blood transfers safely and securely.

---

## 2. What's Included in This Release

### 2.1 Facility Onboarding & Access Control
* **Public Facility Registration:** Unregistered hospitals and blood banks can apply online; submissions enter `Pending` status.
* **System Administration Oversight:** `SystemAdmin` can review, approve, reject with reason, or suspend facilities.
* **Facility-Scoped Staff Management:** `FacilityAdmin` can provision and deactivate staff members for their own facility.
* **Multi-Layer Security:** Role policies (`SystemAdmin`, `FacilityAdmin`, `FacilityStaff`) and strict `FacilityId` scoping prevent cross-facility data leakage.

### 2.2 Blood Inventory Management
* **Exact Blood-Type Tracking:** Separate real-time balances for all eight canonical blood types (`A+`, `A-`, `B+`, `B-`, `AB+`, `AB-`, `O+`, `O-`).
* **Available Units Calculation:** `AvailableUnits = TotalUnits - ReservedUnits` computed dynamically.
* **Immutable Audit Trail:** All stock movements (StockIn, Consumption, Manual Corrections) generate permanent `InventoryTransaction` records.

### 2.3 Internal Needs & Escalation
* **Staff Need Creation:** `FacilityStaff` can raise internal blood needs (`PendingReview`) when local stock is depleted.
* **Administrative Decision:** `FacilityAdmin` can fulfil internally, reject with a reason, cancel, or escalate to external search.

### 2.4 Inter-Facility Request Coordination & Fulfilment
* **Exact-Type Availability Search:** Facility admins can query active partner facilities with available stock (excluding self, excluding pending/suspended).
* **External Request Workflow:** Direct facility-to-facility requests (`Sent` -> `Accepted` -> `Fulfilled`).
* **Atomic Reservations:** Accepting an incoming request protects the requested units from double-allocation.
* **Atomic Transfer Handover:** Handover confirmation decreases source inventory, increases destination inventory, and creates linked transfer transactions in a single transaction boundary.
* **Safe Cancellation:** Cancellation of accepted requests immediately releases reserved units back to available stock.

### 2.5 In-App Notifications & Dashboards
* **Targeted Alerts:** Notifications for new needs, incoming requests, status updates, and low stock.
* **Role-Aware Dashboards:** Customized summary metrics for SystemAdmins, FacilityAdmins, and Staff.

---

## 3. Strict Scope Boundaries (Excluded Features)

* **No Donors:** The system contains zero donor tables, registration flows, eligibility tests, or donor reminders.
* **No Medical / Clinical Decision Support:** The system coordinates inventory logistics only and does not replace laboratory cross-matching, clinical approval, or transport protocols.
* **No Public Inventory Viewing:** All stock numbers require authenticated and authorized facility credentials.

---

## 4. Quality & Verification Highlights

* **Automated Test Suite:** 85+ unit, integration, and acceptance tests passing with 100% green status.
* **End-to-End Acceptance Journey:** Proves full lifecycle from need creation to search, request, acceptance, and fulfilment.
* **Continuous Integration:** Automated GitHub Actions pipeline verifying restore, build, test, and code formatting on every merge.
