# SquiFlow v0.0.16 — Master Implementation Plan

**Status:** Current audited architecture/implementation baseline.  
**Implementation state:** **pre-Phase-0**.

This file is the high-level source of truth. `docs/review/DECISION_AUDIT.md` records why decisions were kept, simplified, deferred, removed, or restored. Detailed semantics belong to focused owner documents.

---

## 1. Implementation principle: remove accidental complexity, not core capability

SquiFlow should not confuse minimal structure with minimal behavior.

Use this rule:

```text
real requirement / failure mode
→ preserve the full required behavior
→ choose the simplest structure that can own it correctly
→ test hostile/edge cases
→ measure
→ add further layers only when evidence earns them
```

A design is **not** better because it has fewer processes/interfaces if that simplification destroys recovery, provider replacement, authorization freshness, offline durability, retry safety, duplicate handling, observability evidence, platform-control independence, or other accepted responsibilities.

Likewise, a design is not better because it has more layers. Avoid forwarding-only Manager/Service/Helper/Repository hierarchies.

Code-quality/SOLID guidance is applied pragmatically:
- use meaningful business names;
- keep responsibilities cohesive;
- avoid magic policy/configuration values;
- prefer readable shallow control flow;
- explain `why`, not obvious mechanics;
- keep accepted interfaces narrow;
- do not create an interface/class/helper merely to satisfy a pattern slogan;
- tolerate small duplication when the alternative is a wrong shared abstraction.

Secondary architecture articles/diagrams are used to expose questions and trade-offs. Exact high-impact security/database/protocol/provider claims should be checked against primary specifications, official provider documentation, or foundational papers when available before being locked into the architecture.

---

## 2. Runtime shape

Accepted early runtime direction:

```text
apps/web                  Blazor Web App — tenant business Web + tenant Owner Settings
apps/desktop/workstation  Avalonia — Windows Workstation
apps/desktop/guard        Workstation supervision/recovery companion
services/core-api         ASP.NET Core tenant/business HTTP/composition host
```

Create later when their first real feature exists:

```text
apps/admin-web            Blazor Web App — SquiFlow platform/super-admin UI
services/admin-api        ASP.NET Core — independent platform/super-admin backend
services/worker           durable background execution
```

Business capability code remains a modular monolith.

Inside a runtime host, business modules communicate **in-process**. Do not create HTTP/gRPC calls between modules merely to imitate microservices. Network communication is reserved for real process/service/provider boundaries.

### Platform Admin backend separation

Platform/super-admin control is a separate runtime/security/availability plane:

```text
apps/admin-web
→ services/admin-api
```

`services/admin-api` is not a route group inside Core API and does not use Core API as its normal downstream execution dependency.

Normal platform administration must not require:

```text
Admin Web → Admin API → Core API
```

Core API and Admin API may share reviewed libraries/modules and may intentionally share underlying infrastructure. The requirement is independent backend process/API ownership, deployment, authentication/authorization scope, health and restart lifecycle.

A Core API outage must not automatically remove the Platform Admin application control surface. An Admin API outage must not block ordinary tenant business API work. Shared database/provider outages may still affect both where the requested operation depends on that shared infrastructure.

Owner: `docs/admin/ADMIN_SURFACES.md` and `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`.

### Guard

`SquiFlow.Guard` is accepted because supervision/recovery must survive or observe Workstation failure from outside the Workstation process.

Guard owns:
- launch/supervision;
- bounded crash/hang recovery;
- update handoff/recovery;
- child/helper cleanup;
- bounded lifecycle/crash/resource evidence;
- safe-mode/restart-budget behavior.

Guard does **not** own business rules, OpenFGA authorization, sync semantics, central DB access, or Worker/platform duties.

Owner: `docs/workstation/GUARD_AND_RECOVERY.md`.

Repository boundary owner: `docs/architecture/REPOSITORY_STRUCTURE.md`.

---

## 3. Small-team tenant/control model

Normal tenant:

```text
Owner
└── Staff
```

Owner/Staff are default templates, not hardcoded product roles.

Tenant Owner controls ordinary roles/permissions inside SquiFlow's entitlement/security ceiling.

Web-only tenant control-plane operations include:
- team/invitations;
- role/permission assignment;
- tenant rule/workflow/form publication;
- custom domains/branding;
- device/workstation settings where implemented.

Desktop consumes published permissions/config but never grants them.

Tenant administration remains in the normal tenant Web and uses Core API tenant-admin operations. Platform-critical application controls belong to **Platform Admin Web → Admin API**, not Core API, when that slice exists. If the application control plane itself is unavailable, infrastructure recovery uses a private runbook rather than a hidden business/Desktop endpoint.

Owners:
- `docs/admin/ADMIN_SURFACES.md`
- `docs/security/TENANT_PERMISSIONS.md`
- `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`.

---

## 4. Identity: ZITADEL

**ZITADEL is selected as the SquiFlow identity/authentication platform.**

Stable external identity:

```text
(issuer, subject)
```

Workstation login:

```text
system browser
→ ZITADEL OIDC Authorization Code
→ PKCE S256
→ validated native callback
→ SquiFlow account/device/session context
```

No reusable native client secret and no central DB credentials on the Workstation.

ZITADEL provides authentication/account/session/MFA/SSO capability; it does not become current business authorization truth simply because it can expose roles/claims.

Platform operators also authenticate through ZITADEL, but Admin API requires separate platform-level authorization and never treats a tenant session/role as super-admin authority.

SquiFlow does not create a second JWT/PASETO/password/WebAuthn subsystem around ZITADEL. Token/session form is an identity/session concern and never replaces current OpenFGA authorization or TenantContext isolation.

Open Phase-1 details include ZITADEL Cloud vs self-hosted, exact instance/project/application layout, tenant-organization mapping, Web session topology, and native callback choice.

Owner: `docs/security/IDENTITY_AND_SESSIONS.md`.

---

## 5. Application authorization: OpenFGA + ASP.NET Core integration

**OpenFGA is selected as the SquiFlow application-authorization engine.**

Responsibilities:

```text
ZITADEL
  authentication / account identity / MFA / SSO

OpenFGA
  roles / tenant-defined custom roles / assignments / resource relationships

ASP.NET Core IAuthorizationService
  host integration point for semantic requirements

SquiFlow domain/workflow
  canonical state, business invariants, calculations, transitions

Database isolation
  tenant-scoped data access / provider defense in depth
```

Core API uses tenant/business policy scope. Admin API uses separate platform/super-admin policy scope. Tenant authority can never imply platform authority.

Tenant-created custom roles are tuple/data changes, not a new OpenFGA model deployment per role. Stable SquiFlow permission relations live in a versioned authorization model, and production checks pin an explicit model ID.

Web-only role/grant changes use a durable, reconcilable change path because OpenFGA and the SquiFlow DB are separate systems. Do not report a grant/revocation as applied until the OpenFGA outcome is known/applied.

`TenantAuthorizationRevision` remains SquiFlow-visible snapshot/audit/invalidation evidence. It complements OpenFGA model/tuple state rather than replacing it.

Owner: `docs/security/TENANT_PERMISSIONS.md`.

---

## 6. Web versus Workstation

### Web

Web is **online-only for business operations in v0.0.16**.

Do not implement:
- IndexedDB business replica;
- service-worker business sync;
- browser offline mutation/conflict engine.

Selected valuable forms may use explicit online server-side drafts/autosave.

Owner: `docs/web/WEB_RUNTIME_AND_STORAGE.md`.

### Workstation

Workstation is the local-first/offline client.

For an explicitly local-capable command:

```text
user action
→ local validation
→ one durable local transaction
     business state + outbox
→ immediate local result
→ background sync
→ server authoritative accept/conflict/reject
```

Important states:

```text
LocalCommitted
PendingRemote
Authoritative
Conflict
Rejected
AuthorizationChanged
UpgradeRequired
```

No global CRDT/peer-authority model.

This explicit authority model is intentionally more precise than saying the Workstation is merely “eventually consistent.” Pending local work is never presented as globally authoritative.

Guard must not compromise or rewrite this durable local-first model; it supervises process lifecycle around it.

Owner: `docs/workstation/LOCAL_FIRST_DESKTOP.md`.

---

## 7. Multi-tenancy isolation

Ordinary tenants use pooled isolation:

```text
authoritative TenantContext
→ shared central schema/model
→ tenant discriminator on tenant-owned data
→ provider-specific defense in depth
```

Authentication, OpenFGA authorization, and tenant data isolation are separate concerns.

Pooled data does not mean unbounded pooled compute. Expensive reports, documents, jobs, provider calls and future Worker concurrency are tenant/work-class aware where required so one tenant cannot consume the entire system.

Do not prebuild schema-per-tenant, DB-per-tenant, queue-per-tenant or deployment-per-tenant.

If PostgreSQL is used, prove RLS defense in depth, safe runtime roles and safe tenant context under connection pooling. This does not silently select PostgreSQL.

Owner: `docs/architecture/MULTI_TENANCY_ISOLATION.md`.

---

## 8. Command/query, REST API, idempotency, retry, and rate limiting

SquiFlow adopts **command/query responsibility separation** without assuming full CQRS infrastructure.

```text
Command
→ business intent, possible mutation

Query
→ read only, no business mutation
```

Material actions remain task-oriented (`ApproveQuote`, `RefundPayment`, `AdjustInventory`). Separate read/write databases, event sourcing, or command/query microservices are added only if an implemented workload proves they are worth the extra consistency/operations contract.

**REST/task-oriented HTTP is the v0.0.15 API baseline, but SquiFlow does not claim strict REST purity.** Resource-oriented noun paths are the default for ordinary resources; semantic action subresources remain valid when business intent is clearer than generic CRUD.

GraphQL/GraphQL Federation are deferred until a real client query-composition problem justifies query-cost, field-authorization, caching, schema, and N+1 complexity.

gRPC is also non-baseline. Revisit only if a real Workstation sync or extracted-service workload proves streaming, binary efficiency, generated contracts, or another transport property materially valuable enough to justify the extra protocol/version/operations surface.

Retryable mutations use caller-provided semantic idempotency keys.

```text
same key + same intent      → same semantic result
same key + changed intent   → reject
```

A POST is not automatically retry-safe. It becomes retry-safe for the same intended business operation only when its SquiFlow semantic-idempotency contract applies.

When one store owns business mutation, idempotency receipt and outbox, commit them atomically.

Duplicate handling is end-to-end: caller/producer retry, transport redelivery, and consumer/effect replay are distinct failure points. Do not treat one broker or one dedupe table as system-wide exactly-once.

Retry is finite/classified/budgeted, with an intentional retry owner for each remote dependency path so nested retries do not multiply blindly.

Rate limiting/admission is also explicit and can vary by unauthenticated source, account/device, tenant, endpoint/work class, expensive provider action, Admin API operation, and downstream provider budget. Authorization and throttling are separate decisions. Temporary HTTP throttling uses stable errors and `429`/`Retry-After` where applicable.

API version compatibility is mandatory because Workstations can skip releases. Exact URI/header/media-type version mechanics are chosen when the first compatibility slice is implemented; old supported contracts must not be silently reinterpreted as new semantics.

HTTP status and stable SquiFlow Problem Details codes remain consistent across controllers so unauthenticated, forbidden, validation, conflict/precondition, rate, dependency, and internal failures are distinguishable without leaking internals.

Long-running work uses durable async status only when genuinely long-running; ordinary short transactions stay synchronous.

Owner: `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`.

---

## 9. Synchronization

Workstation durable outbox is local upload truth; in-memory signaling only wakes work.

```text
bounded pending items
→ authenticated Sync API
→ authoritative tenant + OpenFGA permission + business/rule validation
→ idempotency + concurrency/conflict
→ central transaction
→ per-item result
→ durable local acknowledgement
```

Remote changes + cursor advancement commit together locally.

Conflict behavior is aggregate-specific. Long-offline recovery preserves local pending intent and may require upgrade/resnapshot/rebase/review.

Owner: `docs/sync/SYNC_AND_AUTHORITY.md`.

---

## 10. Persistence — normalized authority, measured workload, explicit data ownership

Exact DB products remain open until phase POCs:
- PostgreSQL — strongest central reference candidate;
- SQLite + WAL — mature Workstation candidate;
- libSQL — explicit Workstation candidate.

Do **not** create generic `IRepository<T>`, `IUnitOfWork`, or one-interface-per-provider hierarchies solely to appear portable.

Provider-specific DB code stays contained outside business/domain code. Extract interfaces only when an actual dependency/replacement boundary requires them.

Authoritative relational business state starts normalized around real identities/relationships/constraints. Core business invariants are not hidden in arbitrary JSON/EAV or tenant-specific DDL simply to avoid schema work.

Denormalized/materialized read structures are derived optimizations. Each declares source, freshness, update/rebuild behavior, tenant/authorization scope, and what happens when stale/unavailable.

Indexes are workload contracts, not decorations. Measure real query plans/cardinality **and** write/WAL/storage/migration/sync/import cost; do not “index every filterable field.”

Before selecting/tuning the central DB, record the actual implemented workload shape: read/write/delete mix, representative item sizes, tenant/data skew, normal concurrency, offline reconnect/sync/import bursts, consistency requirements, hot-query cardinality, and the initial HA/geographic assumptions.

If PostgreSQL wins Phase 3, qualification includes connection/backend-process resource cost, WAL/checkpoints, autovacuum, temp/sort spill, archive/log/disk growth, and crash/restart recovery on the low-resource rack—not just SQL/RLS correctness.

Consistency is selected per invariant:
- strong/current authority where temporary disagreement could create an unsafe business/security effect;
- eventual/derived freshness only where staleness is acceptable and visible/reconcilable.

Core API, Admin API, and Worker may intentionally share the same authoritative database because they are runtime hosts of the same modular-monolith business core. They must preserve explicit module/data ownership and the same invariants/transaction rules without making Admin API call Core API as a proxy or bypassing module rules with ad-hoc SQL.

If a future capability is extracted into a truly independent service, authoritative data ownership and the data-sharing contract become explicit at that point; other services do not directly modify its private tables.

Expected-version/optimistic concurrency is the ordinary edit contract. Database constraints protect uniqueness; stronger isolation/locks are narrowly selected for the actual invariant. Transactions remain short and use stable lock ordering where applicable. Classified deadlock/serialization conflicts may retry the whole transaction with a bounded budget under the existing idempotency contract.

Schema evolution accounts for old/new backend processes, skipped Workstations, pending sync, durable jobs/messages, stored idempotency results, and versioned rule/workflow/form snapshots. Prefer expand-migrate-switch-contract changes; remove obsolete shape only after compatibility/inventory/drain evidence and an explicit rollback/roll-forward plan.

Owner: `docs/data/PERSISTENCE_SELECTION.md`.

---

## 11. Rules/workflow/forms

SquiFlow owns the bounded tenant-safe rule architecture:
- typed facts;
- bounded structured rule representation;
- no arbitrary tenant C#/JS/SQL;
- validation/complexity limits;
- immutable versioned publication;
- deterministic evaluation;
- bounded decision trace;
- compatible server/Workstation snapshots.

OpenFGA decides whether an actor has the required relationship/permission. It does not replace workflow/domain transition validity.

Phase 5 proves one real rule + one workflow + one bounded dynamic form, not a general BPM/form platform.

Owners:
- `docs/rules/NATIVE_RULE_ENGINE.md`
- `docs/workflow/WORKFLOW_DESIGN.md`.

---

## 12. Practical business scope

Core model:

```text
Party
→ Commercial Relationship / Account
→ Business Context
→ Transaction
→ Workflow
→ Settlement
```

Support practical walk-in/registered/organization/program/credit scenarios, quotations/tenders, purchasing/suppliers, fulfillment, payments/corrections and pragmatic inventory.

Do not baseline MRP, universal reservation, complex banner-roll wastage, universal lot/serial tracking, or complex procurement workflow.

Do not force `ready-made` versus `custom-design`, a separate `social` category, or one universal quotation-type list. Owner-authorized final pricing, outsourced-print-only work, informal/phone supplier ordering, partial supplier payables, and damaged/unusable stock adjustment remain expressible without introducing manufacturing/MRP machinery.

### Currency — minimal requirement

Currency must not be hardcoded.

```text
Tenant.DefaultCurrencyCode
monetary record retains CurrencyCode where historical meaning requires it
```

No exchange-rate provider/FX/multi-currency accounting subsystem until a real customer needs it.

Owners:
- `docs/domain/BUSINESS_MODEL.md`
- `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`.

---

## 13. Files and bootstrap storage

### Primary business object storage

Current bootstrap provider:

```text
IObjectStore
└── HuggingFaceObjectStore
```

The private Hugging Face Storage Bucket currently provides approximately 100 GB of private-storage capacity.

The interface is justified now because provider replacement at the first paying customer is already planned. It uses SquiFlow-owned object keys/streams/hash metadata and does not leak Hugging Face SDK types into business code.

### Backup destination

Current bootstrap backup destination:

```text
IBackupTarget
└── KaggleBackupTarget
```

`IBackupTarget` is an **infrastructure/operations** boundary, not a business-domain service.

Kaggle receives only locally packaged/encrypted opaque backup artifacts.

Backup scope is broader than application tables: recovery must include all state/configuration/evidence required to reconstruct a usable deployment, according to whether dependencies such as ZITADEL/OpenFGA are managed or self-hosted.

### Migration trigger

At the first paying customer, or earlier if constraints demand it:

```text
implement paid IObjectStore adapter
+ implement paid IBackupTarget adapter
→ migrate/copy/verify
→ switch configuration
→ restore test
```

Owner: `docs/data/FILES_AND_OBJECT_STORAGE.md`.

---

## 14. Printing and physical devices

Printing is a Workstation device side effect.

```text
committed business document
→ print request
→ Windows printer/spooler
→ success/failure/unknown physical output
```

Print failure does not undo committed business truth.

If a future driver/native component needs a helper process, Guard supervises its lifecycle; the helper still has a narrow contract and no broad business authority.

---

## 15. Worker, events, messaging, and service communication

A separate Worker executable is created in Phase 6 when the first durable background workload exists.

Background work may be:
- user-triggered consequence;
- scheduled occurrence;
- external-system-triggered;
- batch/volume-triggered;
- platform-control work.

A schedule is a trigger, not the durable business truth. Important scheduled work becomes a durable occurrence/job before execution.

Keep message semantics explicit:

```text
Command / Job
= instruction with an execution owner

Event
= fact that already happened
```

Communication-pattern selection:

```text
inside one host/module composition → in-process call
immediate authoritative answer across a real boundary → synchronous request/response
one durable task → queue/job semantics
many independent consumers of one fact → pub/sub or multiple outbox deliveries
replay/history/independent offsets required → event stream, only when proven
```

Avoid long synchronous service-call chains. Every network hop creates timeout, retry, partial-failure, versioning, authorization, and observability obligations.

The transactional outbox is the normal bridge from an authoritative commit to later consequences. Do not make hidden event choreography the primary correctness owner for payments, stock, permissions, or other protected transitions.

Eventual consistency is allowed only for consequences/projections whose temporary staleness is safe. Each such consumer defines source/version, duplicate/out-of-order behavior, freshness evidence where material, and reconciliation/rebuild path.

Worker requirements include:
- bounded concurrency;
- durable claims/leases where needed;
- idempotent/reconcilable effects;
- retry classification/backoff;
- no-progress handling;
- pause/drain/recovery;
- `OutcomeUnknown` for ambiguous external effects.

Platform Admin Web sends privileged Worker/control commands to **Admin API**, which persists/authorizes the exact command before Worker/system execution. Core API is not the platform-control proxy.

No Kafka/event-stream infrastructure or generic pub/sub broker is baseline merely because those patterns exist.

Notifications/webhooks use Core API/outbox/Worker boundaries first; no notification microservice baseline.

Owners:
- `docs/server/CORE_API_AND_WORKER.md`
- `docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md`.

---

## 16. Edge, deployment reproducibility, scalability, protocols, observability, and physical operations

A deployment edge/reverse proxy/API-gateway capability may terminate TLS, route hostnames, enforce request-size/WAF/private-access policy, negotiate supported HTTP transport, and apply coarse rate limiting.

That edge never becomes business authority and must preserve direct backend separation:

```text
edge
├── tenant/business → Core API
└── platform/admin → Admin API
```

not:

```text
edge → Core API → Admin API
```

Do not adopt a heavyweight API-management product merely because gateways can also perform transformation, analytics, version management, or authorization. Add only capabilities that solve the actual deployment problem.

Current external protocol baseline:
- HTTPS/TLS for Web/Core API/Admin API/Workstation sync;
- OIDC/OAuth over HTTPS for ZITADEL;
- HTTP/1.1/2/3 negotiation is infrastructure/runtime detail rather than business semantics;
- WebSocket/SignalR, if used, is live signaling only, never durable truth;
- DNS/hostname assists routing but never proves tenant authority;
- SSH/private network access is infrastructure recovery/operations only.

Time synchronization matters for TLS/tokens/leases/schedules/diagnostics, but business correctness uses explicit versions/IDs where wall-clock ambiguity would be unsafe.

The paying-customer deployment must be reproducible from version-controlled deployment/infrastructure definitions and runbooks. This does not turn normal tenant/platform application administration into YAML editing. Exact IaC/automation/container packaging remains OPEN until the deployment slice needs it.

The first production release path promotes one immutable verified artifact, keeps secrets/configuration outside it, preflights compatible migrations/capacity/dependencies, drains where required, evaluates process health plus an authorized smoke journey, and states rollback/roll-forward/maintenance recovery honestly. A single node does not claim zero downtime; blue-green/canary/progressive delivery requires real spare topology, routing, compatibility, telemetry, and recovery evidence.

Containerization is allowed when useful for packaging/isolation. Kubernetes is not baseline; revisit only after concrete multi-node placement/reconciliation, rollout, discovery, failover/replacement, or scaling operations become a real recurring problem that the simpler deployment cannot own reliably.

If containers are chosen, final images are trusted/minimal/version-or-digest-pinned, reproducible/scanned, secret-free, least privilege/non-root where practical, and bounded for ports/capabilities/writable storage/resources/logging. This is conditional hardening, not a container selection.

SquiFlow does not claim infinite scalability. Each deployment profile needs a measured capacity envelope, first-order bottlenecks, and a known next move for those bottlenecks. Diagnose the constraint before adding caching, replicas, extra nodes, sharding, distributed databases, or service extraction.

Do not add gRPC, MQTT, WebRTC, FTP/SFTP, or raw TCP/UDP without a concrete latency/streaming/device/compatibility requirement.

A service mesh is **not baseline**. Revisit only after real independently deployed east-west service traffic proves enough mTLS/discovery/traffic-policy/observability complexity to justify the runtime and operational cost.

OpenTelemetry/OTLP is the provider-neutral telemetry boundary. SquiFlow owns a small `SquiFlow.Observability` implementation boundary over standard .NET/OpenTelemetry primitives; business/domain code does not depend directly on observability-provider SDKs.

Current managed targets:
- New Relic — metrics/traces/APM;
- Aiven OpenSearch — structured operational logs;
- Backtrace — crash diagnostics direction.

Important request/job/sync paths propagate verified tenant/security context plus the correlation data that materially applies: `CorrelationId`, `CausationId`, trace context, operation/idempotency keys, job/message/sync identifiers, and rule/config versions. `TraceId`, `CorrelationId`, and `CausationId` are distinct concepts.

Significant operational events use source-controlled stable `EventId` and `EventName` values; abnormal/failure outcomes use stable `FailureCode` values. Exception/message strings are evidence, not the stable failure identity. Sync/outbox/Worker/Guard and similar operational state machines log meaningful state transitions.

Runtime behavior differs by environment:

```text
Server      → central-first, bounded export/buffering
Workstation → local-durable-first diagnostics + selective central export
```

Workstation logging/diagnostic bundles are bounded and preserve a disk reserve so diagnostics cannot endanger SQLite, OS operation, or update/recovery. Guard remains locally useful when remote observability is unavailable.

Telemetry is another tenant/privacy data path. Verified opaque tenant identity may be used where useful/safe in logs/traces, but TenantId/UserId/WorkstationId/entity/job/correlation/trace IDs are not ordinary unbounded metric dimensions. Central redaction/secret protection is tested before external export where feasible.

Telemetry failure cannot invalidate business transactions. Buffers, retry and local spools are bounded. The telemetry pipeline itself exposes useful self-health such as dropped signals, queue/spool pressure, exporter failures, sampling ratios, processor failures and quota state where available. Alerting should group/deduplicate/rate-limit crash loops and shared outages.

Telemetry wall-clock timestamps use UTC and elapsed durations use monotonic timing. Workstation wall clock is not distributed ordering/idempotency authority.

For important incidents, diagnostic output should distinguish observed symptom, evidence, probable failure class, unknowns/confidence, and safe retry/recovery/reconciliation. Telemetry must not auto-correct ambiguous money/stock/security state.

Security/business history required for correctness remains in durable SquiFlow state. Operational telemetry may use bounded asynchronous buffering/export, but logs/traces or another lossy buffer are not the only audit copy.

Guard supplies bounded desktop lifecycle/crash/resource evidence into the support/diagnostic path.

Core API and Admin API have independent health/readiness/deployment lifecycles. Their telemetry can correlate through shared IDs, but one backend's process failure must not be interpreted as the other backend being down.

Current server hardware is lower-spec/desktop-class rack equipment. `Stateless` means process memory is not authoritative; it does not promise automatic failover.

Owners:
- `docs/observability/OBSERVABILITY.md`
- `docs/observability/OBSERVABILITY_IMPLEMENTATION_CONTRACT.md`
- `docs/observability/STRUCTURED_LOGGING_AND_FAILURE_CODES.md`
- `docs/observability/WORKSTATION_SERVER_LOG_PIPELINE.md`
- `docs/observability/MULTI_TENANT_OBSERVABILITY.md`
- `docs/observability/OBSERVABILITY_VERIFICATION_ACCEPTANCE.md`
- `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

Review/non-accepted follow-ups are tracked in `docs/review/OBSERVABILITY_AND_OVERLOOKED_RECOMMENDATIONS.md` rather than silently promoted into product requirements.

---

## 17. API performance discipline

Common performance techniques are allowed only with explicit correctness/resource limits:
- server-bounded pagination for large collections;
- bounded DB connection pooling;
- selective caching with a freshness contract;
- compression only for suitably large compressible payloads;
- bounded asynchronous telemetry export;
- measured query/index optimization.

A future cache also defines authority/bypass, tenant/permission/configuration-safe keys, TTL/invalidation, memory/entry bounds, stampede/miss amplification, cold-start, outage, privacy, and repopulation behavior. Cache failure may slow the system but cannot grant access, lose truth, or create unbounded load.

A performance optimization that makes a stale permission/payment/stock/credit answer look current is a correctness regression, not an optimization.

Measure representative latency percentiles, throughput, query/dependency time, allocation/memory pressure, payload sizes, and pool wait before adding another caching/proxy/read-model layer.

Application security is separately owned by `docs/security/APPLICATION_SECURITY_BASELINE.md`: encoded/sanitized Web output, CSRF protection for cookie-backed mutations, parameterized SQL and allow-listed dynamic query parts, field contracts, bounded files/templates, SSRF controls, TLS/secrets, dependency/build security, and conditional container hardening complement rather than duplicate ZITADEL/OpenFGA/tenant isolation.

---

## 18. Verification

Keep tests focused on real correctness risks:
- business/domain invariants;
- real DB transaction/concurrency/isolation behavior;
- explicit DB workload profile including reconnect/import bursts;
- PostgreSQL WAL/checkpoint/autovacuum/temp/disk behavior if PostgreSQL is selected;
- schema/index/query-plan behavior at representative and projected cardinalities;
- expected-version/constraint/isolation/lock/deadlock/serialization behavior and bounded whole-transaction retry;
- expand/backfill/switch/contract schema evolution across supported old/new readers, writers, pending sync and durable work;
- Workstation local durability/restart;
- Guard independent crash/hang/update recovery;
- ZITADEL authentication/session flows;
- OpenFGA model/tuple/custom-role/consistency/reconciliation behavior;
- tenant/Core API authorization;
- **platform/Admin API authorization and Core-API-outage independence**;
- idempotency/response loss across caller/transport/consumer boundaries;
- retry amplification and retry-budget exhaustion;
- rate/admission behavior and `Retry-After` client backoff;
- sync protocol/version compatibility and long-offline recovery;
- derived-projection duplicate/out-of-order/staleness/rebuild behavior where implemented;
- edge routes Core/Admin directly without collapsing authorization or backend ownership;
- WebSocket/live-signal loss does not destroy durable business progress;
- SQL/identifier injection, XSS, CSRF, mass assignment, SSRF, malicious file/template and secret-leak paths for implemented surfaces;
- cache outage/cold-start/stampede/stale-sensitive/cross-tenant behavior when a cache exists;
- DNS/hostname/clock-skew hostile cases where relevant;
- `IObjectStore` provider contract + Hugging Face adapter;
- `IBackupTarget` contract + encrypted Kaggle backup restore;
- clean/replacement-environment rebuild from deployment definitions/runbook;
- immutable-artifact promotion, migration preflight, smoke verification and failed-release rollback/roll-forward/maintenance recovery;
- actual low-end hardware/resource/capacity limits;
- observability correlation across representative Workstation/API/authorization/DB/outbox paths;
- stable EventId/EventName/FailureCode uniqueness/compatibility;
- redaction/privacy and multi-tenant telemetry isolation;
- metric-cardinality behavior under many tenants/entities;
- collector/provider outage and queue/spool saturation;
- Workstation offline logging/disk-pressure/diagnostic-bundle behavior;
- alert-storm grouping/deduplication/rate controls;
- observability resource-overhead regression.

Do not introduce unrelated interfaces merely to increase mock/unit-test count.

Owners:
- `docs/testing/VERIFICATION_STRATEGY.md`
- `docs/observability/OBSERVABILITY_VERIFICATION_ACCEPTANCE.md`.

---

## 19. Sequential implementation plan

Default WIP limit: **one implementation phase**, but each phase must cover its defined edge/failure behavior before being called complete.

```text
Phase 0  Web + Workstation + Guard + Core API skeleton, provider contracts, minimal CI, initial reproducible deploy representation
Phase 1  ZITADEL identity + OpenFGA Owner/Staff/custom-role authorization
Phase 2  first local-first Workstation Customer/Order transaction + local DB + Guard recovery
Phase 3  authoritative sync + central DB + workload/schema/index/consistency + API-version proof + pooled tenant isolation + idempotency
Phase 4  conflict/long-offline/resnapshot recovery
Phase 5  one native rule + workflow + bounded dynamic form
Phase 6  create Worker + Platform Admin Web + independent Admin API; prove first platform-control and durable-work slice
Phase 7  Hugging Face IObjectStore flow + documents/printing + Kaggle IBackupTarget restore proof
Phase 8  API/rate/network/performance/observability/admin hardening, including stable event registry and telemetry failure tests
Phase 9  payments/credit/inventory/correction hardening
Phase 10 actual-rack release/resource/restore/rebuild/scaling qualification + paid-provider migration readiness
```

Owner: `docs/implementation/PHASES_AND_GATES.md`.

---

## 20. Explicit non-baseline work

Do not add these now:
- dedicated accessibility/a11y workstream or conformance program;
- generic repository/unit-of-work/one-interface-per-class hierarchy;
- full browser offline/PWA sync;
- Kafka/event-log infrastructure;
- generic pub/sub/event-bus platform before a real multi-consumer need;
- mandatory Redis;
- microservice-per-module architecture;
- full CQRS/event sourcing/Saga core architecture;
- event-driven-everything;
- eventual-consistency-everywhere;
- global CRDTs;
- GraphQL/GraphQL Federation;
- service mesh;
- Kubernetes before a real cluster-orchestration problem;
- API-management platform selected before need;
- HTTP/gRPC between ordinary modules;
- gRPC sync without measured transport/contract need;
- database-per-service rules applied to the current modular monolith;
- MQTT/WebRTC/FTP/SFTP/raw TCP/UDP without a concrete workload;
- custom Raft/consistent-hashing/Merkle-repair/distributed-database machinery;
- Operational Transformation without a real collaborative-editing requirement;
- denormalized authoritative core schema;
- schema/database/deployment per tenant baseline;
- multi-currency/FX subsystem;
- advanced peripheral suite;
- MRP/wastage;
- generic social/catalog categories or universal quotation-type taxonomy not required by the business;
- data lake/streaming platform or custom identity/token subsystem;
- generic ETL/search/SaaS billing infrastructure without a real requirement;
- a dedicated observability microservice merely to route telemetry;
- 100% production tracing by default;
- hundreds of placeholder files/projects.

The accepted `IObjectStore`, `IBackupTarget`, Guard, ZITADEL, OpenFGA, `SquiFlow.Observability`, and separate Admin API boundaries are **not** examples of forbidden complexity: each has a concrete current or committed near-term responsibility.

---

## 21. Implementation-complete rule

A capability is complete when the concerns that materially apply to that capability are proven: user states/recovery, tenant/authority, validation/permission, transaction/idempotency/concurrency, consistency/freshness, local-vs-server authority, async/external-unknown behavior, API/protocol version compatibility, resource/storage/rate limits, observability/evidence, upgrade/restore implications, and relevant hostile tests.

For platform administration this additionally includes proving that super-admin control uses Admin API directly and remains process-independent from Core API for the implemented operation.

For deployment qualification this also includes reproducibility: a usable replacement environment must be recoverable from versioned deployment definitions/runbooks plus the verified backup/recovery set, not undocumented machine state.

Do not force irrelevant checklist items onto tiny features, but do not waive required edge cases simply to keep the implementation visually minimal.
