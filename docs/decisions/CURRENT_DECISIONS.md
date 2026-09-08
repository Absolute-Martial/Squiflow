# Current Decisions — v0.0.15

This file records accepted direction only. Detailed reasoning and changes from the decision audit live in `docs/review/DECISION_AUDIT.md`.

## Product and runtime

- C# / modern .NET is the application foundation.
- ASP.NET Core is the Core API host.
- Avalonia is the Windows Workstation UI framework.
- Blazor Web App is the tenant Web presentation foundation and the future Platform Admin Web presentation foundation.
- The business core is a modular monolith. A module does not become a service merely because it has a name.
- `apps/web`, `apps/desktop`, and `services/core-api` are early executable boundaries.
- `apps/admin-web` remains a separate platform-control-plane executable and is created when the platform-admin slice needs it.
- `services/worker` remains a separate durable background executable and is created when durable background work is implemented.
- **`SquiFlow.Guard` is a baseline Workstation companion process.** It owns desktop process supervision, bounded crash/hang recovery, update handoff/recovery, child/helper cleanup, and bounded diagnostic/resource evidence. It does not own business rules, authorization, sync semantics, or central DB access.
- Printing is a Workstation device side effect. Other peripherals are requirement-driven.

## Completeness versus minimalism

- The architecture minimizes unnecessary **layers/components**, not required behavior.
- A component must still fully cover its accepted success, failure, recovery, security, and resource responsibilities.
- Do not remove a real boundary or edge-case capability merely to reduce project/interface/process count.
- Do not create empty projects/directories to match an architecture diagram.
- Do not create generic helper/manager/service layers that only forward calls.
- Do not introduce an interface merely because an implementation class exists or because mocking it is possible.
- Generic `IRepository<T>`, `IUnitOfWork`, and one-interface-per-class conventions are not baseline.
- An interface is justified when there is a concrete dependency-inversion/replacement boundary, including an already-planned near-term provider migration.

## Small-team tenant control

- `Owner` + `Staff` are the default small-team role templates.
- Tenant Owner controls ordinary staff permissions inside SquiFlow security/entitlement limits.
- Role/permission assignment and tenant rule/workflow/form publication are Web-only tenant-administration operations.
- Tenant administration lives in the normal tenant Web Settings/Administration area.
- Platform-critical application controls are available only through the separate Platform Admin Web during normal operation.
- Desktop never grants permissions or changes platform control-plane state.

## Identity and authorization stack

- **ZITADEL is the current identity/authentication platform choice** for interactive authentication, account/session/MFA/SSO capability, using standards-based OpenID Connect/OAuth integration.
- Workstation login uses ZITADEL through the system browser + Authorization Code + PKCE `S256`; no reusable native client secret or central DB credential is embedded in the Workstation.
- Stable external account identity remains `(issuer, subject)`, not email.
- **OpenFGA is the current application-authorization engine choice** for tenant roles, tenant-defined custom roles, role assignments, stable permissions/relations, and resource relationship checks where applicable.
- ZITADEL authentication and OpenFGA application authorization are separate concerns. ZITADEL role/token claims are not treated as current SquiFlow business authorization truth.
- ASP.NET Core policy/requirements/`IAuthorizationService` remain the Core API integration point: handlers/policies invoke OpenFGA where a relationship/permission decision belongs there, then SquiFlow domain/workflow/concurrency rules still run separately.
- OpenFGA does not replace database tenant isolation, business state validation, workflow guards, idempotency, or concurrency checks.
- OpenFGA production calls pin an explicit authorization model ID; model migrations are versioned/controlled rather than silently using whatever model is newest.
- Tenant-created custom role instances/assignments are data/tuples, not a new OpenFGA authorization-model deployment for every role edit.
- OpenFGA tuples use opaque SquiFlow IDs rather than emails or other unnecessary PII.
- Permission/relationship changes return success only after the authoritative OpenFGA change is known/applied; ambiguous external-write outcomes are reconciled rather than assumed successful.
- `TenantAuthorizationRevision` remains SquiFlow evidence/versioning for effective authorization/configuration and Workstation snapshot freshness; it complements rather than replaces OpenFGA model/tuple state.

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

- Currency is not hardcoded in application logic.
- A tenant has a configurable default currency code and monetary records that need historical meaning retain the applicable currency code.
- v0.0.15 does not add a multi-currency ledger, exchange-rate service, FX conversion engine, gain/loss accounting, or currency-provider abstraction.

## Object storage and backups

- The current bootstrap primary object store is a **private Hugging Face Storage Bucket**, with the currently available private-storage envelope of about **100 GB** treated as a real limit.
- Because primary object storage is already planned to change at the first paying customer, a narrow **`IObjectStore`** provider boundary is baseline. `HuggingFaceObjectStore` is the bootstrap implementation; provider SDK types do not leak into business/domain contracts.
- The current bootstrap off-site backup target is a **private Kaggle Dataset** containing only encrypted opaque backup archives, never raw customer tables/files.
- Backup destination access uses a separate infrastructure-level **`IBackupTarget`** boundary. `KaggleBackupTarget` is the bootstrap implementation.
- Backup is an infrastructure recovery concern, not only an application feature: the recoverable set must include all state needed to reconstruct a usable SquiFlow deployment, according to the selected deployment topology.
- Hugging Face/Kaggle are temporary. The first paying customer is the planned trigger to move to purpose-built paid primary/backup providers, or earlier if constraints demand it.
- Backups are not considered valid until download + integrity verification + restore has been proven.

## Operations and observability

- Current server hardware is lower-spec/desktop-class rack hardware; `stateless` does not imply automatic failover or zero downtime.
- Resource use is explicitly bounded; spare CPU/RAM is headroom rather than permission for caches/workers to grow without limit.
- OpenTelemetry/OTLP is the instrumentation boundary. New Relic + Aiven OpenSearch are current managed targets and Backtrace remains the crash-diagnostics direction.
- Guard contributes bounded Workstation lifecycle/crash/resource evidence into diagnostics without becoming business authority.
- Telemetry-provider failure/quota exhaustion cannot block business transaction correctness.

## Explicitly not baseline

- formal accessibility/a11y work as a separate v0.0.15 project/gate;
- full browser offline sync;
- generic repository/unit-of-work/one-interface-per-class abstractions;
- Kafka, mandatory Redis, event-sourced/full-CQRS/Saga core architecture;
- global CRDTs;
- microservice-per-module design;
- per-tenant infrastructure by default;
- advanced peripheral suite, MRP/wastage, specialized ETL/search, or SaaS billing engine without a current customer/commercial requirement.
