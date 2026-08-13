# Testing Strategy

## Unit Tests

Each source project has a matching test project. Unit tests should verify validation, status transition rules, DTO mappings, and role checks within the owned feature area.

## Integration Tests

Database and service integration tests should cover EF configuration, unique constraints, row version concurrency, transactions, rollback behavior, and facility-scoped queries.

## Authorization Tests

Security tests must cover anonymous access, wrong role, inactive users, pending/rejected/suspended facilities, and cross-facility ID tampering.

## UI Tests

UI tests should cover role-appropriate navigation, validation messages, error states, loading states, empty states, mobile layout at 360px, and desktop layout without horizontal scrolling.

## Smoke and Acceptance Tests

QA owns smoke and acceptance flows for onboarding, staff, inventory, internal needs, availability search, request response, reservation, cancellation, fulfilment, notifications, dashboards, and audit evidence.

## Required Local Verification

```bash
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes
git diff --check
```
