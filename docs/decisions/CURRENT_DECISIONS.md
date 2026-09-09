# Current Decisions — v0.0.16

This file records accepted direction only. Detailed reasoning and changes from the decision audit live in `docs/review/DECISION_AUDIT.md`. Observability-specific implementation contracts live under `docs/observability/`.

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
- A component must still fully cover its accepted success, failure, recovery, security, compatibility, resource and operability responsibilities.
- Do not remove a real boundary or edge-case capability merely to reduce project/interface/process count.
- Do not create empty projects/directories to match an architecture diagram.
- Do not create generic helper/manager/service layers that only forward calls.
- Do not introduce an interface merely because an implementation class exists or because mocking it is possible.
- Generic `IRepository<T>`, `IUnitOfWork`, and one-interface-per-class conventions are not baseline.
- An interface is justified when there is a concrete dependency-inversion/replacement boundary, including an already-planned near-term provider migration.
- Clean-code/SOLID principles are design-review guidance, not reasons to create ceremonial layers. Prefer meaningful business names, cohesive responsibilities, shallow/readable control flow, and explicit policy/config values; tolerate small duplication when the alternative is a wrong generic abstraction.
- Liskov/interface-segregation implications apply to accepted provider seams: replacement adapters must honor the same SquiFlow contract and interfaces stay narrower than the third-party SDKs they hide.
- Secondary architecture articles/diagrams are used to surface questions and trade-offs; exact high-impact security/database/protocol/provider claims are closed against current primary specifications, official provider documentation, or foundational papers when available.

## Non-functional, consumption, and operational requirements

- Cross-cutting NFRs are classified as **HardInvariant**, **OperationalTarget**, or **DegradedMode** requirements. Detailed owner: `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md`.
- A capability is not complete merely because the happy path works; material failure, recovery, concurrency, compatibility, consistency/freshness, capacity, security, observability/support and user-understandability behavior must also be defined/tested.
- Numerical latency/resource/recovery targets are measured from representative slices and actual deployment hardware before becoming release/customer promises; architecture does not invent arbitrary p95/p99 or memory numbers.
- **Consumption accounting for a defined metered resource is durable/reconcilable application state when it is needed for enforcement, provider/account capacity, cost, abuse control, support/contract explanation, or future billing. It is not optional analytics and OpenTelemetry is not the authority for it.**
- **Application-level scoped limit enforcement is accepted.** Limits may be platform/provider, workload/resource, tenant, integration/destination, or another explicit bounded scope when the implemented resource requires it.
- A tenant-specific limit does **not** require or imply a subscription tier. Commercial plan names/prices/default allowances remain separate OPEN product decisions.
- Every authoritative meter defines what consumes a unit, whether retries/failed attempts count, unit/scope, correction/reconciliation behavior and consistency needed for hard enforcement.
- Hard limits must define race/atomicity/reservation behavior so concurrent requests cannot both spend the same final allowance; approximate counters cannot silently masquerade as strict contractual enforcement.
- Limit changes are versioned/audited. Lowering a limit below current consumption blocks/degrades new optional usage according to policy; it never silently deletes already committed business state or retained customer objects.
- Workstation offline usage/limit snapshots may support UX but do not override current server/provider hard limits. Pending local intent is preserved if server authority later rejects/defers it because a limit changed or was exhausted.
- Existing `entitlement` wording means a SquiFlow/platform capability/security ceiling or manually controlled product availability where required; it does not imply that commercial subscription tiers already exist.
- Detailed owner: `docs/requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md`.
- Accepted/open/deferred NFR decisions and edge-case challenges are recorded in `docs/review/NFR_DECISION_CHALLENGE.md`.

## Small-team tenant control

- `Owner` + `Staff` are the default small-team role templates.
- Tenant Owner controls ordinary staff permissions inside SquiFlow security/platform-capability constraints; no commercial plan is implied.
- Role/permission assignment and tenant rule/workflow/form publication are Web-only tenant-administration operations.
- Tenant administration lives in the normal tenant Web Settings/Administration area and uses the ordinary Core API tenant-admin surface.
- Platform-critical application controls are available only through the separate Platform Admin Web **and separate Admin API backend** during normal operation.
- Platform Admin Web does not call Core API as its normal platform-command backend.
- Desktop never grants permissions or changes platform control-plane state.
- Tenant-visible usage/limit views may be added per implemented resource, but platform hard caps and authority to raise a tenant limit remain protected platform control. Whether a Tenant Owner may configure a lower self-limit is OPEN per resource.

## Identity and authorization stack

- **ZITADEL is the current identity/authentication platform choice** for interactive authentication, account/session/MFA/SSO capability, using standards-based OpenID Connect/OAuth integration.
- Workstation login uses ZITADEL through the system browser + Authorization Code + PKCE `S256`; no reusable native client secret or central DB credential is embedded in the Workstation.
- Stable external account identity remains `(issuer, subject)`, not email.
- **OpenFGA is the current application-authorization engine choice** for tenant roles, tenant-defined custom roles, role assignments, stable permissions/relations, and resource relationship checks where applicable.
- ZITADEL authentication and OpenFGA application authorization are separate concerns. ZITADEL role/token claims are not treated as current SquiFlow business authorization truth.
- ASP.NET Core policy/requirements/`IAuthorizationService` remain the server integration point. Core API uses tenant authorization; Admin API uses separate platform authorization. SquiFlow domain/workflow/concurrency rules still run separately.
- OpenFGA does not replace database tenant isolation, business state validation, workflow guards, idempotency, concurrency checks, consumption accounting, or limit enforcement.
- OpenFGA production calls pin an explicit authorization model ID; model migrations are versioned/controlled rather than silently using whatever model is newest.
- Tenant-created custom role instances/assignments are data/tuples, not a new OpenFGA authorization-model deployment for every role edit.
- OpenFGA tuples use opaque SquiFlow IDs rather than emails or other unnecessary PII.
- Permission/relationship changes return success only after the authoritative OpenFGA change is known/applied; ambiguous external-write outcomes are reconciled rather than assumed successful.
- `TenantAuthorizationRevision` remains SquiFlow evidence/versioning for effective authorization/configuration and Workstation snapshot freshness; it complements rather than replaces OpenFGA model/tuple state.
- SquiFlow does not add a separate JWT/PASETO authentication subsystem. Token/session format is part of the trusted ZITADEL/OIDC integration and never substitutes for current OpenFGA authorization or TenantContext isolation.

## Web and Workstation

- Web is online-only for business operations in v0.0.16. No IndexedDB business replica, service-worker business sync, or browser offline mutation queue is baseline.
- Valuable online forms may use explicit server-side drafts/autosave when justified.
- Workstation is the local-first/offline client.
- Local Workstation success and server-authoritative acceptance are separate states (`LocalCommitted`, `PendingRemote`, `Authoritative`, `Conflict`, `Rejected`, `AuthorizationChanged`, `UpgradeRequired`).
- SquiFlow adopts local-first interaction/durability, not a global CRDT or peer-authority model for payments, stock, credit, permissions, hard limits, or other shared invariants.
- Local-first synchronization is not described as generic eventual consistency: users can see whether work is only local/pending or centrally authoritative.

## Multi-tenancy and persistence

- Ordinary tenants use a pooled multi-tenant baseline with explicit tenant discriminators on tenant-owned authoritative data.
- Authentication, authorization, and tenant isolation are separate concerns.
- Schema-per-tenant, DB-per-tenant, queue-per-tenant, and deployment-per-tenant are not baseline.
- Shared-resource fairness/noisy-neighbor protection and tenant-scoped resource limits may be used where the implemented workload requires them. They are application/resource policies; they become commercial allowances only if a later product decision maps them to a subscription/contract.
- PostgreSQL remains the strongest central reference candidate; if used, its proof includes RLS defense in depth and safe runtime-role/connection-pool behavior.
- SQLite + WAL and libSQL remain Workstation-store candidates.
- Exact central and local database products remain open until the relevant vertical-slice POCs close them.
- Authoritative relational data is normalized around real business identities/relationships first. Denormalized/materialized read structures are derived optimizations with explicit source, freshness, rebuild, tenant-scope, and failure contracts.
- Core business invariants are not hidden in arbitrary JSON/EAV or tenant-specific DDL merely to avoid schema design. Bounded custom fields/forms are a separate extensibility concern.
- Indexes are workload-driven: each important index/constraint must protect a real query/invariant and its write, storage, WAL, migration, and sync/import costs are measured. “Index every filterable column” is not baseline.
- Tenant-local uniqueness and hot tenant-scoped queries use tenant-aware keys/indexes where appropriate, but index shape is confirmed by actual query plans/cardinality rather than a mechanical prefix rule.
- Central DB selection/tuning starts from an explicit workload profile: read/write/delete mix, representative item sizes, tenant/data skew, normal and reconnect-burst concurrency, sync/import bursts, consistency requirements, hot query cardinalities, and the initial HA/geographic assumptions.
- If PostgreSQL is selected, operational proof includes connection/backend-process cost, WAL growth, checkpoints, autovacuum, temp/sort spill, archive/log growth, restart/crash recovery, and disk-full behavior on the actual rack—not only SQL/RLS correctness.
- Core API, Admin API, and Worker may share the same authoritative central database because they are runtime hosts of the same modular-monolith business core, not independent microservices. Shared access must preserve explicit module/data ownership and the same invariants/transaction rules.
- If a future capability is extracted into a genuinely independent service, its authoritative data ownership becomes explicit; other services do not directly modify its private tables as a shortcut.
- Database schema changes account for supported old/new backend processes, skipped Workstations, pending sync, durable jobs/messages, and stored rule/workflow snapshots. Prefer additive expand-migrate-switch-contract evolution; destructive contraction waits for inventory/drain/compatibility evidence.
- Expected-version/optimistic concurrency is the ordinary edit contract. Database constraints protect uniqueness; stronger isolation/locks are selected narrowly for the actual invariant, with short transactions, stable lock ordering where relevant, and bounded whole-transaction retry only for classified deadlock/serialization conflicts.

## Consistency model

- SquiFlow does not choose one consistency model for the whole product.
- Current/strong authority is required where temporary disagreement can create unsafe business effects, including sensitive authorization, tenant isolation, shared stock/credit decisions, payment/refund authority, unique issued-document truth, expected-version state transitions, and strict hard-limit decisions.
- Eventual/derived consistency is acceptable for consequences such as notifications, telemetry, non-authoritative caches, read/search/report projections, and advisory usage projections when their freshness and rebuild behavior are explicit.
- An eventually updated projection declares its authoritative source, freshness/version evidence where material, duplicate/out-of-order handling, and reconciliation/rebuild path.
- Stale derived data must not silently become current authority for permissions, payment, stock, credit, hard limits, or another protected invariant.

## API, sync, and Worker correctness

- **REST/task-oriented HTTP is the v0.0.15 application API baseline, but SquiFlow does not claim strict REST purity.** Resource-oriented naming is the default; semantic command/action subresources remain valid for approvals, refunds, publications, reconciliations, and other material domain transitions.
- GraphQL and GraphQL Federation are deferred until a real client/query-composition requirement justifies their query-cost, authorization, caching, schema, and N+1 complexity.
- Retryable mutating operations use caller-provided semantic idempotency keys. A POST command is not automatically retry-safe; it becomes retry-safe only when its SquiFlow idempotency contract applies to the same intended business operation.
- Same idempotency key + changed intent is rejected.
- Where one store owns mutation + idempotency receipt + outbox, they commit atomically.
- Where one store also owns a strict consumption/limit decision for that semantic effect, prefer an atomic check-and-consume/commit design when practical.
- At-least-once delivery/redelivery is assumed; effects and usage records are idempotent or explicitly reconcilable according to their semantics.
- Retry is finite, classified, budgeted, and uses backoff/jitter/`Retry-After` where appropriate.
- Long-running HTTP work uses durable asynchronous status only when work is actually long-running; ordinary short business transactions remain synchronous.
- Conflict handling is aggregate-specific; no global last-write-wins policy.
- Large collection APIs are paginated/bounded. Connection pools are bounded/measured and must not leak tenant-scoped DB context across reused connections.
- Caching, response compression, and asynchronous telemetry logging are selective performance techniques, not default correctness mechanisms. Security/business audit and authoritative usage are not allowed to exist only in lossy async telemetry buffers.
- Any cache remains bounded, tenant-safe, disposable, and non-authoritative. Its design must define freshness/invalidation, stampede/miss amplification, outage bypass, cold-start behavior, and capacity; no Redis/Memcached/browser cache is mandatory.
- Rate limiting/admission is multi-dimensional where needed (IP/unauthenticated abuse, account/device, tenant, endpoint/work class, expensive provider action, platform admin, downstream budget). Authorization and throttling are separate decisions. Rate limiting can share policy concepts with resource limits, but rate-limit telemetry alone is not durable quota accounting where durable usage is required.
- Temporary HTTP throttling uses stable errors and `429`/`Retry-After` where applicable; clients back off rather than amplify overload.
- Communication inside the modular monolith is in-process by default. Do not create HTTP/gRPC between modules merely to imitate microservices.
- At real process/service boundaries, choose synchronous calls only when an immediate response is required; use durable asynchronous work for long-running/after-commit consequences. Avoid long synchronous service-call chains that multiply timeout/retry/failure obligations.
- **gRPC is a preferred candidate, not a pre-decided universal transport, for real process/service boundaries.** The first candidate areas are Workstation synchronization and future independently deployed synchronous service communication. Adopt it only when streaming, binary efficiency, generated contracts, or high-frequency RPC materially improve the implemented workload enough to justify the extra protocol/version/operations surface.
- Workstation sync correctness stays transport-independent. Phase 3 may compare the simpler HTTP baseline with gRPC before the first sync transport is finalized.
- Admin API and Core API remain independent normal runtime planes. Do not introduce an Admin API → Core API gRPC dependency merely because gRPC is available; if a future operation genuinely requires synchronous cross-process communication, evaluate gRPC under the same adoption gate.
- Detailed transport owner: `docs/api/TRANSPORT_SELECTION.md`.

## Network edge, deployment and protocol boundaries

- An edge reverse proxy/API-gateway capability may terminate TLS, route hostnames, enforce request-size/WAF/access policy, perform transport/protocol negotiation, and apply coarse rate limiting when the deployment needs it.
- Edge/gateway controls do not replace Core API or Admin API authentication, OpenFGA authorization, TenantContext isolation, domain validation, idempotency/concurrency, consistency, operation-specific admission, or authoritative usage/limit enforcement.
- Core API and Admin API remain independent backend/runtime planes even when one edge technology routes to both; Admin API does not route through Core API.
- A heavyweight API-management platform is not baseline merely because API gateways can perform analytics, transformation, version management, or authorization. Add only the edge capabilities SquiFlow actually needs.
- A service mesh is not baseline. Revisit only if independently deployed east-west service traffic becomes large/complex enough that mTLS, discovery, traffic policy, and distributed observability justify the added runtime/operational cost.
- Production external application traffic uses HTTPS/TLS. ZITADEL uses standards-based OIDC/OAuth over HTTPS.
- HTTP/1.1, HTTP/2, or HTTP/3 transport negotiation is an infrastructure/runtime concern; SquiFlow application semantics do not depend on one HTTP transport version.
- WebSocket/SignalR, if used, is for live UI/signal/wakeup behavior only. Durable business/sync/usage truth remains in DB/outbox/state records.
- SSH/private network access is infrastructure recovery/operations only, never a normal tenant business channel.
- DNS/hostname information assists routing but is never tenant authority by itself. Time synchronization is operationally important for TLS/tokens/leases/schedules/diagnostics, while business correctness still uses explicit versions/IDs where wall-clock ambiguity would be unsafe.
- gRPC is not the universal/default application protocol, but it is the preferred candidate to evaluate for real synchronous process/service boundaries under `docs/api/TRANSPORT_SELECTION.md`. MQTT, WebRTC, FTP/SFTP, and raw TCP/UDP each still require a concrete latency/streaming/device/transport/compatibility need before adoption.
- The initial paying-customer deployment must be reproducible from version-controlled deployment/infrastructure definitions and runbooks. Exact IaC/automation/container tooling is OPEN; normal tenant/platform application settings still belong in Web/Admin API rather than YAML-only administration.
- The initial release path promotes the same verified immutable artifact, applies compatible/preflighted migrations, runs health/smoke checks, and has explicit rollback/roll-forward and maintenance-window behavior. Blue-green/canary/progressive delivery requires enough topology, capacity, routing, compatibility, telemetry, and recovery evidence; it is not assumed for one rack node.
- Containerization is allowed when it improves repeatability/isolation, but Kubernetes is not baseline. Revisit Kubernetes only after concrete multi-node orchestration, rollout/reconciliation, discovery, failover/replacement, or scaling pain exceeds the simpler deployment approach.
- Scalability is finite and measured: each deployment profile has a capacity envelope and a known next move for the first-order bottlenecks. Do not add caching, replicas, sharding, extra nodes, or event-driven decomposition before the actual bottleneck is identified.

## Rules/workflow

- SquiFlow owns the bounded native rule model; arbitrary tenant C#/JS/SQL is not allowed.
- Rules/workflows are edited/published through Web administration and distributed as immutable/versioned compatible snapshots.
- Workstation local rule evaluation cannot turn stale server-owned facts into authoritative financial/stock/security/hard-limit decisions.
- Workflow is continuation-first and versioned.

## Practical business scope

- Do not force a universal `ready-made` versus `custom-design`, separate `social`, or one quotation-type category model. Tenant-visible kinds/categories exist only for real business distinctions while shared quotation revision/issued-history rules remain stable.
- Owner-authorized final pricing is supported inside permission/rule limits and historical issued values remain explainable rather than being recomputed from later settings.
- Outsourced print/production records only the actual external work/cost/payable; it does not invent supplier design work. Informal/phone supplier ordering and partial payment/outstanding payable are valid.
- Inventory can use precise quantity, availability-only, non-stock/service, and damaged/unusable adjustment modes as needed. Universal reservation, MRP/production planning, banner/roll wastage optimization, and universal lot/serial tracking are not baseline.

## Money/currency

- Currency is not hardcoded in application logic.
- A tenant has a configurable default currency code and monetary records that need historical meaning retain the applicable currency code.
- Exact rounding/tax precision is closed when the first affected production financial slice requires it; it is not guessed globally in advance.
- v0.0.15 does not add a multi-currency ledger, exchange-rate service, FX conversion engine, gain/loss accounting, or currency-provider abstraction.

## Object storage and backups

- The current bootstrap primary object store is a **private Hugging Face Storage Bucket**, with the currently available private-storage envelope of about **100 GB** treated as a real provider/account limit, not as a predetermined per-tenant allowance.
- Retained object bytes are a likely first authoritative consumption meter because provider/account capacity is real; exact tenant storage limits remain OPEN until configured/required.
- Because primary object storage is already planned to change at the first paying customer, a narrow **`IObjectStore`** provider boundary is baseline. `HuggingFaceObjectStore` is the bootstrap implementation; provider SDK types do not leak into business/domain contracts.
- The current bootstrap off-site backup target is a **private Kaggle Dataset** containing only encrypted opaque backup archives, never raw customer tables/files.
- Backup destination access uses a separate infrastructure-level **`IBackupTarget`** boundary. `KaggleBackupTarget` is the bootstrap implementation.
- Backup is an infrastructure recovery concern, not only an application feature: the recoverable set must include all state needed to reconstruct a usable SquiFlow deployment, including usage/limit state that affects enforcement, according to the selected deployment topology.
- Hugging Face/Kaggle are temporary. The first paying customer is the planned trigger to move to purpose-built paid primary/backup providers, or earlier if constraints demand it.
- Backups are not considered valid until download + integrity verification + restore has been proven.

## Operations and observability

- Current server hardware is lower-spec/desktop-class rack hardware; `stateless` does not imply automatic failover or zero downtime.
- Resource use is explicitly bounded; spare CPU/RAM is headroom rather than permission for caches/workers to grow without limit.
- Core API and Admin API have independent process/deployment health. A Core API outage must not automatically remove the Platform Admin application control surface; an Admin API outage must not block ordinary tenant business API work.
- OpenTelemetry/OTLP is the instrumentation boundary. New Relic + Aiven OpenSearch are current managed targets and Backtrace remains the crash-diagnostics direction.
- SquiFlow owns a provider-neutral `SquiFlow.Observability` boundary built on standard .NET/OpenTelemetry primitives; application/domain code does not depend on provider SDKs.
- `TraceId`, SquiFlow `CorrelationId`, and `CausationId` are distinct and are propagated deliberately across HTTP/sync/outbox/worker boundaries.
- Significant operational logs use a source-controlled stable `EventId`/`EventName`; abnormal/failure outcomes use stable `FailureCode` values. Exception/message text is evidence, not the stable failure identity.
- Sync/outbox/worker/Guard and similar operational state machines emit meaningful state-transition events.
- Workstation observability is **local-durable-first** with bounded rotating evidence and selective central export; server observability is **central-first** with bounded buffering/export.
- Workstation diagnostics must protect a disk reserve and shed low-value telemetry before threatening SQLite/OS/update recovery.
- Guard contributes bounded Workstation lifecycle/crash/resource evidence into diagnostics without becoming business authority.
- Tenant/user/workstation/entity/job/correlation/trace identifiers are not ordinary unbounded metric dimensions; instance-specific investigation belongs primarily in controlled logs/traces.
- Telemetry wall-clock timestamps are UTC and elapsed durations use monotonic timing; Workstation wall clock is not distributed ordering/idempotency authority.
- Telemetry-provider failure/quota exhaustion cannot block business transaction correctness.
- Operational telemetry is separate from authoritative security/business audit state.

Detailed owners:
- `docs/observability/OBSERVABILITY.md`
- `docs/observability/OBSERVABILITY_IMPLEMENTATION_CONTRACT.md`
- `docs/observability/STRUCTURED_LOGGING_AND_FAILURE_CODES.md`
- `docs/observability/WORKSTATION_SERVER_LOG_PIPELINE.md`
- `docs/observability/MULTI_TENANT_OBSERVABILITY.md`
- `docs/observability/OBSERVABILITY_VERIFICATION_ACCEPTANCE.md`.
- Production qualification includes proving the deployment can be recreated from the versioned deployment definitions/runbook on a replacement environment and identifying the next scaling action for the measured first-order bottlenecks.

## Application security

- Application security is layered across ZITADEL identity, OpenFGA/ASP.NET authorization, TenantContext/data isolation, domain/field rules, parameterized data access, browser output/forgery controls, bounded files/URLs, TLS/secrets, dependency/build controls, and edge/host defense in depth.
- Parameterized/ORM-bound data access is mandatory; untrusted values are never concatenated into SQL, and dynamic identifiers/operators use explicit allow-lists.
- Web output is encoded by default. Any permitted rich content is narrowly sanitized; cookie-backed mutations receive CSRF protection; exact CSP/security-header policy closes with the selected Web topology.
- Server-side outbound URLs use explicit SSRF destination/scheme/redirect/size/timeout controls. File names, templates, provider responses, and uploaded artwork/documents are untrusted inputs.
- If containers are used, final images are trusted/minimal/pinned, least privilege/non-root where practical, secret-free, reproducible, resource-bounded, and scanned. This does not preselect containers or Kubernetes.
- Security scanning is supporting evidence, not a substitute for tenant/resource/journey/provider/recovery tests. Detailed ownership is in `docs/security/APPLICATION_SECURITY_BASELINE.md`.

## Explicitly not baseline

- formal accessibility/a11y work as a separate v0.0.16 project/gate;
- full browser offline sync;
- generic repository/unit-of-work/one-interface-per-class abstractions;
- Kafka, mandatory Redis, event-sourced/full-CQRS/Saga core architecture;
- GraphQL/GraphQL Federation;
- service mesh;
- Kubernetes without a concrete cluster-orchestration requirement;
- API-management platform selected before a concrete need;
- HTTP/gRPC between ordinary modules;
- gRPC as a universal/default transport or an unmeasured Workstation/service dependency; it remains a preferred candidate only at real boundaries under the transport adoption gate;
- database-per-service rules applied to the current modular monolith;
- eventual-consistency-everywhere;
- denormalized authoritative core schema;
- global CRDTs;
- microservice-per-module design;
- per-tenant infrastructure by default;
- invented commercial plan names/prices/default allowances or a SaaS billing engine without a current product/commercial requirement;
- a giant generic metering/data-warehouse platform that records every user action without an enforcement/cost/capacity/support reason;
- advanced peripheral suite, MRP/wastage, specialized ETL/search, or SaaS billing engine without a current customer/commercial requirement;
- mandatory cache product, data lake, custom authentication/token system, or pattern-driven security microservice.
