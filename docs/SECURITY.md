# Authentication and security handoff

## Scope and baseline

Completes the authorization work originally developed on `feature/auth-security`, based on `23dde54`, for review on `feature/security-completion`. The prior baseline was 172 passing tests and a passing formatting check. Existing policy hardening, fresh role checks, router authorization and regression tests have been preserved and extended. No business-service implementations, migrations or access-control matrix were changed. Merging the feature branch remains a human/team decision.

This is an MVP implementation and a code/test review, not penetration testing or production security certification.

## Identity architecture

ASP.NET Core Identity remains responsible for password verification/hashing, password changes, reset tokens, security stamps, lockout and cookie issuance. `BloodLinkSignInManager` extends `CanSignInAsync` to enforce BloodLink account eligibility while retaining Identity's sign-in checks. No account endpoint handles password hashes directly.

`AccountAccessService` is the shared source of fresh, server-side account state. Each lookup creates and disposes a DbContext through the factory, using no-tracking reads. It verifies that the account exists and is active, has recognized current role assignments, and, for facility users, is linked to an Approved facility. Pending, Rejected, Suspended, missing and unlinked facilities fail closed. SystemAdmin can have no facility.

Existing sessions additionally require the exact current security stamp and current role set. A role grant or revocation requires a fresh principal; an old cookie cannot retain revoked permissions. Missing stamps fail closed. SystemAdmin is excluded from facility-user policies and receives no facility scope through `CurrentUserService`, including if an account was accidentally assigned both platform and facility roles.

## Roles, policies and current user

Canonical role names are case-sensitive: `SystemAdmin`, `FacilityAdmin`, `FacilityStaff`. The approved `ACCESS_CONTROL_MATRIX.md` is unchanged.

| Policy | Required access |
| --- | --- |
| `RequireSystemAdmin` | Valid active session, completed password change, current SystemAdmin role |
| `RequireFacilityAdmin` | Valid active session, completed password change, current FacilityAdmin role and approved facility |
| `RequireFacilityStaff` | Valid active session, completed password change, current FacilityStaff role and approved facility |
| `RequireApprovedFacilityUser` | Either facility role with the same account/facility checks; excludes SystemAdmin |
| Default `[Authorize]` | Valid active operational session in a recognized role; facility users require an approved facility |
| `AccountSession` | Valid active session, including users who must change their password; only for necessary account actions |

`CurrentUserService` prefers circuit authentication state over a possibly stale HttpContext, falls back to HttpContext only before a server authentication provider is initialized, and fails closed if the circuit state is still pending. It never blocks waiting for that task: it consumes a result only when already completed successfully.

The existing synchronous interface has no separate operational-access property. Therefore **`IsActive` means that the current session may operate**, not just the raw database `ApplicationUser.IsActive` flag. It is false for a stale stamp/role set, blocked facility or `MustChangePassword`. Effective roles are empty and facility scope is null in these cases. Identity/account code uses `AccountAccessService` when it needs to distinguish account eligibility from operational access. Facility claims or client parameters never override the database facility ID.

`ICurrentUserService` now documents these semantics at the interface. `IsAuthenticated` and `UserId` describe the principal and may still refer to a revoked/deleted account; they never independently authorize an operation. Access properties can perform synchronous SQL I/O and separate reads are not a transaction-wide snapshot. The implementation deliberately retains fresh lookups rather than introducing request/circuit caching that could conceal revocation. An asynchronous operation-level access contract would require a coordinated backend API change; it is a future performance/clarity improvement, not part of this patch.

This immediately protects service methods that use these guards. It cannot authorize a method that never checks the current-user abstraction; see cross-team findings below.

## Login, logout and account pages

- GET `/account/login` renders the existing public login design with a standard HTTP form. POST on the same path uses `SignInManager.PasswordSignInAsync`, with `lockoutOnFailure: true`.
- Invalid credentials, unknown/deleted users, inactive accounts, unknown/absent roles and blocked/unlinked facility users share a generic failure response. Identity MFA-required results do not issue an application session; MFA enrollment/challenge UI is outside this MVP.
- Unknown accounts perform a dummy Identity password verification against an immutable, process-local random hash generated with the configured `PasswordHasherOptions`. No dummy account is persisted and the result can never grant access. `BloodLinkSignInManager.CheckPasswordSignInAsync` also performs this work for NotAllowed/LockedOut results, where Identity can skip password verification. Actual eligible password verification still belongs to Identity. This reduces an avoidable timing distinction; it is not mathematically constant-time. Database lookups, legacy hash costs, runtime/network variability and the attempt that triggers lockout can still differ. Tests observe real password-hasher calls rather than asserting fragile elapsed-time thresholds.
- Successful login updates `LastLoginAtUtc`, writes an `AccountLogin` audit entry and redirects to a validated local return URL. Absolute, protocol-relative, backslash and control-character redirects are rejected. Default destination is the security-owned `/account/manage`, because business dashboard pages do not yet exist. Mandatory password changes override any return URL.
- POST `/account/logout` requires an authenticated account session and antiforgery token. **MVP semantics: sign out all sessions for this user.** It calls `UserManager.UpdateSecurityStampAsync` before Identity sign-out; other users are unaffected. Previously issued cookies/principals fail the next authoritative check, and existing other-tab circuits fail their next revalidation (normally within one minute plus processing time). Fresh login creates valid new authentication state. GET never signs out. The current browser cookie is cleared in `finally`; a failed Identity update returns 503 instead of claiming global revocation. A database exception follows the normal error path; database availability remains necessary for server-side revocation. Stamp rotation also invalidates previously generated Identity reset links. Account pages explain the all-device semantics.
- GET `/account/access-denied` remains public. HTTP challenges and forbids use the correct existing account routes; interactive router failures force a full navigation to login, password change or access denied as appropriate.
- Account forms use full HTTP posts rather than interactive event handlers, so response cookies can be issued safely. Explicit MVC POST routes have precedence over Blazor's generated SSR POST routes.
- All account POST actions validate antiforgery tokens and restrict request bodies to 16 KiB. Independent per-client-IP fixed-window policies allow 10 posts/minute each for login (`account-login`), recovery requests (`account-recovery`), and password change/reset (`account-password`), without queued HTTP requests. Logout is exempt from rate limiting, so exhausting any of those quotas cannot prevent sign-out. Limits apply independently of whether an account exists; rejection remains HTTP 429. State is process-local: shared NAT clients still share each policy quota, and multiple IPs/instances require deployment-level controls. Per-recipient recovery abuse protection remains a hardening recommendation before real delivery is enabled. Password fields are capped at 256 characters and use Identity's configured policy (8 characters, uppercase, lowercase and digit).
- Application cookies are Secure, HttpOnly and SameSite=Lax. Use HTTPS for authenticated local development. Account responses use `Cache-Control: no-store` and `Referrer-Policy: no-referrer`.

## Mandatory password change

GET/POST `/account/change-password` uses the restricted `AccountSession` policy. The user must provide the correct current/temporary password and a different valid new password, confirmed twice. `UserManager.ChangePasswordAsync` performs the change and stamp rotation. Only after success is `MustChangePassword` cleared and `RefreshSignInAsync` called; the current browser receives a refreshed cookie and old sessions lose access. An `AccountPasswordChanged` audit entry is recorded.

HTTP middleware redirects restricted users away from other pages to password change, rejects other POSTs and Blazor circuit connections with HTTP 403, and allows only password change, logout, access denied and required static assets. Named/default operational policies and the current-user service independently deny restricted users, including calls made from an already-running component. Account recovery can be reached after signing out.

No staff/facility lifecycle rows are changed by this flow. Backend Developer 1 must coordinate any `FacilityStaff.PendingActivation` to `Active` transition (see below).

## Password recovery and reset delivery

GET/POST `/account/forgot-password` and GET/POST `/account/reset-password` use Identity password reset tokens with a one-hour lifetime. URLs encode the token using Base64Url. Reset checks token validity and password confirmation; malformed, tampered, expired, replayed and other-account tokens fail. Successful reset rotates the Identity stamp, clears `MustChangePassword`, records `AccountPasswordReset`, signs out the current browser and requires a fresh login. It never activates an account or approves a facility. A user deactivated after receiving a token remains inactive after a successful reset.

Forgot-password status, redirect and body are equivalent for unknown, ineligible and eligible accounts. The HTTP action performs input validation and a nonblocking enqueue, without account lookup or waiting for delivery. Responses contain no reset token. Reset links use the configured trusted `Account:PublicOrigin`, never the request Host header; it must be a bare HTTPS origin, with no credentials, path, query or fragment.

`PasswordRecoveryQueue` uses a bounded channel (100 pending email addresses, one consumer) and a hosted service. All valid submitted addresses follow the same enqueue path. A fresh DI scope resolves Identity, configuration and delivery for each item; eligibility is checked when processing. Reset tokens are generated only there and are never included in queued items. The worker catches failures without logging exception details and continues processing later requests. A blocked-provider test proves HTTP responses complete before delivery is released.

This is best-effort in-memory recovery, not a durable mail system: queue saturation rejects new work with a fixed operator warning while preserving the generic public response; shutdown/restart can lose pending work, and a stalled provider can delay subsequent requests. Each item has a cooperative 30-second cancellation budget. Shutdown stops accepting new items and cancels in-flight work; a cancellation-aware blocked-provider test verifies this. Providers must honor cancellation and bound their network timeouts: the worker cannot forcibly stop an uncooperative external provider without risking abandoned scoped work. No retry/delivery guarantee is presented to the user. Durable delivery, distributed abuse controls and monitoring belong to cross-team/deployment integration. Removing provider/account work from the response path reduces timing leakage; it does not establish constant-time network behavior or eliminate aggregate load side channels.

Delivery is behind `BloodLink.Application.Security.IPasswordResetDelivery`. The default `DisabledPasswordResetDelivery` sends nothing, stores nothing and logs no tokens. The page explicitly states that delivery is unconfigured. Tests replace it with an in-memory capture confined to the test host. Delivery failures do not change the already-issued generic response and log only a fixed warning, with no provider exception, email, password or token.

**Production email integration still required:** register an actual delivery implementation after infrastructure registration, set `IsConfigured` accurately, and set `Account__PublicOrigin=https://your-approved-host`. Use a trusted provider and externally stored credentials. Configure provider timeouts, monitor queue/processing failures, and avoid logging message bodies or reset URLs. No SMTP credentials, real messages, public token viewer or token log has been added. Initial temporary credentials generated by the facility/staff owner still have no delivery mechanism; eligible users can use reset once delivery is configured.

The app does not log passwords or reset tokens. Operators must also disable/redact account request bodies and reset query strings in reverse-proxy, tracing and access logs. Do not enable verbose request logging on production account routes. Reset links are credentials until used or expired.

## Cookies and Interactive Server revalidation

`BloodLinkCookieValidation` validates fresh account eligibility, role assignments and security stamp on every authenticated HTTP request, then retains Identity's standard security-stamp validation/renewal callback. Invalid sessions are rejected and their application cookie cleared.

`IdentityRevalidatingAuthenticationStateProvider` derives from the supported `RevalidatingServerAuthenticationStateProvider`. The framework's revalidation loop checks the same fresh account state in a new DI scope every minute. It invalidates circuit authentication after logout, password change/reset, stamp update, account deactivation/deletion, role change or facility block. No custom timer/polling loop has been added.

| Change | Enforcement |
| --- | --- |
| Account/stamp/roles/facility change | Next HTTP request, next policy evaluation and next guarded current-user lookup |
| Mandatory password change | Next HTTP request, operational policy evaluation and guarded current-user lookup |
| Idle connected circuit after an invalidating change | Framework revalidation, at most the normal one-minute interval plus processing time |
| Password change in current browser | Refreshed cookie and full navigation; old stamps denied |
| Password reset | Old stamps denied; fresh login required |
| Successful logout | All existing sessions for that user have old stamps; next HTTP/guard check denies, connected circuits fail next revalidation |

Already-rendered data cannot be recalled. These checks do not cancel an operation already past its guard. Logout revokes all sessions for the account; it is not limited to a single browser/device. Browser-driven multi-tab and SignalR reconnection automation is still absent. Integration tests cover actual issued-cookie replay, another logged-in client, and the running framework revalidation loop with a shortened test interval.

## Route coverage and frontend handoff

The current tree contains public `/`, `/facility/register`, login, forgot/reset-password, access-denied, error and not-found pages. The only implemented protected pages are `/account/manage` (default operational policy) and `/account/change-password` (`AccountSession`). The router uses `AuthorizeRouteView`; test-only HTTP endpoints exercise all four canonical policies without introducing business pages.

Business routes listed in the blueprint are not implemented yet. Their owners should apply:

| Planned pages | Policy / required handoff |
| --- | --- |
| `/system/facilities`, `/system/facilities/{id}`, `/system/audit`, `/system/dashboard` | `RequireSystemAdmin` |
| `/facility/profile` | `RequireApprovedFacilityUser` for shared viewing; FacilityAdmin guard for edits |
| `/facility/staff`, `/facility/staff/create` | `RequireFacilityAdmin` |
| `/inventory`, `/inventory/history` | `RequireApprovedFacilityUser`; service still enforces own-facility scope |
| `/inventory/adjust`, `/inventory/search` | `RequireFacilityAdmin` |
| `/needs/new`, `/needs/mine` | `RequireFacilityStaff` |
| `/needs`, `/requests/sent`, `/requests/received`, `/requests/{id}` | `RequireFacilityAdmin` plus record relationship checks |
| `/needs/{id}` | `RequireApprovedFacilityUser`, with own-submission/own-facility checks in service |
| `/dashboard` | `RequireApprovedFacilityUser` and role-specific existing service |
| `/notifications` | Default operational policy and own-recipient service checks |

These are applications of existing contracts, not new business permissions. Do not rely on hidden navigation or `AuthorizeView` for event-handler security. Do not use `AccountSession` on operational pages. There were no other implemented protected business pages to annotate, and none were redesigned.

## Cross-team findings (not modified)

| Owner | Concrete finding | Required follow-up and exposure |
| --- | --- | --- |
| Backend Developer 2, coordinated with Backend Developer 3 | `InventoryService.ReserveForRequestAsync`, `ReleaseReservationAsync`, `FulfilTransferAsync` have no authentication, role or actor-to-request facility checks | Confirmed missing checks at the public service boundary. Current request-workflow callers perform checks, so a remotely exploitable bypass was not demonstrated. Add checks consistent with approved accept/cancel/fulfil authority, including active approved participants, or isolate internal inventory helpers behind an explicitly guarded workflow boundary. Do not expose these methods directly from UI/API. |
| Backend Developer 2 | `GetOwnInventoryAsync`, `GetTransactionHistoryAsync`, `GetLowStockAlertsAsync` do not explicitly require a facility role or query approved facility status | Fresh `CurrentUserService.IsActive`/scope now blocks invalid production sessions, but the methods remain dependent on those semantics. Add explicit service-boundary role/facility checks and tests to match the declared contract; this is defense in depth after the current abstraction changes. |
| Backend Developer 1 | Facility/staff creation manually constructs Identity users, marks emails confirmed, hashes an undisclosed generated temporary password and does not enable per-user Identity lockout | Coordinate UserManager-based provisioning/activation and secure initial credential delivery. The new endpoint rate limit applies, but Identity account lockout only applies to records with `LockoutEnabled=true`. Do not assume configuring Identity's new-user options changes these existing directly-created records. |
| Backend Developer 1 | `FacilityStaff.Status` can remain `PendingActivation` after Identity password change/reset | Coordinate lifecycle synchronization with the account flow. Security uses authoritative `ApplicationUser.IsActive`, current roles, password-change flag and facility status; this task does not mutate staff workflow state or invent a new status transition. |
| Frontend owners 1–3 | Blueprint operational pages are absent | Apply the policy table above and keep operation-level guards. Account login defaults to `/account/manage` until the actual destinations exist. |
| Database / QA | Authorization tests use EF InMemory; real SQL relationships and concurrency are not covered here | Verify deployed Identity schema, migrations, role seeding, constraints and least-privilege SQL access. No migrations were created. |

`ProvisioningSecurityTests` now calls the unchanged production `StaffService.CreateStaffAsync` with an authoritative FacilityAdmin current-user service. The resulting staff account completes the real HTTP recovery/reset/login flow with test-only delivery and receives staff access while admin access is denied. This confirms compatibility after recovery, not delivery of the undisclosed initial temporary password. The same test confirms `LockoutEnabled=false` and `PendingActivation` persisting after reset: those Backend 1 gaps remain unresolved. EF InMemory still does not prove relational SQL behavior.

Cross-team findings are handed off through this document and the pull request.

## Test and package evidence

`AuthorizationPolicyTests` and `CurrentUserServiceTests` preserve and extend the original policy/circuit cases with stamp-aware principals. `AccountFlowTests`, `SessionSecurityTests` and `SecurityTestApplication` add real UserManager/SignInManager, cookie, antiforgery, HTTP policy and token tests using WebApplicationFactory and an isolated in-memory database. Tests cover all three roles, wrong roles, missing/deleted/inactive accounts, blocked/missing facilities, restoration, role revocation, mandatory changes, invalid password confirmation, cookie refresh/invalidation, the actual framework revalidation loop, cross-facility claims, redirect attacks, reset expiry/replay/tampering, disabled delivery, untrusted link origins, generic recovery responses, lockout, throttling and account-page headers.

The follow-up adds logout tests for copied cookies, another active client, the running circuit revalidation loop, fresh re-login, another unaffected user, and failed stamp updates. `AccountProtectionTests` checks real Identity verification work on generic failed login paths, independent limiter exhaustion with logout still available, blocked/failed delivery response equivalence, worker recovery, shutdown cancellation, and bounded-queue saturation. Existing reset tests now await asynchronous test delivery. `ProvisioningSecurityTests` adds the production-staff-service boundary described above. These tests improve integration evidence without claiming a browser penetration test or relational-database coverage.

The vulnerable web-test dependency path was `Microsoft.AspNetCore.Mvc.Testing 8.0.0 -> Microsoft.Extensions.DependencyModel 8.0.0 -> System.Text.Json 8.0.0` (also through hosting dependencies). Updating only the direct test-host package to `8.0.19` brings `DependencyModel 8.0.2`, whose net8.0 assets use the framework JSON implementation instead of requiring the affected 8.0.0 package. Older SQL-client transitive metadata still resolves `System.Text.Json 4.7.2`; the net8 runtime supplies its framework implementation. No production package was upgraded. EF InMemory `8.0.19` was added only to the web test project to support the new Identity integration tests.

The solution vulnerable-package audit now reports no known vulnerable packages from the configured NuGet source. This resolves the reported [CVE-2024-30105](https://github.com/advisories/GHSA-hh2w-p6rv-4g7w) and [CVE-2024-43485](https://github.com/advisories/GHSA-8g4q-xg66-9fp4) dependency findings; it is not a guarantee against unknown vulnerabilities or unpatched deployed runtimes. The pre-existing duplicate EF InMemory package warning in the infrastructure test project is unchanged.

## Deployment requirements and limits

- Complete and verify reset delivery, the trusted public origin, initial account provisioning and canonical role seeding. No production administrator or password is seeded by this task.
- Supply a production SQL connection through secrets/environment configuration, apply database-owner migrations and restrict database privileges. The checked-in development connection and `TrustServerCertificate=True` are not production settings; use verified SQL TLS certificates.
- Terminate HTTPS correctly; configure only trusted proxies/forwarded headers, host restrictions and external rate limits. The built-in per-IP limiter is process-local and does not coordinate a server farm; shared proxies/NAT can share its quota.
- Persist and protect ASP.NET Core Data Protection keys, share them only among intended instances, and plan rotation. Reset tokens and cookies depend on these keys.
- Use a supported patched .NET/ASP.NET Core runtime; NuGet package audits do not inventory the production host, OS or infrastructure.
- Keep production exception handling and HSTS, redact sensitive request telemetry, monitor account audit events and generic delivery warnings, and manage provider secrets outside source control.
- Identity confirmation remains at the existing MVP setting (`RequireConfirmedAccount=false`). Real email verification and MFA workflows are not implemented; users configured to require MFA fail closed at login.
- Security-stamp revalidation does not replace service ownership checks, nor does this patch repair unguarded inventory mutations or staff lifecycle integration.

## Final verification evidence (2026-09-12)

- `dotnet restore`: passed.
- `dotnet build BloodLink.sln`: passed, zero errors; two occurrences of the existing NU1504 duplicate EF InMemory warning.
- `dotnet test BloodLink.sln`: 246 passed, zero failed/skipped (Domain 2, Application 1, Infrastructure 134, Web 89, Acceptance 20). This is 15 additional cases over the 231-test pre-hardening baseline.
- `dotnet format --verify-no-changes`: passed.
- Local application startup/DI (including the hosted recovery worker) and HTTP smoke check: login, forgot-password, access-denied and reset-password returned 200. Reset with a placeholder code rendered the token-bearing form and antiforgery field; this was route/rendering evidence, not a successful reset. Anonymous `/account/manage` and `/account/change-password` returned 302 to login. GET logout returned 405; anonymous POST logout redirected to login. Shutdown completed cleanly with exit 0.
- The previously recorded live invalid-account login probe against the unchanged SQL configuration returned 500 because SQL Server reported TCP provider error 26, "Error Locating Server/Instance Specified." Live SQL authentication was not retried in this follow-up; SQL-backed account flows remain environment-unverified. The Identity integration tests above used an isolated in-memory database.
- The loopback HTTP smoke run had no HTTPS port configured and reported the corresponding redirect warning. Antiforgery also reported normalizing account cache headers to `no-cache, no-store`. No warning was suppressed. This was an HTTP smoke check, not browser automation or production TLS testing.
- Follow-up review verified stamp revocation/failure behavior, equivalent login verification work, bounded recovery processing, cancellation, and separate rate-limit policies. Two new test requests initially omitted the reset-page code parameter; those test requests were corrected before the passing targeted/full runs. Earlier POST-route and 403/429 regression cases remain passing.
- `git diff --check`: passed. Vulnerable-package audit: no known vulnerable packages reported. No business-service implementations, migration, production configuration or package references were changed by the hardening follow-up.

Framework references: [Blazor security](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-8.0), [ASP.NET Core 8 revalidation template](https://github.com/dotnet/aspnetcore/blob/v8.0.19/src/ProjectTemplates/Web.ProjectTemplates/content/BlazorWeb-CSharp/BlazorWeb-CSharp/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs), [antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-8.0).
