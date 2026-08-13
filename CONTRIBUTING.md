# Contributing

## Git Workflow

1. Pull the latest approved development branch before starting.
2. Create one branch per task.
3. Use branch formats such as `feature/authentication`, `feature/facility-management`, `feature/inventory`, `feature/blood-requests`, `feature/notifications`, `feature/reporting`, `test/authorization`, and `docs/workflows`.
4. Do not commit directly to the protected branch.
5. Keep pull requests limited to one concern.
6. Run formatting, build, and tests before requesting review.
7. Do not edit another role's owned files without coordination.
8. Shared entities, enums, interfaces, and database migrations require review from affected teams.
9. Never commit passwords, connection strings with real credentials, access tokens, or real patient data.
10. Resolve merge conflicts with the relevant file owner.
11. Use conventional commit messages.

## Pull Request Checklist

- Requirement or issue is linked.
- Scope is one concern.
- Owned folders and documents are respected.
- Shared contracts have affected-team review.
- No donor functionality or donor terminology has been introduced.
- No patient-identifying information is stored or displayed.
- No secrets or production connection strings are committed.
- `dotnet restore` passes.
- `dotnet build` passes.
- `dotnet test` passes.
- `dotnet format --verify-no-changes` passes.
- Manual verification is documented for UI work.
- The next consuming team or owner is named.
