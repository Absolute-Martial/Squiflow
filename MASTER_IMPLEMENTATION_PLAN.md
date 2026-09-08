# SquiFlow v0.0.15 — Master Implementation Plan

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

A design is **not** better because it has fewer processes/interfaces if that simplification destroys recovery, provider replacement, authorization freshness, offline durability, retry safety, duplicate handling, or other accepted responsibilities.

Likewise, a design is not better because it has more layers. Avoid forwarding-only Manager/Service/Helper/Repository hierarchies.

---

## 2. Runtime shape

Accepted early runtime direction:

```text
apps/web                  Blazor Web App — tenant business Web + tenant Owner Settings
apps/desktop/workstation  Avalonia — Windows Workstation
apps/desktop/guard        Workstation supervision/recovery companion
services/core-api         ASP.NET Core HTTP/composition host
```

Create later when their first real feature exists:

```text
apps/admin-web            Blazor Web App — SquiFlow platform control plane
services/worker           durable background execution
```

Business capability code remains a modular monolith.

Containerization may be used for server deployment where it helps packaging/operations, but container design patterns are not permission to create extra services, proxies, sidecars, leaders, or fan-out machinery without a concrete deployment/coordination problem.

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

Platform-critical application controls belong to the separate Platform Admin Web when that slice exists. If the application control plane itself is unavailable, infrastructure recovery uses a private runbook rather than a hidden business/Desktop endpoint.

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

ZITADEL provides authentication/account/session/MFA/passkey/SSO/federation capability according to configuration. SquiFlow does not build a competing password/OTP/MFA/passkey stack; it owns OIDC integration, application session/device/tenant binding, step-up requirements, and post-authentication authorization.

ZITADEL does not become current business authorization truth simply because it can expose roles/claims.

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
  application integration point for semantic requirements

SquiFlow domain/workflow
  canonical state, business invariants, calculations, transitions

Database isolation
  tenant-scoped data access / provider defense in depth
```

These layers are deliberately separate.

Tenant-created custom roles are tuple/data changes, not a new OpenFGA model deployment per role. Stable SquiFlow permission relations live in a versioned authorization model, and production checks pin an explicit model ID.

Web-only role/grant changes use a durable, reconcilable change path because OpenFGA and the SquiFlow DB are separate systems. Do not report a grant/revocation as applied until the OpenFGA outcome is known/applied.

`TenantAuthorizationRevision` remains SquiFlow-visible snapshot/audit/invalidation evidence. It complements OpenFGA model/tuple state rather than replacing it.

Owner: `docs/security/TENANT_PERMISSIONS.md`.

---

## 6. Web versus Workstation

### Web

Web is **online-only for business operations in v0.0.15**.

Do not implement:
- IndexedDB business replica;
- service-worker business sync;
- browser offline mutation/conflict engine.

Selected valuable forms may use explicit online server-side drafts/autosave.

Blazor Web App does not make all Web state stateless. If Interactive Server rendering is used, per-user circuit state can live in server memory. That state is transient runtime/UI state, not authoritative business state. Exact render-mode/circuit/session-affinity/distributed-state behavior is a Phase-1 decision before multi-node failover claims.

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

## 8. Command/query, API security, idempotency, and retry

SquiFlow adopts **command/query responsibility separation** without assuming full CQRS infrastructure.

```text
Command
→ business intent, possible mutation

Query
→ read only, no business mutation
```

Material actions remain task-oriented (`ApproveQuote`, `RefundPayment`, `AdjustInventory`). Separate read/write databases, event sourcing, or command/query microservices are added only if an implemented workload proves they are worth the extra consistency/operations contract.

Cross-cutting API behavior is uniform where it truly spans endpoints: correlation/safe logging, authentication, generic rate/resource limits, safe error shaping, and coarse policy live in ASP.NET Core host/pipeline/endpoint metadata. Resource/OpenFGA authorization and domain/workflow/concurrency validation still run at the layer where the actual resource/state exists.

Every externally reachable endpoint declares its audience/authentication/policy/limits or an explicit reviewed public exception. Authentication success alone is never resource authorization.

Retryable mutations use caller-provided semantic idempotency keys.

```text
same key + same intent      → same semantic result
same key + changed intent   → reject
```

When one store owns business mutation, idempotency receipt and outbox, commit them atomically.

Duplicate handling is end-to-end: caller/producer retry, transport redelivery, and consumer/effect replay are distinct failure points. Do not treat one broker or one dedupe table as system-wide exactly-once.

Retry is finite/classified/budgeted, with an intentional retry owner for each remote dependency path so nested retries do not multiply blindly.

Long-running work uses durable async status only when genuinely long-running; ordinary short transactions stay synchronous.

Owners:
- `docs/server/CORE_API_AND_WORKER.md`
- `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`.

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

## 10. Persistence — real provider first, measured optimization

Exact DB products remain open until phase POCs:
- PostgreSQL — strongest central reference candidate;
- SQLite + WAL — mature Workstation candidate;
- libSQL — explicit Workstation candidate.

Do **not** create generic `IRepository<T>`, `IUnitOfWork`, or one-interface-per-provider hierarchies solely to appear portable.

Provider-specific DB code stays contained outside business/domain code. Extract interfaces only when an actual dependency/replacement boundary requires them.

Database performance is a trade-off, not a checklist. Indexes can increase write/import cost; caches introduce freshness/invalidation risk; denormalization complicates authoritative updates. Hot-path POCs therefore measure realistic growth/cardinality, query plans, tenant-aware indexes, write/sync/import cost, pool contention and storage/WAL/temp impact before adding Redis/read replicas/sharding/denormalized views.

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

## 15. Worker, events, messaging, and external effects

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

Messaging pattern selection:

```text
one durable task → queue/job semantics
many independent consumers of one fact → pub/sub or multiple outbox deliveries
replay/history/independent offsets required → event stream, only when proven
immediate authoritative answer → direct synchronous path
```

The transactional outbox is the normal bridge from an authoritative commit to later consequences. Do not make hidden event choreography the primary correctness owner for payments, stock, permissions, or other protected transitions.

Worker requirements include:
- bounded concurrency;
- durable claims/leases where needed;
- idempotent/reconcilable effects;
- retry classification/backoff;
- no-progress handling;
- pause/drain/recovery;
- `OutcomeUnknown` for ambiguous external effects.

No Kafka/event-stream infrastructure or generic pub/sub broker is baseline merely because those patterns exist.

Notifications/webhooks use Core API/outbox/Worker boundaries first; no notification microservice baseline.

Owners:
- `docs/server/CORE_API_AND_WORKER.md`
- `docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md`.

---

## 16. State placement, observability, and physical operations

OpenTelemetry/OTLP is the telemetry boundary.

Current managed targets:
- New Relic — metrics/traces/APM;
- Aiven OpenSearch — structured operational logs;
- Backtrace — crash diagnostics direction.

Guard supplies bounded desktop lifecycle/crash/resource evidence into the support/diagnostic path.

Telemetry failure cannot invalidate business transactions.

Current server hardware is lower-spec/desktop-class rack equipment.

`Stateless Core API/Worker` means process memory is not the only authoritative durable business state. State is relocated into the systems that own it: DB, object store, durable job/outbox state, ZITADEL/OpenFGA, configuration/session state where required, and the local Workstation DB for offline work. Process-local caches/circuits remain disposable/explicitly lossy.

`Stateless` does not promise automatic failover, transparent Blazor circuit recovery, or zero downtime.

Owner: `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

---

## 17. History/audit without event-sourcing the product

SquiFlow needs explainable history in selected domains, but the baseline remains authoritative current relational state plus explicit immutable/append-only records where required.

Examples:
- payment/effect evidence;
- stock movements;
- corrections/reversals;
- issued document revisions;
- privileged security/admin audit;
- versioned rule/workflow/form publication.

Transactional outbox events and audit logs are **not** event sourcing. Event sourcing remains deferred unless a real domain requires replay-derived authoritative state strongly enough to justify event schema/projection/rebuild complexity.

---

## 18. Verification

Keep tests focused on real correctness risks:
- business/domain invariants;
- real DB transaction/concurrency/isolation behavior;
- DB hot-path performance at representative growth/cardinality and write/index cost;
- Workstation local durability/restart;
- Guard independent crash/hang/update recovery;
- ZITADEL authentication/session flows;
- OpenFGA model/tuple/custom-role/consistency/reconciliation behavior;
- tenant/API authorization;
- endpoint cross-cutting metadata/policy completeness;
- idempotency/response loss across caller/transport/consumer boundaries;
- retry amplification and retry-budget exhaustion;
- sync conflict/long-offline;
- `IObjectStore` provider contract + Hugging Face adapter;
- `IBackupTarget` contract + encrypted Kaggle backup restore;
- actual low-end hardware/resource limits.

Do not introduce unrelated interfaces merely to increase mock/unit-test count.

Owner: `docs/testing/VERIFICATION_STRATEGY.md`.

---

## 19. Sequential implementation plan

Default WIP limit: **one implementation phase**, but each phase must cover its defined edge/failure behavior before being called complete.

```text
Phase 0  Web + Workstation + Guard + Core API skeleton, provider contracts, minimal CI
Phase 1  ZITADEL identity + OpenFGA Owner/Staff/custom-role authorization + Web session/render-mode proof
Phase 2  first local-first Workstation Customer/Order transaction + local DB + Guard recovery
Phase 3  authoritative sync + central DB + pooled tenant isolation + idempotency + realistic DB performance proof
Phase 4  conflict/long-offline/resnapshot recovery
Phase 5  one native rule + workflow + bounded dynamic form
Phase 6  create Worker + Platform Admin Web; prove first real queue/schedule/event consequence
Phase 7  Hugging Face IObjectStore flow + documents/printing + Kaggle IBackupTarget restore proof
Phase 8  API/observability/admin hardening
Phase 9  payments/credit/inventory/correction hardening
Phase 10 actual-rack release/resource/restore qualification + paid-provider migration readiness
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
- container sidecar/proxy/leader/scatter-gather infrastructure without a concrete need;
- global CRDTs;
- schema/database/deployment per tenant baseline;
- multi-currency/FX subsystem;
- advanced peripheral suite;
- MRP/wastage;
- generic ETL/search/SaaS billing infrastructure without a real requirement;
- hundreds of placeholder files/projects.

The accepted `IObjectStore`, `IBackupTarget`, Guard, ZITADEL, and OpenFGA boundaries are **not** examples of forbidden complexity: each has a concrete current or committed near-term responsibility.

---

## 21. Implementation-complete rule

A capability is complete when the concerns that materially apply to that capability are proven: user states/recovery, tenant/authority, validation/permission, transaction/idempotency/concurrency, local-vs-server authority, async/external-unknown behavior, resource/storage bounds, upgrade/restore implications, and relevant hostile tests.

Do not force irrelevant checklist items onto tiny features, but do not waive required edge cases simply to keep the implementation visually minimal.
