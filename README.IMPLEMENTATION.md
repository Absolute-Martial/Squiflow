# SquiFlow Current Implementation Truth

**Product version:** `v0.1.0`, locked until the complete production-capable product gate

**Repository name:** internal development codename; public runtime identity is configuration-owned

**Current state:** first backend/API vertical slice implemented

## Current inventory

```text
production projects: 12
test projects:       12
executable hosts:    2
solution files:      1
repository build/test contract: present
active runtime responsibilities: public application bootstrap; classified OpenAPI v1 description; JWT access-token validation; authenticated account resolution; tenant membership listing/context resolution; Finbuckle route-candidate resolution; pinned-model OpenFGA tenant-workspace, customer organization/program and distinct order create/view/edit/abandon permissions; account/tenancy/customer/order-draft persistence; liveness and bounded dependency readiness; Autofac root composition; bounded dormant tenant-keyed profile-runtime mechanics; ordered one-shot DB migration
active host-neutral responsibilities: bounded application-feature definition, dependency-graph validation and deterministic effective-selection compilation; immutable tenant-owned customer organization/program creation and queries; optional customer/program attribution on priced order-draft create/read/browse/revise/abandon; caller-scoped semantic idempotency
BLOCKED: none
```

The current solution contains compact host-neutral ApplicationProfiles, Branding, IdentityAccess, Tenancy, Customers and Orders capabilities, capability-owned PostgreSQL adapters, an ASP.NET Core CoreApi host, and a separate one-shot database migrator. ApplicationProfiles validates a bounded shipped feature graph and compiles an explicit request into a deterministic dependency-closed selection with stable catalog/selection fingerprints; it contains no production feature catalog or durable tenant profile authority. CoreApi uses Autofac as its root service provider while retaining standard `IServiceCollection` registrations. Its internal tenant-keyed profile-runtime registry proves bounded single-flight construction, operation-scoped tenant context, idle retirement, draining, disposal and metrics. Identical implementation fingerprints do not share one retained scope across tenants. No production endpoint acquires that registry. `GET /api/v1/application/bootstrap` exposes bounded public white-label identity. `GET /openapi/v1.json` exposes the current executable HTTP contract using the configured public brand rather than the repository codename; its configured OpenID Connect discovery scheme is attached only to operations that require authorization. The IdentityAccess schema durably binds one or more exact OIDC `(issuer, subject)` identities to a stable application account and deliberately stores no password, role or permission authority. `GET /api/v1/account` validates a configured HTTPS issuer, exact audience, signature and lifetime through ASP.NET Core JWT bearer authentication before returning an active bound account. `GET /api/v1/account/tenants` returns only current active tenant memberships for that account; the host-neutral resolver creates `TenantContext` only from the same current membership authority. `GET /api/v1/tenants/{tenantId}/workspace` uses Finbuckle route resolution only to capture the untrusted candidate, establishes the current account and membership-derived `TenantContext`, and then uses ASP.NET Core resource authorization plus OpenFGA to require the pinned model's persisted `workspace_viewer` relation. Customer organization/program create, detail and bounded browse routes are under `/api/v1/tenants/{tenantId}/customers/organizations`, with nested `/programs` routes; distinct pinned OpenFGA create/view permissions protect each resource after current membership. Order creation, detail, browse, revision and abandonment routes are `POST /api/v1/tenants/{tenantId}/orders`, `GET /api/v1/tenants/{tenantId}/orders/{orderId}`, `GET /api/v1/tenants/{tenantId}/orders`, `PUT /api/v1/tenants/{tenantId}/orders/{orderId}/draft`, and `POST /api/v1/tenants/{tenantId}/orders/{orderId}/abandon`. Each establishes the current account and `TenantContext`; OpenFGA checks distinct persisted `order_creator`, `order_viewer` (detail and browse), `order_editor`, or `order_abandoner` relations. Customer organization/program creates and Order create/revise/abandon use caller-scoped semantic idempotency keys. Order create and revise optionally accept a tenant-owned organization and its program, and detail/browse expose that attribution without treating it as legal billing authority. Browse is bounded newest-first keyset pagination with a versioned tenant-bound cursor and no count or text-search claim. Revision replaces the entire priced draft only while it is still a draft and the expected revision matches, increments revision and preserves earlier command receipts. Abandonment uses an expected revision for a one-way `draft` to `abandoned` transition, retains priced content and the original create receipt, records the abandoning account and authoritative timestamp, and increments the current revision; detail and browse expose current lifecycle state. It does not delete the draft or reverse financial, inventory or fulfillment effects. Orders persistence uses explicit tenant SQL predicates plus forced PostgreSQL RLS with transaction-local tenant context. OpenFGA receives verified membership only as a contextual tuple, uses opaque GUID-based tuple identifiers, checks with `HIGHER_CONSISTENCY`, and fails closed with a bounded safe `503` when the provider is unavailable.

Liveness is dependency-free; readiness is a status-only, five-second single-flight cached check of primary PostgreSQL connectivity and read access to the configured OpenFGA model. It does not substitute for migration verification or an authorized business smoke journey.

The one-shot DatabaseMigrator uses an explicit ordered registry of IdentityAccess, Tenancy, Customers and Orders migration contributions; each PostgreSQL adapter owns its context factory and migration files. The migrator owns the advisory lock, command/exit behavior and ordering, not module schema decisions. Each PostgreSQL adapter also owns its current runtime DI registration, persistence code and migration contribution. CoreApi owns the shared bounded data source, invokes those module registrations and keeps its authentication/authorization middleware sequence visible in `Program.cs`. Branding owns its validated deployment-supplied public identity; CoreApi owns the bootstrap endpoint's HTTP cache policy. `Application.Architecture.Tests` checks the current dependency graph, provider isolation, solution membership and these inventory counts. Database grants for the existing restricted CoreApi login are a separate provisioner artifact under `deploy/database/`; no production role-creation or credential-rotation automation is claimed.

The organization/program association is an attribution contract, not legal debtor, account billing, credit, invoice or settlement authority. The repository does not yet contain a production feature catalog, durable Tenant Application Profile authority, production profile-specific resolution, real ZITADEL-instance evidence, login/callback/session flows, account/tenant provisioning operations, Owner/Staff or custom-role modeling, application tuple administration/reconciliation, authorization revision, a broader order/business lifecycle, an external PostgreSQL pooler, devices, Worker, Web UI, Workstation or a general ApplicationKernel/module runtime. Those responsibilities remain `NOT_INTRODUCED`. A deployment must provide the public Branding values, Authentication authority/audience, exact `AllowedHosts`, primary database connection, and exact OpenFGA API/store/model/credential configuration; checked-in configuration exposes bounded authentication, authorization, bootstrap cache and database/profile-runtime resource policies for deliberate deployment review and override. Blank, permissive or unsafe configuration fails startup. DbMigrator additionally requires a deployment-specific nonzero advisory-lock key and explicit bounded lock timeout.

The ignored `reference-sources/snapshots/` research workspace may contain upstream `.csproj`, source and test files at pinned revisions. Those files are external evidence only: they are not application projects, are not referenced by product code, and are excluded from this implementation inventory.

The deleted `PartyKind` slice and its executable scaffolding were purged on 2026-09-17. The decision and exact removed inventory are recorded in `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md`.

## Current gate state

Phase 0A remains the qualified reset baseline. Its enduring guarantees still govern new work: current authority must be distinguishable from history, boundaries must be earned by real responsibility, and every introduced claim needs falsifiable evidence plus a lasting regression guard.

The former Phase 0B Parties qualification is retired historical evidence. It does not describe the current tree and does not authorize recreation of its enum, projects, solution, package files, tests, or CI configuration.

The bounded ApplicationProfiles feature compiler, deployment-wide public Branding contract, CoreApi bootstrap/liveness paths, classified OpenAPI v1 document, configured JWT validation, active-account resolution, current active-membership listing, exact account/membership queries, immutable membership-derived `TenantContext`, Finbuckle route-candidate plumbing, pinned-model OpenFGA workspace, Orders and Customers permissions, immutable tenant-owned customer organization/program create/read/browse, optional customer/program-attributed priced order-draft create/read/browse/revise/abandon, caller-scoped semantic idempotency, explicit tenant SQL plus forced PostgreSQL RLS, shared bounded/resetting CoreApi Npgsql data source, and the ordered one-shot PostgreSQL migrator are `PRODUCTION_HONEST` for their declared narrow scopes. The Customer organization/program and Orders attribution evidence and regression guards are owned by `docs/implementation/CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md`; the Orders draft/lifecycle contract remains owned by `docs/implementation/ORDER_DRAFT_INTAKE_SLICE.md`, including the separate `order_editor` and `order_abandoner` permissions, revision-checked changes, lifecycle metadata and scoped persistence rights. That owner defines host-neutral, ASP.NET pipeline, OpenFGA and PostgreSQL evidence for create, read, browse, revise and abandon. The local `COLLECT_COVERAGE=1 ./eng/verify.sh` run on 2026-09-24 passed locked restore, formatting, Release build, all 307 tests and coverage report generation. Remote CI and a production deployment are separate claims. Tenant-specific branding, real ZITADEL topology/flows, broader OpenFGA roles/administration and the broader business lifecycle remain `NOT_INTRODUCED`. `BLOCKED = none`.

## Active implementation rule

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

The script restores from committed NuGet lockfiles, audits dependencies, verifies formatting, builds the solution in Release configuration and runs the complete test suite. `COLLECT_COVERAGE=1 ./eng/verify.sh` also produces an ignored local Coverlet/ReportGenerator report under `artifacts/coverage/report/`. Provider tests require Docker because they run PostgreSQL 17 through Testcontainers. The current environment may require writable `NUGET_PACKAGES` and `NUGET_HTTP_CACHE_PATH` locations. GitHub and GitLab verification wrappers invoke the same script with coverage and retain its report; their execution is not claimed until a remote run is inspected. The current testing-tool scope and security scan configuration are recorded in `docs/testing/VERIFICATION_STRATEGY.md`.

## Authority

Read in this order:

1. this file for current implementation inventory;
2. `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md` for the purge decision;
3. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` for the qualified reset guarantees;
4. `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` for source-first routing;
5. the focused owner and current decision record for the responsibility being introduced.

Historical implementation, phase, review, branch, and CI records remain evidence and context only when they conflict with this current inventory.
