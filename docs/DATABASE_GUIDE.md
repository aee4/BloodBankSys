# Database Guide

## Ownership

Database Developer 1 owns `BloodLinkDbContext`, EF configurations, migrations, seed data, relationships, constraints, delete behavior, and database setup documentation.

Database Developer 2 owns query review, concurrency tests, indexes, performance validation, and backup or reset guidance.

## Migration Rules

- Do not create migrations until the shared entity model is approved.
- Only Database Developer 1 commits migration files.
- Feature owners request schema changes through reviewed pull requests.
- Migrations must not include donor tables, donor columns, donor seed data, or patient-identifying information.

## Entity Rules

- Facilities, users, needs, requests, transactions, histories, and audit logs are not hard-deleted through application workflows.
- `BloodInventory` must be unique by FacilityId and BloodType.
- `AvailableUnits` is computed as TotalUnits minus ReservedUnits.
- Inventory transaction rows are immutable.
- RowVersion fields protect inventory, needs, and requests from stale updates.

## Seed Data Rules

- Seed roles: SystemAdmin, FacilityAdmin, FacilityStaff.
- Seed all eight blood types for approved demo facilities only when demo data is explicitly requested.
- Use safe fake demo data only.
- Do not seed credentials, tokens, or real people.
