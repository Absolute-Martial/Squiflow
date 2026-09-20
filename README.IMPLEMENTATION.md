# SquiFlow Current Implementation Truth

**Product version:** `v0.1.0`, locked until the complete production-capable product gate

**Repository name:** internal development codename; public runtime identity is configuration-owned

**Current state:** first backend/API vertical slice implemented

## Current inventory

```text
production projects: 8
test projects:       7
executable hosts:    2
solution files:      1
repository build/test contract: present
active runtime responsibilities: public application bootstrap; classified OpenAPI v1 description; JWT access-token validation; authenticated account resolution; tenant membership listing/context resolution; account/tenancy persistence; Autofac root composition; bounded dormant profile-runtime mechanics; one-shot DB migration
active host-neutral responsibilities: bounded application-feature definition, dependency-graph validation and deterministic effective-selection compilation
BLOCKED: none
```

The current solution contains compact host-neutral ApplicationProfiles, Branding, IdentityAccess and Tenancy capabilities, capability-owned PostgreSQL adapters, an ASP.NET Core CoreApi host, and a separate one-shot database migrator. ApplicationProfiles validates a bounded shipped feature graph and compiles an explicit request into a deterministic dependency-closed selection with stable catalog/selection fingerprints; it contains no production feature catalog or durable tenant profile authority. CoreApi uses Autofac as its root service provider while retaining standard `IServiceCollection` registrations. Its internal profile-runtime registry proves bounded single-flight construction, operation-scoped tenant context, idle retirement, draining, disposal and metrics, but no production endpoint acquires it. `GET /api/v1/application/bootstrap` exposes bounded public white-label identity. `GET /openapi/v1.json` exposes the current executable HTTP contract using the configured public brand rather than the repository codename; its configured OpenID Connect discovery scheme is attached only to operations that require authorization. The IdentityAccess schema durably binds one or more exact OIDC `(issuer, subject)` identities to a stable application account and deliberately stores no password, role or permission authority. `GET /api/v1/account` validates a configured HTTPS issuer, exact audience, signature and lifetime through ASP.NET Core JWT bearer authentication before returning an active bound account. `GET /api/v1/account/tenants` returns only current active tenant memberships for that account; the host-neutral resolver creates `TenantContext` only from the same current membership authority.

The repository does not yet contain a production feature catalog, durable Tenant Application Profile authority, production profile-specific resolution, real ZITADEL-instance evidence, login/callback/session flows, account/tenant provisioning operations, OpenFGA roles/permissions, tenant-owned business tables or RLS, devices, Worker, Web UI, Workstation or a general ApplicationKernel/module runtime. Those responsibilities remain `NOT_INTRODUCED`. A deployment must provide every `Branding` value, `Authentication:Authority`, `Authentication:Audience`, and `ConnectionStrings:PrimaryDatabase`; blank or unsafe identity, trust or database configuration fails startup.

The ignored `reference-sources/snapshots/` research workspace may contain upstream `.csproj`, source and test files at pinned revisions. Those files are external evidence only: they are not SquiFlow projects, are not referenced by product code, and are excluded from this implementation inventory.

The deleted `PartyKind` slice and its executable scaffolding were purged on 2026-09-17. The decision and exact removed inventory are recorded in `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md`.

## Current gate state

Phase 0A remains the qualified reset baseline. Its enduring guarantees still govern new work: current authority must be distinguishable from history, boundaries must be earned by real responsibility, and every introduced claim needs falsifiable evidence plus a lasting regression guard.

The former Phase 0B Parties qualification is retired historical evidence. It does not describe the current tree and does not authorize recreation of its enum, projects, solution, package files, tests, or CI configuration.

The bounded ApplicationProfiles feature compiler, deployment-wide public Branding contract, CoreApi bootstrap/liveness paths, classified OpenAPI v1 document, configured JWT validation, active-account resolution, current active-membership listing, exact account/membership queries, immutable membership-derived `TenantContext`, and the one-shot PostgreSQL migrator are `PRODUCTION_HONEST` for their declared narrow scopes. Real ASP.NET pipeline tests prove that the API document uses configured public identity, declares the configured OpenID Connect authority, applies its security requirement only to protected operations and contains no repository codename. They also reject missing, wrongly issued, wrong-audience, expired, incorrectly signed and incomplete identities with generic safe failures. Real PostgreSQL tests prove model/migration agreement, ordered multi-capability migration, repeat application, bounded migration locking, uniqueness, exact binding lookup, cross-account membership denial, suspension denial, account referential integrity and read-only runtime roles. Tenant-specific branding, real ZITADEL topology/flows, OpenFGA authorization, tenant-owned RLS and business capabilities are `NOT_INTRODUCED`. `BLOCKED = none`.

## Active implementation rule

The next slice starts from a useful application responsibility, not a phase label or deleted project shape. Before custom infrastructure is written, use `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` to decide whether to:

1. use a focused maintained package;
2. adapt bounded source when its license permits the intended use and distribution;
3. reuse tests, failure cases, or algorithms while retaining SquiFlow ownership; or
4. keep the source as reference with a concrete rejection reason.

For any selected source, record its immutable revision, exact inspected types/tests, license, entry mode, framework assumptions, SquiFlow-owned authority, known gaps, exit path, and SquiFlow-owned evidence.

License is not an exclusion filter for internal research. Research access does not itself authorize copying, dependency adoption or distribution; those decisions record and satisfy the applicable obligations when they occur.

The current source-owned backend base decision and subsystem ledger are in `docs/review/FULLSTACKHERO_BACKEND_ADOPTION_LEDGER.md`. Continue by admitting one real responsibility at a time; a narrow scope is allowed, while prototype-grade depth for an introduced claim is not.

## Verification

Repository verification is:

```bash
./eng/verify.sh
```

The script restores, verifies formatting, builds the solution in Release configuration and runs the complete test suite. Provider tests require Docker because they run PostgreSQL 17 through Testcontainers. The current environment may require writable `NUGET_PACKAGES` and `NUGET_HTTP_CACHE_PATH` locations. `.github/workflows/verify.yml` invokes the same script; no remote CI execution claim exists until its run is inspected.

## Authority

Read in this order:

1. this file for current implementation inventory;
2. `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md` for the purge decision;
3. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` for the qualified reset guarantees;
4. `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` for source-first routing;
5. the focused owner and current decision record for the responsibility being introduced.

Historical implementation, phase, review, branch, and CI records remain evidence and context only when they conflict with this current inventory.
