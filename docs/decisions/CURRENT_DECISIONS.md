# Current Decisions — v0.0.15

This file records accepted direction only. Detailed reasoning and changes from the decision audit live in `docs/review/DECISION_AUDIT.md`.

## Product and runtime

- C# / modern .NET is the application foundation.
- ASP.NET Core is the Core API host.
- Avalonia is the Windows Workstation UI framework.
- Blazor Web App is the tenant Web presentation foundation and the future Platform Admin Web presentation foundation.
- The business core is a modular monolith. A module does not become a service merely because it has a name.
- `apps/web`, `apps/desktop`, and `services/core-api` are early executable boundaries.
- `apps/admin-web` is a separate platform-control-plane UI executable and is created when the platform-admin slice needs it.
- **`services/admin-api` is a separate Platform Admin backend executable and deployment boundary from `services/core-api`.** Platform/super-admin operations do not depend on Core API being available and are not hosted as `/platform-admin/...` routes on Core API.
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
- Clean-code/SOLID principles are design-review guidance, not reasons to create ceremonial layers. Prefer meaningful business names, cohesive responsibilities, shallow/readable control flow, and explicit policy/config values; tolerate small duplication when the alternative is a wrong generic abstraction.
- Liskov/interface-segregation implications apply to accepted provider seams: replacement adapters must honor the same SquiFlow contract and interfaces stay narrower than the third-party SDKs they hide.

## Small-team tenant control

- `Owner` + `Staff` are the default small-team role templates.
- Tenant Owner controls ordinary staff permissions inside SquiFlow security/entitlement limits.
- Role/permission assignment and tenant rule/workflow/form publication are Web-only tenant-administration operations.
- Tenant administration lives in the normal tenant Web Settings/Administration area and uses the ordinary Core API tenant-admin surface.
- Platform-critical application controls are available only through the separate Platform Admin Web **and separate Admin API backend** during normal operation.
- Platform Admin Web does not call Core API as its normal platform-command backend.
- Desktop never grants permissions or changes platform control-plane state.

## Identity and authorization stack

- **ZITADEL is the current identity/authentication platform choice** for interactive authentication, account/session/MFA/SSO capability, using standards-based OpenID Connect/OAuth integration.
- Workstation login uses ZITADEL through the system browser + Authorization Code + PKCE `S256`; no reusable native client secret or central DB credential is embedded in the Workstation.
- Stable external account identity remains `(issuer, subject)`, not email.
- **OpenFGA is the current application-authorization engine choice** for tenant roles, tenant-defined custom roles, role assignments, stable permissions/relations, and resource relationship checks where applicable.
- ZITADEL authentication and OpenFGA application authorization are separate concerns. ZITADEL role/token claims are not treated as current SquiFlow business authorization truth.
- ASP.NET Core policy/requirements/`IAuthorizationService` remain the server integration point. Core API uses tenant authorization; Admin API uses separate platform authorization. SquiFlow domain/workflow/concurrency rules still run separately.
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
- Local-first synchronization is not described as generic eventual consistency: users can see whether work is only local/pending or centrally authoritative.

## Multi-tenancy and persistence

- Ordinary tenants use a pooled multi-tenant baseline with explicit tenant discriminators on tenant-owned authoritative data.
- Authentication, authorization, and tenant isolation are separate concerns.
- Schema-per-tenant, DB-per-tenant, queue-per-tenant, and deployment-per-tenant are not baseline.
- PostgreSQL remains the strongest central reference candidate; if used, its proof includes RLS defense in depth and safe runtime-role/connection-pool behavior.
- SQLite + WAL and libSQL remain Workstation-store candidates.
- Exact central and local database products remain open until the relevant vertical-slice POCs close them.
- Authoritative relational data is normalized around real business identities/relationships first. Denormalized/materialized read structures are derived optimizations with explicit source, freshness, rebuild, tenant-scope, and failure contracts.
- Core business invariants are not hidden in arbitrary JSON/EAV or tenant-specific DDL merely to avoid schema design. Bounded custom fields/forms are a separate extensibility concern.
- Indexes are workload-driven: each important index/constraint must protect a real query/invariant and its write, storage, WAL, migration, and sync/import costs are measured. “Index every filterable column” is not baseline.
- Tenant-local uniqueness and hot tenant-scoped queries use tenant-aware keys/indexes where appropriate, but index shape is confirmed by actual query plans/cardinality rather than a mechanical prefix rule.

## Consistency model

- SquiFlow does not choose one consistency model for the whole product.
- Current/strong authority is required where temporary disagreement can create unsafe business effects, including sensitive authorization, tenant isolation, shared stock/credit decisions, payment/refund authority, unique issued-document truth, and expected-version state transitions.
- Eventual/derived consistency is acceptable for consequences such as notifications, telemetry, non-authoritative caches, and read/search/report projections when their freshness and rebuild behavior are explicit.
- An eventually updated projection declares its authoritative source, freshness/version evidence where material, duplicate/out-of-order handling, and reconciliation/rebuild path.
- Stale derived data must not silently become current authority for permissions, payment, stock, credit, or another protected invariant.

## API, sync, and Worker correctness

- REST/task-oriented HTTP is the v0.0.15 application API baseline. GraphQL and GraphQL Federation are deferred until a real client/query-composition requirement justifies their query-cost, authorization, caching, schema, and N+1 complexity.
- Retryable mutating operations use caller-provided semantic idempotency keys.
- Same idempotency key + changed intent is rejected.
- Where one store owns mutation + idempotency receipt + outbox, they commit atomically.
- At-least-once delivery/redelivery is assumed; effects are idempotent or explicitly reconcilable.
- Retry is finite, classified, budgeted, and uses backoff/jitter/`Retry-After` where appropriate.
- Long-running HTTP work uses durable asynchronous status only when work is actually long-running; ordinary short business transactions remain synchronous.
- Conflict handling is aggregate-specific; no global last-write-wins policy.
- Large collection APIs are paginated/bounded. Connection pools are bounded/measured and must not leak tenant-scoped DB context across reused connections.
- Caching, response compression, and asynchronous telemetry logging are selective performance techniques, not default correctness mechanisms. Security/business audit is not allowed to exist only in a lossy async log buffer.
- Rate limiting/admission is multi-dimensional where needed (IP/unauthenticated abuse, account/device, tenant, endpoint/work class, expensive provider action, platform admin, downstream budget). Authorization and throttling are separate decisions.
- Temporary HTTP throttling uses stable errors and `429`/`Retry-After` where applicable; clients back off rather than amplify overload.

## Network edge and service-to-service traffic

- An edge reverse proxy/API-gateway capability may terminate TLS, route hostnames, enforce request-size/WAF/access policy, and apply coarse rate limiting when the deployment needs it.
- Edge/gateway controls do not replace Core API or Admin API authentication, OpenFGA authorization, TenantContext isolation, domain validation, or operation-specific admission.
- Core API and Admin API remain independent backend/runtime planes even when one edge technology routes to both; Admin API does not route through Core API.
- A service mesh is not baseline. Revisit only if independently deployed east-west service traffic becomes large/complex enough that mTLS, discovery, traffic policy, and distributed observability justify the added runtime/operational cost.

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
- Core API and Admin API have independent process/deployment health. A Core API outage must not automatically remove the Platform Admin application control surface; an Admin API outage must not block ordinary tenant business API work.
- OpenTelemetry/OTLP is the instrumentation boundary. New Relic + Aiven OpenSearch are current managed targets and Backtrace remains the crash-diagnostics direction.
- Guard contributes bounded Workstation lifecycle/crash/resource evidence into diagnostics without becoming business authority.
- Telemetry-provider failure/quota exhaustion cannot block business transaction correctness.

## Explicitly not baseline

- formal accessibility/a11y work as a separate v0.0.15 project/gate;
- full browser offline sync;
- generic repository/unit-of-work/one-interface-per-class abstractions;
- Kafka, mandatory Redis, event-sourced/full-CQRS/Saga core architecture;
- GraphQL/GraphQL Federation;
- service mesh;
- eventual-consistency-everywhere;
- denormalized authoritative core schema;
- global CRDTs;
- microservice-per-module design;
- per-tenant infrastructure by default;
- advanced peripheral suite, MRP/wastage, specialized ETL/search, or SaaS billing engine without a current customer/commercial requirement.
