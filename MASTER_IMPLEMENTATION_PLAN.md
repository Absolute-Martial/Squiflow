# SquiFlow v0.0.15 — Master Implementation Plan

**Status:** Current architecture and implementation baseline.

This is the primary implementation document. Detailed docs under `docs/` explain the boundaries that need more depth. Historical generated inventories/review ledgers are not design authorities.

## 1. Product and runtime shape

SquiFlow is a C#/.NET multi-tenant business platform designed for very small teams first, while remaining expandable.

```text
apps/web            Tenant staff Web + tenant-owner Settings/Administration
apps/admin-web      SquiFlow platform administration/control plane
apps/desktop        Windows Workstation + Guard/on-demand helpers
services/core-api   ASP.NET Core HTTP/composition host
services/worker     Durable background execution
```

Business modules remain a modular monolith. Web/Admin/Desktop/API/Worker are real runtime/deployment boundaries; modules are not automatically services.

## 2. Small-team-first tenant model

The normal tenant may simply be:

```text
Owner
└── Staff
```

`Owner` and `Staff` are starting templates. The tenant Owner decides ordinary staff rights inside the tenant's purchased/enabled capabilities and SquiFlow's non-overridable security/domain limits.

The Owner can create roles, clone/edit templates, assign users, and optionally scope assignments to branch/program/own-assigned records where that has clear business meaning.

The platform still prevents cross-tenant access, platform-operator privilege, unavailable entitlements, arbitrary code execution, direct database/root access, and bypass of protected financial/security invariants.

## 2A. Multi-tenancy isolation baseline

Isolation is treated as a spectrum rather than one forever-topology.

For ordinary v0.0.15 tenants the implementation target is **pooled compute + pooled authoritative data**:

```text
shared Web/API/Worker
→ authoritative TenantContext
→ shared central schema/model
→ TenantId on tenant-owned authoritative records
→ provider-appropriate defense-in-depth isolation
```

Authentication, application authorization and tenant isolation are separate concerns. A valid identity/role does not by itself prove that a resource belongs to the current tenant.

Tenant context is derived from authoritative SquiFlow membership/placement state, not trusted from a Workstation payload, browser header, custom-domain Host value or stale client snapshot.

Tenant-owned repository/query contracts must be tenant-scoped so isolation does not depend on each developer remembering a filter. Lists, writes, reports, search, exports, Worker jobs, object metadata and read models must preserve tenant scope.

Schema-per-tenant, database-per-tenant, physical queue-per-tenant and full deployment-per-tenant are **not** baseline implementation targets.

The architecture still leaves an evolution path:

```text
pooled
→ targeted dedicated resource / dedicated database
→ dedicated stack
```

only when residency, compliance, contractual isolation, noisy-neighbor, enterprise scale or customer-managed hosting requirements justify the added operational cost.

Processing isolation is a separate axis from data isolation. Baseline Worker processing stays pooled but tenant-aware, bounded and fair; dedicated worker capacity can be introduced later for a tenant/tier when evidence requires it.

If PostgreSQL is used as the central reference/selected provider, its pooled-storage POC must prove Row-Level Security as defense in depth with safe runtime roles, write-side checks and connection-pool tenant-context handling. This does not select PostgreSQL; any selected provider must prove an equivalent provider-appropriate isolation story.

See `docs/architecture/MULTI_TENANCY_ISOLATION.md`.

## 3. Permission ownership and where permissions are changed

Permission **definitions** are stable server-side capabilities such as:

```text
customers.view
customers.edit
orders.create
orders.edit
orders.cancel
orders.apply_manual_price
orders.approve
inventory.view
inventory.adjust
payments.record
payments.refund
quotes.create
quotes.approve
documents.print
team.manage
roles.manage
domains.manage
rules.manage
workflow.manage
```

Permission/role assignment is a **Web control-plane operation only**:

```text
Tenant Owner Web Settings
→ /tenant-admin/... API
→ current-actor delegation check
→ role/permission validation
→ durable change
→ session/authorization version update
→ audit
```

The Workstation may read its effective permissions and react to revocation, but it never grants roles or permissions.

Platform permissions/entitlements are changed only through `apps/admin-web` and `/platform-admin/...` APIs.

Authorization for a business command combines:

```text
authenticated actor
+ tenant/platform scope
+ permission
+ resource scope
+ canonical resource state
+ workflow transition guard
+ risk tier/step-up when required
+ concurrency/version check
```

A permission is not created for every status combination. Example: `orders.edit` may be granted while the domain still permits editing only in `Draft`.

## 4. Canonical state + tenant workflow stage

Tenant workflow customization must not redefine protected system truth.

```text
Canonical system state
+
Configurable tenant workflow stage
```

Example:

```text
Order canonical state: Accepted
Tenant stage: WaitingForDesignApproval
```

Tenant stages may describe how work proceeds. Canonical states protect payment, stock, financial, security and synchronization invariants.

Creation/editing of tenant workflow stages is also Web administration only. The Desktop consumes the published/versioned workflow snapshot relevant to it.

## 5. Web administration surfaces

### Tenant administration — `apps/web`

For a two-person business this is ordinary `Settings / Administration`, not an enterprise console.

It owns the presentation for:
- Team and invitations
- Roles and permissions
- Branch/program settings
- Rules/workflow/forms
- Custom domains/branding
- Feature settings
- Devices/workstations
- Tenant-visible audit and configuration history

### Platform administration — `apps/admin-web`

For SquiFlow operators only:
- tenants/subscriptions/entitlements
- global/runtime configuration
- provider configuration
- incidents and health
- platform security
- support/break-glass operations
- server/worker control-plane tasks

The browser is never the authorization authority. Both surfaces call authoritative APIs.

## 6. Critical server/control-plane tasks are Web-only

Platform-critical commands must originate from `apps/admin-web`; they are not exposed through Desktop sync/business APIs.

Examples:
- pause/drain/resume a Worker workload class;
- retry/quarantine/reconcile a privileged failed job;
- rotate platform/provider secrets;
- change deployment/runtime/resource policies;
- change platform feature entitlements;
- database/storage maintenance or restore workflows;
- cross-tenant support operations;
- platform domain/provider configuration.

Flow:

```text
Platform Admin Web
→ /platform-admin/... API
→ permission + scope
→ exact diff / risk classification
→ step-up MFA / approval if required
→ durable command/proposal
→ Worker/control-plane execution where async
→ verification
→ audit
```

Desktop continues to send ordinary business changes through `/sync/...`; this Web-only rule is for the privileged server control plane, not normal business synchronization.

## 7. ASP.NET Core API host

`services/core-api` is the HTTP/composition host, conceptually similar to a separate Axum host project in Rust.

It owns:
- endpoint registration;
- auth/session middleware;
- rate limits/admission controls;
- correlation;
- health/readiness;
- dependency composition.

It does not own business/domain rules. Business operations live in application/domain modules.

Suggested endpoint groups:

```text
/api/...
/tenant-admin/...
/platform-admin/...
/sync/...
/client/...
```

The URL is organizational, not the security boundary.

## 8. Identity and Workstation login

Use one canonical SquiFlow browser identity authority for Web, custom domains, Admin Web and native Workstation interactive login.

```text
Install Workstation
→ launch
→ open system browser
→ SquiFlow identity login/MFA
→ choose permitted tenant/workstation context
→ device enrollment/approval if required
→ one-time authorization callback
→ Workstation exchanges code using PKCE
→ local session/device credential
→ bootstrap authorized configuration/rules/data
```

The Workstation does not collect the password as its primary login path and never receives central database credentials.

## 9. Custom domains

A tenant Owner with `domains.manage` may configure verified domains through Web Settings.

```text
Draft
→ PendingVerification
→ Verified
→ CertificateProvisioning
→ Active
```

Failure states include `VerificationFailed`, `CertificateFailed`, `Misconfigured`, `Suspended`, `Removing`, `Removed`.

SquiFlow verifies ownership, handles TLS lifecycle, persists authoritative mapping centrally, audits changes and keeps a safe SquiFlow fallback domain unless deliberately disabled through a reviewed policy.

Authentication still redirects through the canonical identity origin. Do not share one broad auth cookie across arbitrary customer-owned domains.

## 10. Web is online-only for business operations in v0.0.15

Do **not** implement partial/offline-first Web business behavior now.

Current Web baseline:
- requires network for business reads/writes;
- may use normal browser/HTTP caching for static versioned assets;
- may keep harmless UI preferences in `localStorage`;
- may keep transient per-tab UI hints in `sessionStorage`;
- keeps auth/session secrets out of `localStorage`;
- does not use IndexedDB as a business-data store;
- does not queue business mutations offline;
- does not add a service-worker synchronization model.

If the connection is lost, preserve only safe in-memory form state where practical, show a clear connectivity state, and require reconnect before committing business work.

Offline Web/PWA business behavior is a **future evaluation**, not an implementation requirement for v0.0.15.

## 11. Desktop is the local-first client

The Workstation is where local-first principles belong.

For operations allowed offline:

```text
UI command
→ local validation + applicable local rule snapshot
→ one local durable transaction
     business state + outbox/change record
→ immediate local result
→ background synchronization when network exists
```

The network is not in the critical interaction path for those operations.

However, SquiFlow is not a pure peer-to-peer document editor. Shared payments, stock, credit, permissions, platform configuration and other global invariants still require server authority.

Therefore distinguish:
- **LocalCommitted** — safely stored on this Workstation;
- **PendingRemote** — waiting for remote synchronization/validation;
- **Authoritative** — accepted by the server authority;
- **Conflict/Rejected/UpgradeRequired** — requires recovery or user action.

Never tell the user `Synced` or `Server accepted` merely because local storage succeeded.

## 12. Local-first principles adapted to SquiFlow

The Ink & Switch local-first paper is useful for the Desktop because it emphasizes instant local interaction, the network being optional, multi-device synchronization, longevity/user control and understandable history. It also explicitly notes that banking/e-commerce-like systems are well served by centralized authority. SquiFlow adopts the useful local-first UX/storage principles without making every business entity multi-master.

Practical consequences:
- local DB is the default read/write path for offline-permitted Workstation interaction;
- local writes are durable before sync is attempted;
- synchronization is background/recoverable;
- change/sync history is visible enough to explain pending/conflicting work;
- data export/backup to stable formats is part of user control/longevity;
- no global CRDT requirement;
- conflict policy remains aggregate-specific;
- local tampering is assumed possible, so server authorization/validation still decides shared authority.

## 13. Workstation local durability and synchronization

The selected local store must prove:
- atomic business + outbox transaction;
- crash/power-loss recovery;
- bounded resource use;
- schema migration across skipped releases;
- corruption detection/recovery;
- durable attachment staging;
- local backup/export policy;
- long-offline behavior.

Sync path:

```text
local durable change
→ bounded upload batch
→ authentication
→ authoritative tenant derivation
→ permission/business validation
→ idempotency/concurrency/conflict
→ central transaction
→ per-item result
→ durable local acknowledgement
```

Remote changes and the remote cursor are applied atomically locally. Long-offline clients need upgrade/resnapshot/export/repair paths, never silent discard.

## 14. Worker

Durable asynchronous work uses:
- bounded queues/concurrency;
- claims/leases/fencing where required;
- idempotency;
- retry classification/backoff;
- checkpoints/progress;
- no-progress detection;
- pause/resume/drain;
- crash-loop protection;
- quarantine/DLQ;
- reconciliation for `OutcomeUnknown` external effects.

Worker control is not a hidden Desktop feature. Platform-critical Worker controls are invoked only through Platform Admin Web.

## 15. Persistence products remain open

Central and local persistence requirements are decided; exact products are not.

- PostgreSQL is the strongest current central reference candidate.
- SQLite + WAL is the mature local reference candidate.
- libSQL is an explicit local-store candidate.
- Server and Workstation do not need to use the same product.

Provider-specific reference projects do not silently close the decision. A central provider must also prove the pooled tenant-isolation requirements documented above and in `docs/data/PERSISTENCE_SELECTION.md`.

## 16. Rules and workflow

SquiFlow owns the native bounded rule model, validation, scope/inheritance, immutable snapshots, evaluation contract, decision trace and publication lifecycle. External evaluators can be bounded adapters, not the tenant rule model.

Workflow is continuation-first. Every non-terminal state answers who acts next, where they discover the work, what continues it, deadlines/escalation, cancellation/correction, concurrent actors, permission/version changes and recovery.

Rules/workflows are edited/published through Web administration. The Workstation consumes compatible effective snapshots and may evaluate allowed rules locally for offline UX.

## 17. Files and object storage

- local filesystem: temporary processing/cache/staging and unsynced local attachments;
- durable server objects: object storage behind a provider-neutral abstraction;
- business DB: metadata, references, hashes, lifecycle state;
- repeated delivery: CDN/cache where useful;
- no object store mounted as an ordinary authoritative filesystem.

## 18. Observability

OpenTelemetry/OTLP is the provider-neutral instrumentation boundary.

Current managed targets:
- New Relic free service — metrics/traces/APM;
- Aiven OpenSearch free service — searchable structured operational logs;
- Backtrace — crash-oriented diagnostics.

Managed observability is intentionally relied upon. Telemetry export is still outside business transaction correctness.

## 19. Stateless infrastructure

Web/API/Worker nodes are disposable for authoritative business state. Durable state lives in the selected database/object storage/job records. Shared session/revocation/domain-routing state is durable/shared when required; node caches are reconstructable.

A locally stateful Workstation does not make the server infrastructure stateful.

## 20. What we intentionally do not add now

Do not add complexity merely because it is technically possible:
- no Kafka baseline;
- no YugabyteDB baseline;
- no mandatory Redis;
- no microservice per module;
- no full browser offline/PWA sync layer;
- no CRDT global data model;
- no generic per-row ACL engine;
- no schema-per-tenant/database-per-tenant/deployment-per-tenant baseline;
- no physical Worker queue/pool per tenant baseline;
- no per-tenant cloud account/VPC machinery;
- no separate Rule network service by default;
- no generic `run SQL` / `mark job complete` admin controls;
- no hundreds of placeholder projects/files to satisfy a documentation inventory.

## 21. Implementation sequence

Implement complete vertical journeys, not many parallel modules.

### Phase 0 — repository/runtime skeleton
- `apps/web`, `apps/admin-web`, `apps/desktop`;
- `services/core-api`, `services/worker`;
- modules/packages/persistence abstractions;
- error/result/execution-context contracts;
- typed tenant context/isolation boundary;
- architecture dependency tests.

### Phase 1 — identity + small-tenant setup
- browser identity flow;
- Owner tenant creation/bootstrap;
- invite one Staff user;
- Web-only role/permission assignment;
- authoritative tenant-context resolution;
- Workstation browser-login/device enrollment.

### Phase 2 — first local-first Workstation transaction
- customer/walk-in + minimal order;
- selected local store;
- atomic local business + outbox write;
- instant local UI;
- restart/power-loss recovery.

### Phase 3 — authoritative sync + pooled tenant-isolation proof
- one upload command end to end;
- idempotency receipts;
- tenant-scoped resource/data access;
- provider-specific pooled isolation proof;
- PostgreSQL RLS POC if PostgreSQL remains reference candidate;
- cross-tenant read/write/report/job/connection-pool attacks;
- response-loss retry;
- remote change feed/cursor;
- permission revocation while pending.

### Phase 4 — conflict/long-offline
- concurrent edit;
- stale version;
- protocol/schema/rule-version mismatch;
- resnapshot/export/repair.

### Phase 5 — native rules + workflow
- one tenant rule;
- one configurable workflow stage;
- one approval transition;
- publish through Web;
- compatible Workstation snapshot.

### Phase 6 — Worker/control plane
- durable job;
- lease/retry/no-progress;
- tenant-aware fair/admitted processing;
- pause/drain/recovery from Platform Admin Web only;
- unknown external-effect reconciliation.

### Phase 7 — files/documents/printing
- local staging;
- tenant-scoped object lifecycle;
- printing failure separated from transaction truth;
- helper isolation where justified.

### Phase 8 — observability/admin hardening
- OTel → New Relic/Aiven;
- Backtrace;
- tenant/platform audit;
- noisy-tenant/resource evidence;
- step-up MFA and exact-diff critical admin flows.

### Phase 9 — financial/stock hardening
- payment outcome unknown;
- refund/reversal;
- inventory concurrency;
- credit authority.

### Phase 10 — release/resource/recovery qualification
- 8 GB single-node target;
- Workstation resource budget;
- update/version skew;
- cross-tenant isolation release suite;
- backup/restore;
- failure-injection and long-running soak tests.

Only after this baseline is proven should broader business modules, multi-node HA, dedicated tenant placement or Web offline behavior expand.

## 22. Definition of implementation-complete

A capability is not complete until it answers:
- user states and recovery;
- owner/authority and durable source;
- tenant/isolation scope where applicable;
- validation and permission;
- transaction boundary;
- idempotency/concurrency;
- async work/crash recovery;
- unknown external-effect handling;
- user feedback/retry/cancel;
- audit/telemetry;
- resource limits/noisy-neighbor behavior;
- upgrade/version skew;
- backup/restore/deletion;
- tests proving those behaviors.
