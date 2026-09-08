# SquiFlow v0.0.15 — Master Implementation Plan

**Status:** Current audited architecture/implementation baseline.  
**Implementation state:** **pre-Phase-0**.

This file is the high-level source of truth. `docs/review/DECISION_AUDIT.md` records what was kept, simplified, deferred or removed. Detailed semantics belong to focused owner documents.

---

## 1. Implementation principle

SquiFlow should preserve hard correctness/security boundaries without pre-building future machinery.

Use this order:

```text
real user journey
→ smallest correct implementation
→ hostile/failure test
→ measurement
→ only then extract abstraction/process/service if evidence earns it
```

Do not create a helper, interface, project or provider-wrapper layer merely because it might be useful later.

---

## 2. Runtime shape

Accepted technology direction:

```text
apps/web            Blazor Web App — tenant business Web + tenant Owner Settings
apps/desktop        Avalonia — Windows Workstation
services/core-api   ASP.NET Core HTTP/composition host
```

Create later when their first real feature exists:

```text
apps/admin-web      Blazor Web App — SquiFlow platform control plane
services/worker     durable background execution
```

Business capability code remains a modular monolith.

There is **no baseline SquiFlow.Guard process**. Workstation begins as one process. A helper/supervisor process is added only when a real updater/native-library/driver fault-isolation problem proves that process boundary necessary.

Owner: `docs/architecture/REPOSITORY_STRUCTURE.md`.

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

## 4. Identity and sessions

Interactive authentication uses OpenID Connect; SquiFlow authorization is separate.

Stable external identity:

```text
(issuer, subject)
```

Workstation login:

```text
system browser
→ Authorization Code
→ PKCE S256
→ validated native callback
→ SquiFlow session/device context
```

No reusable native client secret and no central DB credentials on the Workstation.

Exact OIDC provider, browser session implementation and native callback mechanism are closed just-in-time for Phase 1.

Owner: `docs/security/IDENTITY_AND_SESSIONS.md`.

---

## 5. Web versus Workstation

### Web

Web is **online-only for business operations in v0.0.15**.

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

Owner: `docs/workstation/LOCAL_FIRST_DESKTOP.md`.

---

## 6. Multi-tenancy isolation

Ordinary tenants use pooled isolation:

```text
authoritative TenantContext
→ shared central schema/model
→ tenant discriminator on tenant-owned data
→ provider-specific defense in depth
```

Authentication, application authorization and tenant isolation are separate concerns.

Do not prebuild schema-per-tenant, DB-per-tenant, queue-per-tenant or deployment-per-tenant.

If PostgreSQL is used, prove RLS defense in depth, safe runtime roles and safe tenant context under connection pooling. This does not silently select PostgreSQL.

Owner: `docs/architecture/MULTI_TENANCY_ISOLATION.md`.

---

## 7. Authorization

Permission keys represent stable business actions, not screens/routes.

Server authorization can combine:

```text
authenticated actor
+ authoritative tenant/platform context
+ function permission
+ resource scope
+ sensitive property access
+ canonical state/workflow guard
+ risk/step-up where required
+ expected version/concurrency
```

ASP.NET Core policy/requirements/`IAuthorizationService` are the runtime primitives.

Authorization mutations advance `TenantAuthorizationRevision`; Workstation permission snapshots never replace server reauthorization.

Owner: `docs/security/TENANT_PERMISSIONS.md`.

---

## 8. API/idempotency/retry

Retryable mutations use caller-provided semantic idempotency keys.

```text
same key + same intent      → same semantic result
same key + changed intent   → reject
```

When one store owns the business mutation, idempotency receipt and outbox, commit them atomically.

Retry is finite/classified/budgeted. Long-running work uses durable async status only when the work is genuinely long-running; ordinary short transactions stay synchronous.

Owner: `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`.

---

## 9. Synchronization

Workstation durable outbox is the local upload truth; in-memory signaling only wakes work.

```text
bounded pending items
→ authenticated Sync API
→ authoritative tenant + permission + business/rule validation
→ idempotency + concurrency/conflict
→ central transaction
→ per-item result
→ durable local acknowledgement
```

Remote changes + cursor advancement commit together locally.

Conflict behavior is aggregate-specific. Long-offline recovery preserves local pending intent and may require upgrade/resnapshot/rebase/review.

Owner: `docs/sync/SYNC_AND_AUTHORITY.md`.

---

## 10. Persistence — real provider first, abstraction only if earned

Exact products remain open until their phase POCs:
- PostgreSQL — strongest central reference candidate;
- SQLite + WAL — mature Workstation candidate;
- libSQL — explicit Workstation candidate.

Do **not** create generic `IRepository<T>`, `IUnitOfWork`, one-interface-per-provider or a `persistence/abstractions` project solely to appear portable.

Keep provider-specific code contained outside business/domain code. Introduce an interface/project when a real dependency inversion, multiple production implementation, stable process/wire contract, or active migration makes it useful.

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

Local rule evaluation cannot make stale server-owned facts authoritative.

Workflow is continuation-first and versioned. Phase 5 proves one real rule + one workflow + one bounded dynamic form, not a general BPM/form platform.

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

Owner: `docs/domain/BUSINESS_MODEL.md`.

### Currency — minimal requirement

Currency must not be hardcoded.

Baseline:

```text
Tenant.DefaultCurrencyCode
monetary record retains CurrencyCode where historical meaning requires it
```

Do not add exchange-rate providers, FX conversion, multi-currency accounting or a currency-service/interface hierarchy until a real customer requires multi-currency behavior.

Owner: `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`.

---

## 13. Files and bootstrap storage

### Primary object storage

Current bootstrap primary object storage is a **private Hugging Face Storage Bucket** with approximately **100 GB** current private-storage capacity.

Treat capacity as finite. Business DB stores object metadata/ownership/hash/lifecycle; application-level immutable/versioned keys protect issued/historical object identity.

Do not create `IObjectStorage` merely because migration is expected. Keep Hugging Face calls localized in infrastructure code. Extract the narrow migration seam when migration actually begins.

### Backup

Current bootstrap off-site backup carrier is a **private Kaggle Dataset** containing only locally encrypted opaque backup archives.

Never upload raw customer DB dumps/CSV/object trees to Kaggle.

```text
required backup state
→ package/compress locally
→ authenticated encryption locally
→ opaque .sqfbak + checksum
→ private Kaggle Dataset version
→ download verification
→ restore drill
```

Backup key/recovery material stays outside Kaggle and must itself be recoverable.

### Migration trigger

Planned migration to purpose-built paid primary/backup storage: **first paying customer**, or earlier if capacity, privacy/compliance, reliability, service limits, contract or restore requirements demand it.

Owner: `docs/data/FILES_AND_OBJECT_STORAGE.md`.

---

## 14. Printing and physical devices

Printing is the baseline device side effect and initially runs through the normal Workstation/Windows printing path.

```text
committed business document
→ print request
→ Windows printer/spooler
→ success/failure/unknown physical output
```

Print failure does not undo committed business truth.

Do not create a helper process for printing unless actual driver/native behavior proves process isolation is needed.

Other peripherals are requirement-driven.

Owner: `docs/workstation/LOCAL_FIRST_DESKTOP.md`.

---

## 15. Worker and external effects

A separate Worker executable is created in **Phase 6**, when the first durable background workload exists.

Worker requirements then include:
- bounded concurrency;
- durable claims/leases where needed;
- idempotent/reconcilable effects;
- retry classification/backoff;
- no-progress handling;
- pause/drain/recovery;
- `OutcomeUnknown` for ambiguous external effects.

Do not create the Worker project in Phase 0 simply because the target architecture has one.

Notifications/webhooks use Core API/outbox/Worker boundaries first; no notification microservice baseline.

Owners:
- `docs/server/CORE_API_AND_WORKER.md`
- `docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md`.

---

## 16. Observability and physical operations

OpenTelemetry/OTLP is the telemetry boundary.

Current managed targets:
- New Relic — metrics/traces/APM;
- Aiven OpenSearch — structured operational logs;
- Backtrace — crash-diagnostics direction.

Telemetry failure cannot invalidate business transactions.

Current server hardware is lower-spec/desktop-class rack equipment. `Stateless` means process memory is not authoritative; it does not promise automatic failover.

Before paying-customer production, prove actual hardware resource/recovery behavior and the encrypted Hugging Face/Kaggle storage/restore path, then migrate the bootstrap storage arrangement as planned.

Owner: `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

---

## 17. Verification

Keep tests focused on real correctness risks:
- business/domain invariants;
- real DB transaction/concurrency/isolation behavior;
- Workstation local durability/restart;
- tenant/API authorization;
- idempotency/response loss;
- sync conflict/long-offline;
- object failure/capacity;
- encrypted backup download/restore;
- actual low-end hardware/resource limits.

Do not introduce interfaces merely to increase mock/unit-test count. Provider correctness should be tested against real adapters where provider behavior matters.

Owner: `docs/testing/VERIFICATION_STRATEGY.md`.

---

## 18. Sequential implementation plan

Default WIP limit: **one implementation phase**.

```text
Phase 0  Web + Workstation + Core API skeleton, minimal CI/dependency boundaries
Phase 1  identity + Owner/Staff + Web-only permissions
Phase 2  first local-first Workstation Customer/Order transaction + local DB
Phase 3  authoritative sync + central DB + pooled tenant isolation + idempotency
Phase 4  conflict/long-offline/resnapshot recovery
Phase 5  one native rule + workflow + bounded dynamic form
Phase 6  create Worker + Platform Admin Web for first durable/control-plane slice
Phase 7  Hugging Face file/document flow + in-process printing + encrypted Kaggle backup proof
Phase 8  API/observability/admin hardening
Phase 9  payments/credit/inventory/correction hardening
Phase 10 actual-rack release/resource/restore qualification + paid-storage migration readiness
```

Each phase closes only the decisions needed to start that phase, implements a complete vertical slice, attacks it, measures it, then updates later assumptions from evidence.

Owner: `docs/implementation/PHASES_AND_GATES.md`.

---

## 19. Explicit non-baseline work

Do not add these now:
- dedicated accessibility/a11y workstream or conformance program;
- Guard/supervisor/helper process without a proven isolation need;
- generic repository/unit-of-work/provider abstraction hierarchy;
- full browser offline/PWA sync;
- Kafka/event-log infrastructure;
- mandatory Redis;
- microservice-per-module architecture;
- full CQRS/event sourcing/Saga core architecture;
- global CRDTs;
- Zanzibar authorization service;
- schema/database/deployment per tenant baseline;
- multi-currency/FX subsystem;
- advanced peripheral suite;
- MRP/wastage;
- generic ETL/search/SaaS billing infrastructure without a real requirement;
- hundreds of placeholder files/projects.

---

## 20. Implementation-complete rule

A capability is complete when the concerns that materially apply to **that capability** are proven: user states/recovery, tenant/authority, validation/permission, transaction/idempotency/concurrency, local-vs-server authority, async/external-unknown behavior where relevant, resource/storage bounds, upgrade/restore implications, and tests.

Do not force irrelevant checklist items onto tiny features merely to satisfy documentation symmetry.
