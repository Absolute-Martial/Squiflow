# SquiFlow Current Implementation Truth

**Product version:** `v0.1.0`, locked until the complete production-capable product gate

**Repository name:** internal development codename; public runtime identity is configuration-owned

**Current state:** first backend/API vertical slice implemented

## Current inventory

The three count lines below are machine-read by `Application.Architecture.Tests` (`ReadCount` matches `^label: N$`). Keep each on its own line in exactly this form.

```text
production projects: 16
test projects:       15
executable hosts:    4
solution files:      1
repository build/test contract: present
BLOCKED: none
```

## What exists

| Area | Projects | Current responsibility (declared narrow scope) |
|---|---|---|
| ApplicationProfiles | `Application.Profiles` | Bounded feature definition, dependency-graph validation and deterministic, dependency-closed effective-selection compilation with stable catalog/selection fingerprints. No production feature catalog and no durable tenant profile authority. |
| Branding | `Application.Branding` | Validated, deployment-supplied public identity (name, legal identity, theme, links). No codename fallback. |
| IdentityAccess | `Application.IdentityAccess`, `.Postgres` | Durable binding of one or more exact OIDC `(issuer, subject)` identities to a stable application account. Stores no password, role or permission authority. |
| Tenancy | `Application.Tenancy`, `.Postgres` | Tenant registry, current account membership query, and the membership-derived immutable `TenantContext`. Owns no role or permission model. |
| PlatformAdministration | `Application.PlatformAdministration`, `.Postgres` | One-time initial Platform Admin bootstrap authority: exact external administrator identity, registered Admin-device certificate fingerprint, resumable pending/completed authorization state and authoritative bootstrap audit. Ongoing Admin API/device/role lifecycle remains absent. |
| Customers | `Application.Customers`, `.Postgres` | Immutable tenant-owned customer organizations and child programs: create, read and bounded browse; separate individual billing records: create/read and revision-checked active/inactive changes. Commands retain caller-scoped semantic idempotency. |
| Orders | `Application.Orders`, `.Postgres` | Priced order drafts: create, read, bounded browse, full revision, one-way abandonment and direct commitment of immutable priced facts, with optional customer/program attribution and caller-scoped semantic idempotency. |
| CoreApi | `Application.CoreApi` | ASP.NET Core host: HTTP contract, JWT validation, Finbuckle route-candidate capture, ASP.NET resource authorization with pinned-model OpenFGA checks, Autofac root composition, the shared bounded Npgsql data source, safe unhandled-failure responses, process-local protected-request and verified-tenant concurrency admission, health endpoints, and bounded dormant tenant-keyed profile-runtime mechanics. |
| AdminBootstrap | `Application.AdminBootstrap` | One-shot private infrastructure executable. It prepares/reuses the durable bootstrap intent, writes the initial administrator relation to the explicitly pinned platform OpenFGA model with duplicate-safe semantics, confirms access at higher consistency, then marks local bootstrap complete. It exposes no HTTP bootstrap endpoint. |
| DatabaseMigrator | `Application.DatabaseMigrator` | One-shot ordered migration of IdentityAccess, Tenancy, PlatformAdministration, Customers and Orders under an advisory lock. |

Each PostgreSQL adapter owns its context factory, migration files, runtime DI registration and persistence code. The migrator owns only the advisory lock, command/exit behavior and ordering. Capability projects stay host- and provider-neutral.

## Implemented HTTP surface (CoreApi)

All protected responses are `no-store`, including authentication failures and handled errors. `GET /openapi/v1.json` describes the executable contract using the configured public brand; its OpenID Connect scheme is attached only to operations that require authorization.

Unhandled transient PostgreSQL failures return safe `503` / `database_unavailable`; non-transient provider and other unexpected faults return safe `500` / `internal_error`. A process-local, queue-free protected-request concurrency cap returns `503` / `api_capacity_exceeded` before authentication/authorization/handler work when full. The checked-in cap is 32, deployment-configurable from 1–256; it is not a throughput target, tenant quota or cross-replica fairness guarantee. After current membership, a separate queue-free per-tenant cap (default 8, configurable 1–256) returns no-store `429` / `tenant_capacity_exceeded` before OpenFGA/business work; bounded partitions retain only active leases. Native provider logs are disabled to avoid SQL/exception disclosure. Focused owners: `docs/implementation/CORE_API_FAILURE_CONTRACT.md` and `docs/implementation/CORE_API_ADMISSION_CONTRACT.md`.

| Route | Authority |
|---|---|
| `GET /health/live` | None. Dependency-free liveness. |
| `GET /health/ready` | None. Status-only, five-second single-flight cached check of primary PostgreSQL connectivity and read access to the configured OpenFGA model. Does not replace migration verification or an authorized business smoke journey. |
| `GET /openapi/v1.json` | None. Classified contract description. |
| `GET /api/v1/application/bootstrap` | None. Bounded public white-label identity with its own bounded cache/ETag policy. |
| `GET /api/v1/account` | JWT (configured HTTPS issuer, exact audience, signature, lifetime) and an active bound account. |
| `GET /api/v1/account/tenants` | JWT; returns only the account's current active memberships. |
| `GET /api/v1/tenants/{tenantId}/workspace` | Current membership plus persisted `workspace_viewer`. |
| `POST`, `GET /api/v1/tenants/{tenantId}/customers/organizations`; `GET .../organizations/{organizationId}` | Current membership plus distinct customer create/view permissions. Create is idempotent. |
| `POST`, `GET .../organizations/{organizationId}/programs`; `GET .../programs/{programId}` | As above. Create is idempotent. |
| `POST`, `GET /api/v1/tenants/{tenantId}/orders`; `GET .../orders/{orderId}` | Current membership plus `order_creator` plus `manual_pricer` (create) or `order_viewer` (detail and browse). Create is idempotent. |
| `POST /api/v1/tenants/{tenantId}/orders/price-preview` | Current membership plus `order_creator` and `manual_pricer`; bounded non-persisting calculation of supplied draft prices. No Idempotency-Key required. |
| `GET /api/v1/tenants/{tenantId}/orders/{orderId}/history` | Current membership plus `order_viewer`; bounded retained snapshots and actors from committed draft receipts. |
| `GET /api/v1/tenants/{tenantId}/orders/{orderId}/actions` | Current membership plus `order_viewer`; explains observed lifecycle and independently checked edit-plus-pricing/abandon/commit permissions without granting command authority. |
| `PUT /api/v1/tenants/{tenantId}/orders/{orderId}/draft` | Current membership plus `order_editor` and `manual_pricer`; requires `expectedRevision`; idempotent. |
| `POST /api/v1/tenants/{tenantId}/orders/{orderId}/abandon` | Current membership plus `order_abandoner`; requires `expectedRevision`; idempotent. |

| `POST /api/v1/tenants/{tenantId}/orders/{orderId}/commit` | Current membership plus `order_committer`; expectedRevision and caller-scoped idempotency; freezes priced facts without billing or fulfillment effects. |
| `POST /api/v1/tenants/{tenantId}/customers/individuals`; `GET .../individuals/{individualId}` | Current membership plus independent `individual_creator` or `individual_viewer`; no login binding or debtor assignment. |
| `POST .../individuals/{individualId}/availability` | Current membership plus `individual_availability_editor`; expectedRevision and idempotency; contact-free transition response. |

Every tenant route captures the route tenant only as an untrusted candidate (Finbuckle), establishes the current account, derives `TenantContext` from current membership, and only then checks OpenFGA. OpenFGA receives verified membership only as a contextual tuple, uses opaque GUID-based tuple identifiers, checks with `HIGHER_CONSISTENCY`, and fails closed with a bounded safe `503` when the provider is unavailable.

## Behavior that is guaranteed

- **Tenant isolation:** explicit tenant SQL predicates plus forced PostgreSQL RLS with transaction-local tenant context. Orders and Customers data is tenant-owned.
- **Idempotency:** Customer organization/program creates and Order create/revise/abandon use caller-scoped semantic idempotency keys with durable receipts.
- **Order draft contract:** browse is bounded newest-first keyset pagination with a versioned tenant-bound cursor and no count or text-search claim. Revision replaces the entire priced draft only while it is a draft and the expected revision matches, increments revision and preserves earlier receipts. Abandonment is a one-way `draft` to `abandoned` transition that retains priced content and the original create receipt, records the abandoning account and authoritative timestamp, and increments revision. Detail and browse expose current lifecycle state. Abandonment does not delete the draft or reverse financial, inventory or fulfillment effects.
- **Detail consistency:** draft detail reads its header and lines from one `RepeatableRead` snapshot, so a concurrent revision is never observed as a mixed header and lines (regression guard: `DraftDetailReadsHeaderAndLinesFromOneSnapshotDuringConcurrentRevision`).
- **Attribution, not billing:** the organization/program association on a draft is an attribution contract, not legal debtor, account billing, credit, invoice or settlement authority.
- **Order-entry price preview:** a protected, bounded preview returns normalized supplied lines and totals through the same calculator as creation, without saving or reading an order. It does not select/approve prices or introduce customer policy, tax, stock or financial effects. Its focused owner is `docs/implementation/PRICING_COMPONENT_BOUNDARY.md`.
- **Retained draft history:** current viewers can browse the priced snapshots, actors and recorded times of successful draft commands. The read reuses immutable durable receipts, uses one database statement snapshot and performs no mutation. Its focused owner is `docs/implementation/ORDER_DRAFT_HISTORY.md`; general audit and override reasons remain absent.
- **Draft action guidance:** a current viewer can discover whether revision/abandonment is available and why, using the existing lifecycle and distinct current write permissions. The response is an observation; commands recheck authority and expected revision. Its focused owner is `docs/implementation/ORDER_DRAFT_ACTION_GUIDANCE.md`; broader customer-specific workflow remains absent.
- **Verification:** `Application.Architecture.Tests` checks the dependency graph, provider isolation, solution membership and the inventory counts above.
- **Database grants:** grants for the restricted CoreApi login are a separate provisioner artifact under `deploy/database/`. No production role-creation or credential-rotation automation is claimed.

## Required deployment configuration

A deployment must provide the public Branding values, Authentication authority/audience, exact `AllowedHosts`, the primary database connection, and exact OpenFGA API/store/model/credential configuration. Checked-in configuration exposes bounded authentication, authorization, bootstrap-cache and database/profile-runtime resource policies for deliberate deployment review and override. Blank, permissive or unsafe configuration fails startup. DbMigrator additionally requires a deployment-specific nonzero advisory-lock key and an explicit bounded lock timeout.

## Explicitly `NOT_INTRODUCED`

No production feature catalog; durable Tenant Application Profile authority; production profile-specific resolution (no production endpoint acquires the internal tenant-keyed profile-runtime registry); real ZITADEL-instance evidence; login/callback/session flows; account/tenant provisioning operations; Owner/Staff or custom-role modeling; ongoing platform/tenant role administration and tuple reconciliation; authorization revision; AdminApi and normal registered-Admin-device request validation/lifecycle; tenant/Workstation device lifecycle; a broader order/business lifecycle (quotation, admission, fulfillment, invoice, payment, credit, return, refund); an external PostgreSQL pooler; Worker; Web UI; Workstation; a general ApplicationKernel/module runtime.

## Excluded from the inventory

The ignored `reference-sources/snapshots/` research workspace may contain upstream `.csproj`, source and test files at pinned revisions. They are external evidence only: not application projects, not referenced by product code, and not counted above.

The deleted `PartyKind` slice and its executable scaffolding were purged on 2026-09-17. The decision and exact removed inventory are recorded in `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md`.

## Current gate state

Phase 0A remains the qualified reset baseline. Its enduring guarantees still govern new work: current authority must be distinguishable from history, boundaries must be earned by real responsibility, and every introduced claim needs falsifiable evidence plus a lasting regression guard.

The former Phase 0B Parties qualification is retired historical evidence. It does not describe the current tree and does not authorize recreation of its enum, projects, solution, package files, tests, or CI configuration.

The bounded ApplicationProfiles feature compiler, deployment-wide public Branding contract, CoreApi bootstrap/liveness paths, classified OpenAPI v1 document, configured JWT validation, active-account resolution, current active-membership listing, exact account/membership queries, immutable membership-derived `TenantContext`, Finbuckle route-candidate plumbing, pinned-model OpenFGA workspace, Orders and Customers permissions, immutable tenant-owned customer organization/program create/read/browse, optional customer/program-attributed priced order-draft create/read/browse/revise/abandon, caller-scoped semantic idempotency, explicit tenant SQL plus forced PostgreSQL RLS, shared bounded/resetting CoreApi Npgsql data source, and the ordered one-shot PostgreSQL migrator are `PRODUCTION_HONEST` for their declared narrow scopes. The Customer organization/program and Orders attribution evidence and regression guards are owned by `docs/implementation/CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md`; the Orders draft/lifecycle contract remains owned by `docs/implementation/ORDER_DRAFT_INTAKE_SLICE.md`, including the separate `order_editor` and `order_abandoner` permissions, revision-checked changes, lifecycle metadata and scoped persistence rights. That owner defines host-neutral, ASP.NET pipeline, OpenFGA and PostgreSQL evidence for create, read, browse, revise and abandon. The local `COLLECT_COVERAGE=1 ./eng/verify.sh` run on 2026-09-24 passed locked restore, formatting, Release build, all 307 tests and coverage report generation. Remote CI and a production deployment are separate claims. Tenant-specific branding, real ZITADEL topology/flows, broader OpenFGA roles/administration and the broader business lifecycle remain `NOT_INTRODUCED`. `BLOCKED = none`.

## Active implementation rule

The 2026-10-01 local full parallel `./eng/verify.sh` run passed locked restore,
format verification, Release build and all **330 tests**, with zero failures or
skips. This qualifies the narrow failure/admission contracts above and the root
version-marker guard. It also reverified the existing Orders snapshot-read
regression under actual PostgreSQL. Successful customer organization/program
creates and Orders draft create/revise/abandon now emit safe structured outcome
logs and bounded-label counters, distinguishing commits from replays. Optional
diagnostic sink failures cannot invalidate those completed outcomes. Scope,
regression evidence and non-claims are owned by
`docs/implementation/CORE_API_MUTATION_DIAGNOSTICS.md`; this is best-effort
diagnostics, not durable audit. Coverage was not collected in this run;
the earlier coverage result above remains historical. No remote CI, production
deployment or complete commercial-backend qualification is inferred from this run.

The subsequent 2026-10-02 local full parallel `./eng/verify.sh` run passed all
**334 tests**, with zero failures or skips, after locked restore, formatting and
Release build. It qualifies canonical, culture-independent customer pagination
validation under the existing v1 cursor format; the focused Customers owner
records compatibility and regression evidence. Existing PostgreSQL guarantees
were reverified. This run did not collect coverage or qualify a deployment.

A later 2026-10-02 local full parallel `./eng/verify.sh` run passed all **339
tests**, with zero failures or skips and zero Release build warnings/errors.
Customers runtime SQL is now adapter-owned embedded resources, matching Orders;
an architecture guard prevents inline runtime SQL drift in those adapters.
Real PostgreSQL tests verify completed/disposed session rejection, rollback on
disposal, transaction-local tenant cleanup through a one-connection pool and
direct adapter page-size bounds before connection acquisition. The focused
Customers owner records those guarantees and requalification triggers. This
run did not collect coverage or qualify remote CI or production deployment.

The protected CoreApi pipeline now has a configurable cooperative request budget
(`RequestBudget:ProtectedRequestTimeoutSeconds`, required 1–120; default 30).
Deadline cancellation before headers start returns safe `504` / `request_timeout`
when it escapes as cancellation, preserves protected no-store and releases
admission capacity as the pipeline unwinds. Public routes retain their existing
policies. The focused owner `docs/implementation/CORE_API_REQUEST_BUDGETS.md`
records limits, retry semantics and guards; this does not guarantee forced
termination or rollback. Its 2026-10-02 local full parallel gate passed all **348
tests**, zero failures/skips and zero build warnings/errors. Coverage, remote CI
and deployment remain separate claims.

CoreApi now validates every mapped route's access classification against explicit
authentication metadata before serving requests. Invalid or anonymous-overridden
protected declarations fail startup. Real-host tests also verify anonymous denial
and no-store across every current protected route/method with forged admin headers.
The focused owner is `docs/implementation/CORE_API_ENDPOINT_ACCESS_GUARD.md`.
The 2026-10-02 full parallel gate passed **351 tests**, zero failures/skips and
zero Release warnings/errors. This is an existing-host safeguard; Admin API and
its platform/device authorization remain `NOT_INTRODUCED`. The expanded delivery
sequence is recorded in `docs/implementation/DELIVERY_PLAN.md`.

The end-to-end business scope is now mapped in
`docs/implementation/BUSINESS_OPERATION_END_TO_END.md`, including conditional
pricing, quotation/approval, fulfillment/outsourcing, purchasing/stock,
receivable/payable, settlement and correction responsibilities. Current supplied
draft selling-price calculations are separated from order-intent assembly in
`Application.Orders/Pricing/OrderDraftPriceCalculator.cs`; the focused boundary
is `docs/implementation/PRICING_COMPONENT_BOUNDARY.md`. No new lifecycle state,
price-selection authority or customer-specific workflow is introduced. The
2026-10-02 full parallel gate passed all **353 tests**, zero failures/skips and
zero Release warnings/errors. `BLOCKED = none`; the business map is scope guidance,
not a completed commercial backend. Coverage, remote CI and deployment were not
qualified by this run.

CoreApi now exposes a protected, non-persisting order-entry price preview using
the same calculator as draft creation. The focused pricing owner above records
authorization, resource bounds, parity and no-mutation guards. The 2026-10-02
normal parallel `./eng/verify.sh` run passed all **373 tests**, zero failures/skips
and zero Release warnings/errors. The owner selected operator-entered prices
first with controlled overrides; separate manual pricing authority is now implemented as described below; richer
reason/approval/ceiling policy remains unintroduced. Product
version remains **v0.1.0**; remote CI, coverage and deployment remain separate.

### Retained draft history and order-entry boundaries, 2026-10-02

Protected order history is `PRODUCTION_HONEST` within
`docs/implementation/ORDER_DRAFT_HISTORY.md`: bounded revision pages reuse
successful immutable receipts, retain historical prices/actors and enforce current
viewer authority. Create/revise now also bound the actual request stream,
preserve native supported charsets, and return safe 413/400 responses for
oversized bodies/unsupported charset names. Their owner and recurring HTTP
regression evidence remain in `ORDER_DRAFT_INTAKE_SLICE.md`.

The final normal parallel `./eng/verify.sh` passed all **413 tests** in twelve test
projects, with zero failures/skips and zero Release warnings/errors. A preceding
attempt exposed Testcontainers image-parser startup timeouts; fixed-image fixtures
now use the supported structured-image API, with unchanged provider versions and
no test retries or serialization. See `docs/testing/VERIFICATION_STRATEGY.md`.
`BLOCKED = none` for these declared scopes. Acceptance, adaptive price-source/approval policy,
fulfillment, billing/invoices and settlement remain `NOT_INTRODUCED`; this does not
qualify the complete commercial backend, remote CI or a production deployment.

### Draft action guidance and separate manual price authority, 2026-10-02

The protected draft action guide explains the observed revision and current
revision/abandonment availability without granting execution authority; its owner
is `docs/implementation/ORDER_DRAFT_ACTION_GUIDANCE.md`. Manual price entry now
requires its own `manual_pricer` grant alongside create for initial entry/preview,
or edit for full priced replacement, including unchanged submitted prices and
idempotent retries. Read and abandonment remain independent. The pricing owner
above records authority, historical facts, failure/recovery and model rollout.

Both declared scopes are `PRODUCTION_HONEST`, `BLOCKED = none`. The final normal
parallel `./eng/verify.sh` passed all **450 tests**, locked restore, formatting
and Release build with zero failures/skips and zero warnings/errors, including
actual PostgreSQL and OpenFGA regressions. Deployments must publish/pin the updated
model and explicitly grant intended pricing tuples. Acceptance, adaptive pricing
policy/reasons/approvals, fulfillment, debtor/invoices and settlement remain
`NOT_INTRODUCED`; this does not qualify the whole backend or production deployment.
Product version remains **v0.1.0**.

The next slice starts from a useful application responsibility, not a phase label or deleted project shape. Before custom infrastructure is written, use `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` to decide whether to:

1. use a focused maintained package;
2. adapt bounded source when its license permits the intended use and distribution;
3. reuse tests, failure cases, or algorithms while retaining repository ownership; or
4. keep the source as reference with a concrete rejection reason.

For any selected source, record its immutable revision, exact inspected types/tests, license, entry mode, framework assumptions, repository-owned authority, known gaps, exit path, and repository-owned evidence.

License is not an exclusion filter for internal research. Research access does not itself authorize copying, dependency adoption or distribution; those decisions record and satisfy the applicable obligations when they occur.

The current source-owned backend base decision and subsystem ledger are in `docs/review/FULLSTACKHERO_BACKEND_ADOPTION_LEDGER.md`. Continue by admitting one real responsibility at a time; a narrow scope is allowed, while prototype-grade depth for an introduced claim is not.

## Verification

Repository verification is:

```bash
./eng/verify.sh
```

A selected executable can be built and verified independently of other hosts:

```bash
./eng/build-host.sh core-api --publish
./eng/verify-host.sh core-api
```

These commands use the host project graph, not the entire solution. Shared
libraries remain real dependencies. The scope, other implemented targets and
CI failure boundaries are owned by `docs/implementation/INDEPENDENT_HOST_BUILDS.md`.

The script restores from committed NuGet lockfiles, audits dependencies, verifies formatting, builds the solution in Release configuration and runs the complete test suite. `COLLECT_COVERAGE=1 ./eng/verify.sh` also produces an ignored local Coverlet/ReportGenerator report under `artifacts/coverage/report/`. Provider tests require Docker because they run PostgreSQL 17 through Testcontainers. The current environment may require writable `NUGET_PACKAGES` and `NUGET_HTTP_CACHE_PATH` locations. GitHub and GitLab verification wrappers invoke the same script with coverage and retain its report; their execution is not claimed until a remote run is inspected. The current testing-tool scope and security scan configuration are recorded in `docs/testing/VERIFICATION_STRATEGY.md`.

## Authority

Read in this order:

1. this file for current implementation inventory;
2. `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md` for the purge decision;
3. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` for the qualified reset guarantees;
4. `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` for source-first routing;
5. the focused owner and current decision record for the responsibility being introduced.

Historical implementation, phase, review, branch, and CI records remain evidence and context only when they conflict with this current inventory.
