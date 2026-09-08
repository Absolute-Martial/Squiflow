# Current Decisions — v0.0.15

This file records accepted direction only. Detailed reasoning and changes from the decision audit live in `docs/review/DECISION_AUDIT.md`.

## Product and runtime

- C# / modern .NET is the application foundation.
- ASP.NET Core is the Core API host.
- Avalonia is the Windows Workstation UI framework.
- Blazor Web App is the tenant Web presentation foundation and the future Platform Admin Web presentation foundation.
- The business core is a modular monolith. A module does not become a service merely because it has a name.
- `apps/web`, `apps/desktop`, and `services/core-api` are the first executable boundaries needed by the early vertical slices.
- `apps/admin-web` remains a separate future platform-control-plane executable, but its project is not created until a platform-admin slice needs it.
- `services/worker` remains a separate future durable background executable, but its project is not created until durable background work is implemented.
- There is **no baseline `SquiFlow.Guard` process**. Start with one Workstation process. Add a helper/supervisor process only after a concrete updater/native-library/crash-isolation requirement proves that process isolation is worth its lifecycle cost.
- Printing is a Workstation device side effect. Other peripherals are requirement-driven.

## Complexity and code-shape rule

- Do not create empty projects/directories to match an architecture diagram.
- Do not create generic helper/manager/service layers that only forward calls.
- Do not introduce an interface merely because an implementation class exists or because mocking it is possible.
- Generic `IRepository<T>`, `IUnitOfWork`, provider-neutral wrapper layers, and one-interface-per-class conventions are **not baseline**.
- Prefer concrete framework/provider integrations contained inside the appropriate infrastructure/application boundary. Introduce an interface only when a real dependency-inversion, multiple-live-implementation, process/wire-contract, or replacement need earns it.
- Provider portability means provider details do not leak throughout business code; it does **not** require speculative abstraction layers before the first provider is implemented.

## Small-team tenant control

- `Owner` + `Staff` are the default small-team role templates.
- Tenant Owner controls ordinary staff permissions inside SquiFlow security/entitlement limits.
- Role/permission assignment and tenant rule/workflow/form publication are Web-only tenant-administration operations.
- Tenant administration lives in the normal tenant Web Settings/Administration area.
- Platform-critical application controls are available only through the separate Platform Admin Web during normal operation.
- Desktop never grants permissions or changes platform control-plane state.

## Identity and authorization

- Interactive authentication uses OpenID Connect; application authorization remains SquiFlow-owned.
- Stable external account identity is `(issuer, subject)`, not email.
- Workstation login uses the system browser + Authorization Code + PKCE `S256`; no reusable native client secret or central DB credential is embedded in the Workstation.
- ASP.NET Core policy/requirements/`IAuthorizationService` are the authorization runtime primitives; SquiFlow does not build a competing authorization service.
- Authorization separates function, tenant/resource, sensitive-property, domain/workflow-state, and concurrency checks where applicable.
- Permission changes advance `TenantAuthorizationRevision`; Workstation permission snapshots never replace authoritative server reauthorization.

## Web and Workstation

- Web is online-only for business operations in v0.0.15. No IndexedDB business replica, service-worker business sync, or browser offline mutation queue is baseline.
- Valuable online forms may use explicit server-side drafts/autosave when justified.
- Workstation is the local-first/offline client.
- Local Workstation success and server-authoritative acceptance are separate states (`LocalCommitted`, `PendingRemote`, `Authoritative`, `Conflict`, `Rejected`, `AuthorizationChanged`, `UpgradeRequired`).
- SquiFlow adopts local-first interaction/durability, not a global CRDT or peer-authority model for payments, stock, credit, permissions, or other shared invariants.

## Multi-tenancy and persistence

- Ordinary tenants use a pooled multi-tenant baseline with explicit tenant discriminators on tenant-owned authoritative data.
- Authentication, authorization, and tenant isolation are separate concerns.
- Schema-per-tenant, DB-per-tenant, queue-per-tenant, and deployment-per-tenant are not baseline.
- PostgreSQL remains the strongest central reference candidate; if used, its proof includes RLS defense in depth and safe runtime-role/connection-pool behavior.
- SQLite + WAL and libSQL remain Workstation-store candidates.
- Exact central and local database products remain open until the relevant vertical-slice POCs close them.

## API, sync, and Worker correctness

- Retryable mutating operations use caller-provided semantic idempotency keys.
- Same idempotency key + changed intent is rejected.
- Where one store owns mutation + idempotency receipt + outbox, they commit atomically.
- At-least-once delivery/redelivery is assumed; effects are idempotent or explicitly reconcilable.
- Retry is finite, classified, budgeted, and uses backoff/jitter/`Retry-After` where appropriate.
- Long-running HTTP work uses durable asynchronous status only when work is actually long-running; ordinary short business transactions remain synchronous.
- Conflict handling is aggregate-specific; no global last-write-wins policy.

## Rules/workflow

- SquiFlow owns the bounded native rule model; arbitrary tenant C#/JS/SQL is not allowed.
- Rules/workflows are edited/published through Web administration and distributed as immutable/versioned compatible snapshots.
- Workstation local rule evaluation cannot turn stale server-owned facts into authoritative financial/stock/security decisions.
- Workflow is continuation-first and versioned.

## Money/currency

- Currency is **not hardcoded** in application logic.
- A tenant has a configurable default currency code and monetary records that need historical meaning retain the applicable currency code.
- v0.0.15 does **not** add a multi-currency ledger, exchange-rate service, FX conversion engine, gain/loss accounting, or currency-provider abstraction.
- If a real multi-currency customer requirement appears, that is a later feature decision.

## Object storage and backups

- The current bootstrap primary object store is a **private Hugging Face Storage Bucket**, with the currently available private-storage envelope of about **100 GB** treated as a real limit.
- Hugging Face is a bootstrap provider, not a permanent architecture commitment. The planned migration trigger is the first paying customer; migrate earlier if capacity, rate limits, reliability, contractual, privacy, compliance, or operational requirements demand it.
- Application business records store object metadata/ownership/hash/lifecycle; retained/issued objects use application-level immutable/versioned keys even though the bucket itself is mutable.
- The current bootstrap off-site backup target is a **private Kaggle Dataset** containing only encrypted opaque backup archives, never raw customer tables/files.
- Kaggle backup use is temporary. The first paying customer is the planned trigger to move to a purpose-built paid backup/storage arrangement, or earlier if capacity/security/restore requirements demand it.
- Backups are not considered valid until download + integrity verification + restore has been proven.

## Operations and observability

- Current server hardware is lower-spec/desktop-class rack hardware; `stateless` does not imply automatic failover or zero downtime.
- Resource use is explicitly bounded; spare CPU/RAM is headroom rather than permission for caches/workers to grow without limit.
- OpenTelemetry/OTLP is the instrumentation boundary. New Relic + Aiven OpenSearch are current managed targets and Backtrace remains the crash-diagnostics direction.
- Telemetry-provider failure/quota exhaustion cannot block business transaction correctness.

## Explicitly not baseline

- formal accessibility/a11y work as a separate v0.0.15 project/gate;
- full browser offline sync;
- Guard/supervisor/helper process without a proven need;
- generic repository/unit-of-work/provider-wrapper abstractions;
- Kafka, mandatory Redis, event-sourced/full-CQRS/Saga core architecture;
- global CRDTs or Zanzibar-style authorization service;
- microservice-per-module design;
- per-tenant infrastructure by default;
- advanced peripheral suite, MRP/wastage, specialized ETL/search, or SaaS billing engine without a current customer/commercial requirement.
