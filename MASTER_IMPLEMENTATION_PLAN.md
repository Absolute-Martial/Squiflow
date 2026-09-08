# SquiFlow v0.0.15 — Master Implementation Plan

**Status:** Current architecture baseline.

## 1. Product shape

SquiFlow is a C#/.NET multi-tenant business platform with four visible runtime/product boundaries:

```text
apps/web            Tenant staff Web + tenant-owner Settings
apps/admin-web      SquiFlow platform administration
apps/desktop        Windows Workstation + Guard/helpers
services/core-api   ASP.NET Core HTTP/composition host
services/worker     Durable background execution
```

Business capability modules remain a modular monolith and are shared by the appropriate hosts. They do not become network services merely because they are separate projects.

## 2. Small-team-first tenant model

The common tenant can be as small as:

```text
Owner
└── Staff
```

Owner and Staff are starting role templates, not hard-coded permanent policy. The tenant Owner decides ordinary staff rights within the tenant's entitlement and SquiFlow's non-overridable security/domain boundaries.

The Owner can create roles, clone/edit templates, assign users to roles, optionally scope assignments to branch/program, and grant/revoke business action permissions.

The Owner cannot grant cross-tenant access, platform-operator rights, unavailable entitlements, arbitrary code execution, direct database/root access, or bypass hard financial/security invariants.

## 3. Granular authorization

Permissions are stable action identifiers such as:

```text
customers.view
customers.create
customers.edit
orders.view
orders.create
orders.edit
orders.cancel
orders.apply_manual_price
orders.approve
inventory.view
inventory.adjust
payments.record
payments.refund
quotes.create
quotes.approve
documents.print
team.manage
roles.manage
domains.manage
rules.manage
workflow.manage
```

Authorization combines:

```text
authenticated actor
+ tenant/platform scope
+ permission
+ resource ownership/scope
+ canonical resource state
+ workflow transition guard
+ risk tier / step-up when required
+ concurrency/version check
```

Do not create a permission for every possible state combination. For example `orders.edit` can be granted while the domain guard still requires the Order to be Draft.

## 4. Canonical state + configurable workflow stage

Tenant-customizable stages must not redefine protected system truth.

```text
Canonical system state
+
Configurable tenant workflow stage
```

Example:

```text
Order canonical state: Accepted
Tenant stage: WaitingForDesignApproval
```

Canonical states protect payment, stock, financial, security and synchronization invariants. Configurable stages provide tenant-specific process flexibility.

## 5. Administration surfaces

### Tenant administration

Lives naturally inside `apps/web` under privileged Settings/Administration routes:

- Team
- Roles and permissions
- Branch/program setup
- Rules/workflow
- Custom fields/forms
- Custom domains/branding
- Feature settings
- Devices/workstations
- Tenant-visible audit/reports

For a two-person business, this should feel like ordinary Settings rather than an enterprise control center.

### Platform administration

`apps/admin-web` is for SquiFlow operators:

- tenants/subscriptions/entitlements
- global configuration
- incidents/runtime health
- platform security
- provider configuration
- support operations

Both surfaces call authoritative Core API endpoints. Browser UI is never the security boundary.

## 6. ASP.NET Core API host

`services/core-api` owns HTTP mechanics and composition, similar in architectural role to a Rust/Axum executable host:

- endpoint registration
- authentication middleware
- rate limiting
- request correlation
- health/readiness
- dependency composition

Business rules and domain operations live in modules/application projects, not in HTTP endpoint files.

Suggested API segmentation:

```text
/api/...
/tenant-admin/...
/platform-admin/...
/sync/...
/client/...
```

The URL is not the authorization boundary; endpoint policies and application authorization are.

## 7. Identity and Workstation login

Use one canonical SquiFlow browser identity authority.

Native Workstation first login:

```text
Install Workstation
→ launch
→ open system browser
→ SquiFlow identity login/MFA
→ select permitted tenant/workstation context
→ optional device enrollment/approval
→ one-time authorization callback
→ Workstation exchanges code using PKCE
→ local session/device credential established
→ bootstrap authorized configuration/rules/data
```

The Workstation does not collect the user's password as its primary login method and never receives database credentials.

## 8. Custom domains

A tenant Owner with `domains.manage` can configure verified domains for staff Web or client portal.

Lifecycle:

```text
Draft
→ PendingVerification
→ Verified
→ CertificateProvisioning
→ Active
```

Failure/maintenance states include VerificationFailed, CertificateFailed, Misconfigured, Suspended, Removing and Removed.

SquiFlow verifies ownership, provisions/renews TLS, stores authoritative domain mapping centrally, audits changes, and preserves a safe SquiFlow fallback domain unless a deliberately reviewed policy disables it.

Interactive authentication still redirects through the canonical SquiFlow identity origin. Do not share one broad auth cookie across arbitrary customer-owned domains.

## 9. Browser storage

Use each browser store intentionally:

- HttpOnly secure cookie/server-backed session: authentication/session where architecture permits.
- Memory: active UI/query state.
- `sessionStorage`: tab-local transient UI state only.
- `localStorage`: low-risk preferences only; never access/refresh tokens, passwords or business truth.
- IndexedDB: explicit offline drafts/bounded recent data, and only later approved offline command queues.
- Cache Storage/service worker: application shell and hashed static assets.

Any business data readable by JavaScript is exposed to successful XSS in that origin; data minimization and browser security matter more than pretending client-side encryption with a JS-readable key solves XSS.

## 10. Web offline strategy

Web is online-first and resilient, not a full Workstation clone on day one.

### Tier 0 — baseline
- precached app shell/static assets
- explicit offline/degraded screen
- reconnect handling
- no false success

### Tier 1 — recommended
- IndexedDB draft preservation for selected forms
- bounded recent read cache where stale data is safe
- visible freshness state

### Tier 2 — selective future
Explicitly approved offline business commands can queue using the same semantic idempotency/version/conflict model as Workstation, only after multi-tab ownership, quota/eviction, schema migration, long-offline recovery, security and conflict tests pass.

### Tier 3 — not baseline
Full Desktop-equivalent browser offline behavior across all modules.

The native Workstation remains the strongest local-first/offline client.

## 11. Stateless infrastructure

Web/API/Worker nodes remain disposable. Authoritative durable state lives in the selected database/object storage/job state.

Clients can be locally stateful without making server nodes stateful.

Session revocation/shared state and custom-domain routing use durable/shared authoritative records, with caches as reconstructable derivatives.

## 12. Persistence products remain open

Central and local persistence requirements are decided; exact products are not.

- PostgreSQL is the strongest current central reference candidate.
- SQLite + WAL is the mature local reference candidate.
- libSQL is an explicit local-store candidate.
- Server and Workstation do not have to use the same product.

Provider-specific reference projects do not silently close the decision.

## 13. Rules and workflow

SquiFlow owns the native bounded rule representation, validation, scope/inheritance, immutable snapshots, evaluation contract, decision trace and publication lifecycle. External evaluators can be bounded adapters, not the tenant rule model.

Workflow design is continuation-first. Every non-terminal state must answer:

1. Who acts next?
2. Where do they discover the work?
3. What action continues it?
4. What information is required?
5. What if nobody acts?
6. Deadline/escalation?
7. Can it be delegated/reassigned?
8. Can it be cancelled, or is compensation required?
9. What if two users act simultaneously?
10. What if permission/resource/rule/workflow version changes?
11. How is recovery/support explained?

## 14. Worker

Durable asynchronous work uses bounded queues/concurrency, claims/leases, idempotency, retry classification, checkpoints/progress, no-progress detection, pause/resume/drain, crash-loop protection, quarantine/DLQ and reconciliation.

External side effects can have `OutcomeUnknown`; cancellation does not imply an external effect was undone.

## 15. Synchronization

Workstation is untrusted from server authority perspective.

```text
local durable business + outbox transaction
→ bounded sync upload
→ authentication
→ authoritative tenant derivation
→ permission/business validation
→ idempotency/concurrency/conflict
→ central transaction
→ per-item result
→ durable local acknowledgement
```

Remote cursor changes are applied together with the cursor in one local transaction.

Long-offline clients must get explicit upgrade/resnapshot/export/repair paths, never silent discard.

## 16. Observability

OpenTelemetry/OTLP is the provider-neutral instrumentation boundary.

Current managed targets:
- New Relic free service: metrics/traces/APM
- Aiven OpenSearch free service: searchable structured operational logs
- Backtrace: crash-oriented diagnostics

Managed observability is intentionally relied upon. Telemetry export is still not part of business transaction correctness.

## 17. Definition of implementation-complete

A capability is not complete until it answers:

- user states and recovery
- ownership and authority
- validation and permission
- transaction boundary
- idempotency/concurrency
- async work and crash recovery
- unknown external-effect handling
- user feedback/retry/cancel
- audit/telemetry
- resource bounds
- upgrade/version skew
- backup/restore/deletion
- tests proving the above

## 18. Implementation order

Implement vertical journeys rather than many modules in parallel:

1. Architecture skeleton and error/execution context.
2. First-run → tenant/customer/order → restart recovery.
3. Local store + durable outbox.
4. Sync duplicate/response-loss/conflict path.
5. Long-offline/version recovery.
6. Native rules + one workflow + one dynamic form.
7. Worker crash/pause/lease/reconciliation path.
8. Tenant Owner permissions + platform administration/security.
9. Files/documents/printing.
10. OTel → New Relic/Aiven + diagnostics.
11. Payment correction/refund + stock concurrency.
12. Resource, update, compatibility and backup/restore qualification.
