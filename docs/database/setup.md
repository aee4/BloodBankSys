# Database Deployment

## Preflight

1. Back up the target database and confirm the connection string points to the intended environment.
2. Run `scripts/integrity_queries.sql`; every violation query must return zero rows.
3. Review duplicate facility names, registration numbers, and staff user mappings.
4. Generate and review an idempotent deployment script with `dotnet ef migrations script --idempotent --project src/BloodLink.Infrastructure --startup-project src/BloodLink.Web`.

## Deployment

Apply migrations using `scripts/database-setup.md` or a reviewed idempotent script. Migrations are not run automatically by the web host. The Phase 2 migration is transactional on SQL Server and its preflight uses `THROW` to stop on incompatible legacy data.

Role and SystemAdmin initialization is a separate, explicitly enabled startup operation. Keep bootstrap values in the deployment platform's secret store. Logs contain the configured email but never the password.

## Validation

- Query `__EFMigrationsHistory` for `20260921230224_EnforceCanonicalDatabaseIntegrity`.
- Run `scripts/integrity_queries.sql` again.
- Confirm the canonical roles exist once each.
- Confirm any bootstrap SystemAdmin is active, email-confirmed, facility-less, and has only `SystemAdmin`.

Disposable SQL Server execution may be deferred where no SQL Server instance is installed; model, migration, and initializer tests remain mandatory. Exercise a disposable SQL Server before production deployment.
