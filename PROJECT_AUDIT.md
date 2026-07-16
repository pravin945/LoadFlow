# LoadFlow Project Audit

Audit date: 2026-07-16

## Current status

| Requirement | Status | Evidence / notes |
|---|---|---|
| Three account types and JWT authentication | Complete | Broker, Carrier, and Shipper registration/login are implemented and runtime-tested with seeded accounts. |
| Permission-based custom RBAC | Complete for the demo | Fixed permission catalog, custom org roles, policy-protected endpoints, and admin-created staff/roles are present. |
| Organization and object scoping | Complete for load access | Broker loads are scoped to broker org, carrier loads to assigned carrier org, and shipper loads to the shipper user. Runtime smoke tests passed. |
| Permission-denied logging | Complete | 403 attempts are logged to the database and console; log queries are scoped to the requesting admin's organization. |
| Load state machine and audit trail | Complete for forward workflow | Assignment and rate confirmation cannot be bypassed with the generic transition endpoint; every action is attributed and timestamped. |
| Carrier accept/decline | Complete | Accept is audited; decline unassigns the carrier and returns the load to Posted. |
| Compliance flag and progression block | Complete | Assignment evaluates insurance, authority, equipment, and commodity; rate confirmation rechecks a corrected record; override requires its own permission. |
| Versioned rate confirmation | Complete | Every confirmation creates an immutable version and the load points to the version actually confirmed. |
| Search/filter and account dashboards | Complete | Scoped dashboards plus reference/route/status/compliance filters are implemented. |
| POD upload/view | Complete | Carrier-scoped PDF/image upload after delivery, authenticated download, and POD-before-verification enforcement are implemented. |
| Build and run | Complete | Backend and frontend production builds pass; API login/scoping/403 logging smoke tests pass. |

## Remaining work before calling it production-ready

These are the most valuable next tasks, in priority order:

1. Add automated backend integration tests for every permission, cross-org access attempt, state transition, compliance block, and rate version.
2. Complete literal CRUD semantics: add broker load deletion/cancellation, compliance-record deletion, and their UI controls. The current app supports load create/read/update and compliance read/upsert.
3. Expand staff lifecycle management with role reassignment, deactivate/reactivate, password reset/invitation tokens, and matching UI.
4. Persist carrier acceptance as explicit data and require it before dispatch; it is currently represented as an audit event.
5. Replace `EnsureCreated` with EF Core migrations and add database upgrade/backup guidance.
6. Move the JWT signing key out of committed settings into environment variables or a secret store before deployment; add refresh/revocation or shorter-lived access tokens.
7. Add input validation (password policy, normalized emails, date ordering, weight/rate limits) and consistent Problem Details error responses.
8. Add pagination to load and audit queries and expose full rate-version history in the UI.
9. Add a dedicated permission-denied/audit-log viewer screen; the API exists, but there is no navigation page for it yet.
10. Add deployment configuration (container or chosen host), HTTPS, persistent POD storage/backup, health checks, and production CORS origins.

## Verification performed

- `dotnet build LoadFlow.sln --no-restore` — passed with 0 warnings and 0 errors.
- `npm run build` — passed with Vite 8.1.4.
- `npm audit` — 0 known vulnerabilities after upgrading the Vite toolchain.
- Running API smoke test — broker login/load list passed; shipper object-scoped list passed; dispatcher direct call to `/api/staff` returned 403; denial was persisted and visible to the broker admin.
