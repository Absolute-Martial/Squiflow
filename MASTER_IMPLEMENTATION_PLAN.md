# SquiFlow v0.0.15 — Master Implementation Plan

**Status:** Current architecture/implementation baseline.  
**Implementation state:** **pre-Phase-0** — the GitLab repository currently contains the curated architecture/planning baseline; the target `apps/`, `services/`, `modules/`, persistence adapters and executable tests/CI are not yet implementation evidence.

This file is the high-level source of truth. Detailed semantics belong to the focused owner documents linked below; do not duplicate those contracts differently here.

---

## 1. Product/runtime boundaries

SquiFlow is a C# / modern .NET multi-tenant business platform designed for small teams first while keeping a path to larger tenants.

Accepted presentation/runtime direction:

```text
apps/web            Blazor Web App — tenant staff Web + tenant-owner Settings
apps/admin-web      Blazor Web App — SquiFlow platform administration/control plane
apps/desktop        Avalonia — Windows Workstation
services/core-api   ASP.NET Core HTTP/composition host
services/worker     durable background execution
```

Business capability modules remain a **modular monolith**. Separate .NET projects are allowed where they provide a real dependency/build/process/provider/test boundary; they do not imply microservices.

Target repository responsibilities are described in `docs/architecture/REPOSITORY_STRUCTURE.md`. Do not create empty projects merely to match a diagram.

---

## 2. Small-team tenant/admin model

A normal tenant may be only:

```text
Owner
└── Staff
```

`Owner` and `Staff` are default templates, not fixed policy.

The tenant Owner controls ordinary staff roles/permissions inside SquiFlow's entitlement/security ceiling. Permission/role changes, tenant rules/workflow/forms and other tenant control-plane configuration are **Web-only** through tenant Settings/Administration.

The Desktop consumes effective permissions/configuration but cannot grant roles, publish workflow/rules or change platform/server controls.

SquiFlow Platform Admin Web is a separate privileged surface for platform tenants/entitlements, provider/runtime configuration, incidents, support/break-glass business controls and Worker/server control-plane commands.

Detailed ownership:
- `docs/admin/ADMIN_SURFACES.md`
- `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`
- `docs/security/TENANT_PERMISSIONS.md`

---

## 3. Identity and session boundary

Interactive authentication uses **OpenID Connect**. Application authorization is separate.

Stable external identity is `(issuer, subject)`, not mutable email.

Workstation interactive login uses:

```text
system browser
→ Authorization Code
→ PKCE S256
→ validated native callback
→ SquiFlow session/device context
```

No reusable native client secret and no central database credentials are stored on the Workstation.

Exact OIDC provider, Web session implementation, native callback mechanism and some logout/step-up provider mappings remain phase-load-bearing implementation decisions.

Owner: `docs/security/IDENTITY_AND_SESSIONS.md`.

---

## 4. Web runtime

The Web application is **online-only for business operations in v0.0.15**.

Do not implement:
- IndexedDB business replica;
- service-worker business synchronization;
- browser offline mutation queue/conflict engine.

Normal HTTP/CDN/static-asset caching is allowed. `localStorage` is for harmless preferences; authentication/business truth is not stored there.

For long/valuable online forms, explicit **server-side drafts/autosave** are allowed when they prevent meaningful user-data loss. This does not introduce browser offline architecture.

Owner: `docs/web/WEB_RUNTIME_AND_STORAGE.md`.

---

## 5. Windows Workstation is the local-first client

For an explicitly offline-capable operation:

```text
user action
→ local validation + compatible local rule/config facts
→ one durable local business + outbox transaction
→ immediate local result
→ background synchronization
→ server authoritative validation/acceptance/conflict
```

Local state is real user work, but authority is explicit:

```text
LocalCommitted
PendingRemote
Authoritative
Conflict
Rejected
AuthorizationChanged
UpgradeRequired
```

The local store is not "just a cache", but the server never trusts local tenant IDs/permissions as central authority.

Owner: `docs/workstation/LOCAL_FIRST_DESKTOP.md`.

### Guard and devices

`SquiFlow.Guard` is a tiny supervision/recovery companion, not a business process. It assists bounded crash/restart/update/helper lifecycle and must not own business rules, ORM/business persistence, synchronization semantics or platform authority.

**Printing is the baseline physical-device integration.** Printer/spooler/driver failure is a separate retryable side effect and never undoes committed business truth.

Scanner/barcode/cash-drawer/other peripherals are added only when a real journey requires them.

Owner: `docs/workstation/GUARD_AND_DEVICE_INTEGRATION.md`.

---

## 6. Multi-tenancy isolation

Ordinary v0.0.15 tenants use a **pooled baseline**:

```text
shared Web/API/Worker
→ authoritative TenantContext
→ shared central relational model
→ tenant discriminator on tenant-owned authoritative records
→ provider-appropriate persistence defense in depth
```

Authentication, authorization and tenant isolation are different concerns.

Tenant-owned repositories/queries are structurally tenant-scoped so isolation does not depend only on a remembered `WHERE TenantId = ...`.

Schema-per-tenant, database-per-tenant, physical queue-per-tenant and deployment-per-tenant are **not baseline**. Future dedicated data/stack placement is evidence-driven by residency/compliance/contract/SLO/customer-managed requirements.

If PostgreSQL is used, its reference POC must prove Row-Level Security defense in depth, non-bypass runtime roles, read/write policy behavior and safe tenant context under connection pooling. PostgreSQL remains a candidate, not a silently selected provider.

Owner: `docs/architecture/MULTI_TENANCY_ISOLATION.md`.

---

## 7. Authorization

Permission keys represent stable business actions, not screens/routes.

Server business authorization can combine:

```text
authenticated actor
+ authoritative tenant/platform context
+ function permission
+ resource/object scope
+ sensitive property access
+ canonical business state
+ workflow transition guard
+ step-up/risk requirement
+ expected version/concurrency
```

ASP.NET Core policy/requirements/`IAuthorizationService` are the runtime primitives; SquiFlow does not build a competing authorization service.

Tenant authorization mutations advance a monotonic `TenantAuthorizationRevision` atomically with audit/outbox invalidation evidence so stale authorization caches/snapshots are detectable.

Owner: `docs/security/TENANT_PERMISSIONS.md`.

---

## 8. API/idempotency/retry

Retryable mutating operations use **caller-provided semantic idempotency keys**.

```text
same key + same business intent      → same/semantically equivalent result
same key + materially changed intent → reject
```

Idempotency key, HTTP request ID, correlation ID, message ID and business entity ID are distinct.

Where one authoritative store owns the mutation/receipt/outbox, commit them atomically.

Retry is finite, classified, bounded, honors `Retry-After`, and uses backoff/jitter where appropriate. Do not stack independent retry loops into a retry storm.

Long-running work uses durable asynchronous request-reply/status resources rather than holding HTTP requests forever. Ordinary short transactions remain synchronous.

Owner: `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`.

---

## 9. Synchronization

Workstation synchronization uses durable local outbox truth; in-memory signaling only wakes work.

Server path:

```text
bounded pending batch
→ authentication
→ authoritative TenantContext
→ current permission/resource/business/rule validation
→ idempotency/concurrency/conflict
→ central transaction
→ per-item result
→ durable local acknowledgement
```

Remote changes + cursor advance are one local transaction.

Conflict policy is aggregate-specific; there is no global last-write-wins or global CRDT model.

Long-offline recovery preserves local pending work and can require reauth, upgrade, resnapshot, rebase/conflict review or export/repair.

Owner: `docs/sync/SYNC_AND_AUTHORITY.md`.

---

## 10. Persistence products remain open, but implementation needs real adapters

Central database and Workstation embedded database products remain OPEN architecture decisions until their POCs meet the required workload/failure/isolation gates.

Current candidates:
- PostgreSQL — strongest central reference candidate;
- SQLite + WAL — mature local reference candidate;
- libSQL — explicit local candidate.

The implementation sequence closes the **initial adapter choice just in time** for the phase that requires it. An open long-term provider decision must not become an excuse to avoid implementing a real vertical slice.

Owner: `docs/data/PERSISTENCE_SELECTION.md`.

---

## 11. Rules, workflow and dynamic forms

SquiFlow owns a bounded tenant-safe native rule architecture:
- typed fact schema;
- safe structured representation, no arbitrary tenant C#/JS/SQL;
- validation/complexity limits;
- immutable versioned publication;
- deterministic evaluation;
- bounded decision trace;
- server/Workstation compatibility.

Local rule evaluation respects **fact authority/freshness**: `LocalSafe`, `LocalProvisional` and `ServerRequired` decisions cannot be mixed blindly.

Workflow is continuation-first: every non-terminal state has an actor/discovery/action/deadline/reassignment/cancel-or-compensate/version/conflict/recovery story. Active instances are pinned to their definition version unless explicitly migrated.

Phase 5 must implement one bounded/versioned dynamic form and its migration/version semantics; arbitrary HTML/script customization is not allowed.

Owners:
- `docs/rules/NATIVE_RULE_ENGINE.md`
- `docs/workflow/WORKFLOW_DESIGN.md`
- form details remain a Phase-5 open decision recorded in `OPEN_DECISIONS.md`.

---

## 12. Business-domain baseline

The domain is practical small/medium business work, not a generic ERP checkbox set.

Core model:

```text
Party
→ Commercial Relationship / Account
→ Business Context
→ Transaction
→ Workflow
→ Settlement
```

Support practical walk-in/registered/organization/program/representative/credit scenarios, quotations/tenders, purchasing/suppliers, fulfillment, payments/corrections/refunds and pragmatic inventory.

Do **not** baseline MRP, universal stock reservation, complex banner-roll wastage, universal lot/serial tracking or full procurement workflow without evidence.

Shared primitives must be consistent across modules:
- Money + currency identity + rounding policy;
- Quantity + unit;
- absolute time + tenant/business timezone/date;
- internal ID versus human/legal document number;
- explicit correction/reversal/revision rather than overwriting issued truth;
- Party duplicate/merge and privacy/data-lifecycle semantics when required.

Owners:
- `docs/domain/BUSINESS_MODEL.md`
- `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`.

---

## 13. Files/object storage and the current 100 GB constraint

Primary durable object storage holds retained business binary objects; DB stores tenant-owned metadata/reference/hash/version/lifecycle.

The currently available object-storage envelope is approximately **100 GB** and must be treated as a real capacity limit.

Do not mix all of these as unlimited retained data:
- customer artwork;
- issued/generated documents;
- temporary exports;
- diagnostics;
- orphaned objects;
- backups.

Backups are a separate durability class and the primary business-object bucket is not assumed to be its own only backup.

Before production, define storage usage monitoring, retention, warning/critical/hard admission behavior and a restore strategy. Do not silently delete retained customer objects to recover space.

Owner: `docs/data/FILES_AND_OBJECT_STORAGE.md`.

---

## 14. Worker and external effects

Durable Worker execution uses:
- bounded concurrency/queues;
- tenant-aware fairness/admission;
- claims/leases/fencing where required;
- idempotent/reconcilable effects;
- retry classification/backoff;
- no-progress detection;
- pause/drain/recovery;
- quarantine/DLQ;
- `OutcomeUnknown` for ambiguous external effects.

Queued work is classified as:
- committed business consequence;
- deferred actor action;
- platform-control command.

This determines whether later permission revocation changes execution authority.

Owner: `docs/server/CORE_API_AND_WORKER.md`.

### Notifications/integrations

Notifications/webhooks/external deliveries use existing Core API/outbox/Worker boundaries first. A separate notification service is not baseline.

Delivery failure normally does not rewrite already committed business truth. Webhooks require signing, replay protection, URL/SSRF controls, bounded retry/backpressure and secret rotation.

Owner: `docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md`.

---

## 15. Observability

OpenTelemetry/OTLP is the provider-neutral instrumentation boundary.

Current managed targets:
- New Relic — metrics/traces/APM;
- Aiven OpenSearch — searchable structured operational logs;
- Backtrace — crash diagnostics direction.

Managed/free tiers are **capacity-limited dependencies**, not infinite resources. Telemetry queues/spools/retries are bounded and provider quota/export failure cannot block business transaction correctness.

Authoritative security/business audit remains durable SquiFlow data when its history is part of correctness.

Owner: `docs/observability/OBSERVABILITY.md`.

---

## 16. Physical deployment/operations reality

The current environment is owned lower-spec/desktop-class rack hardware, not an elastic cloud.

`Stateless/disposable server node` means process memory is not authoritative. It does **not** promise another node exists, automatic failover or zero downtime.

Before production, capture actual CPU/RAM/disk/filesystem/network/node-role/SPOF inventory and qualify the real hardware.

Durability/recovery testing includes power/restart/disk-full/restore behavior appropriate to the actual storage stack. UPS/power protection, independent backup target, RPO/RTO, break-glass private access and operator ownership are explicit deployment decisions.

If Platform Admin/Core API is itself down, recovery uses a separate private infrastructure break-glass runbook. That path is not exposed through Desktop/business APIs.

Owner: `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

---

## 17. Accessibility and interaction quality

Accessibility is a **release-level requirement**, not deferred polish.

Core requirements include:
- keyboard-complete operation;
- predictable focus;
- semantic labels/validation/status;
- no color-only state;
- scalable text/layout;
- accessible dialogs/dynamic updates;
- accessible communication of `LocalCommitted`, `PendingRemote`, `Conflict`, `OutcomeUnknown`, permission failures and high-risk admin diffs.

Tenant branding/dynamic forms cannot bypass this baseline.

Exact formal conformance/legal target remains OPEN until deployment/jurisdiction requirements are known.

Owner: `docs/ux/ACCESSIBILITY_AND_INTERACTION_QUALITY.md`.

---

## 18. Verification strategy

The prose attack cases are not enough.

Use layered verification:
- domain/property tests;
- application tests;
- **real** persistence-adapter tests;
- Workstation local-store tests;
- ASP.NET Core API/authorization tests;
- selected end-to-end journeys;
- failure injection;
- accessibility verification;
- release qualification on the actual deployment hardware;
- backup/restore drills.

CI success must not imply hardware/restore qualification ran when it did not.

Owner: `docs/testing/VERIFICATION_STRATEGY.md`.

---

## 19. Sequential implementation plan

Implementation follows one vertical phase at a time with a default **WIP limit of 1 phase**.

```text
Phase 0  executable repository/runtime skeleton + CI + hardware inventory
Phase 1  identity + Owner/Staff + Web-only permissions
Phase 2  first local-first Workstation Customer/Order transaction + Guard skeleton
Phase 3  authoritative sync + idempotency + pooled tenant isolation
Phase 4  conflict/long-offline/backlog recovery
Phase 5  native rule + workflow + one dynamic form
Phase 6  Worker + long-running API + platform control plane/break-glass separation
Phase 7  files/object capacity + documents + printer/device side effects
Phase 8  API/observability/admin/accessibility hardening
Phase 9  payments/credit/inventory/correction hardening
Phase 10 actual-hardware release/resource/backup/restore qualification
```

The detailed deliver/attack/gate criteria and **phase-start decision gates** are owned by `docs/implementation/PHASES_AND_GATES.md`.

### Planning stop rule

Do not wait for every future enterprise decision to be solved.

For each phase:
1. close the decisions required to start that phase;
2. implement the vertical slice;
3. attack it with tests;
4. measure actual behavior;
5. update later architecture only when evidence changes an assumption.

The current repository is now detailed enough to start **Phase 0**.

---

## 20. Deliberate non-baseline complexity

Do not add these merely because architecture catalogs or future products might use them:

- full browser offline/PWA business sync;
- Kafka/event-log infrastructure;
- mandatory Redis/shared cache;
- microservice per module;
- full CQRS/event sourcing/Saga core architecture;
- global CRDT model;
- generic Zanzibar-style relationship authorization service;
- schema/database/deployment per tenant baseline;
- physical queue/worker per tenant baseline;
- sharding/multi-region active-active/deployment stamps;
- advanced manufacturing/MRP/wastage;
- advanced peripheral suite without customer journeys;
- SaaS metering/billing engine before the commercial model requires it;
- specialized import/ETL or search infrastructure before evidence requires it;
- hundreds of placeholder files/projects to make documentation look complete.

---

## 21. Definition of implementation-complete

A capability is incomplete until it answers, where applicable:

- who uses it and all important UX states;
- accessibility of the implemented journey;
- authoritative owner/tenant/isolation scope;
- permission/resource/property/domain validation;
- transaction boundary;
- idempotency/concurrency;
- local versus remote authority;
- async work/crash/no-progress/external-unknown behavior;
- retry/cancel/correction/recovery;
- audit/telemetry/privacy;
- storage/memory/CPU/network/resource bounds;
- version skew/update/long-offline behavior;
- capacity exhaustion/noisy-neighbor behavior;
- backup/restore/deletion/retention;
- physical deployment/recovery impact when relevant;
- tests/failure injection proving the contract.

If those answers only exist in discussion and not in the owning current document/executable tests, the implementation is not complete.
