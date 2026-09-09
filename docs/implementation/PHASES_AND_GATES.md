# Sequential Implementation Phases and Gates

**Version:** v0.0.15

Implementation is sequential. Default WIP limit: **one phase**.

The WIP rule limits parallel unfinished work; it does **not** reduce the depth of the current phase. A phase is not complete until the edge/failure/recovery behaviors that materially belong to it are proven.

For each phase:
1. close only the decisions required to start it;
2. implement one complete vertical slice;
3. run the relevant correctness/security/failure tests;
4. measure real behavior;
5. revise later assumptions only when evidence changes them.

## Phase-start decisions

- **Phase 0:** ZITADEL/OpenFGA are selected; no final central/local DB is required yet. Create the real Guard and the two already-justified provider contracts (`IObjectStore`, `IBackupTarget`) without scaffolding unrelated abstractions. Start a version-controlled `deploy/` representation, but do not select Kubernetes/Flux/Terraform merely to make the repository look production-like.
- **Phase 1:** close ZITADEL Cloud vs self-hosted, instance/project/application layout, first OpenFGA store/model, model-ID rollout, initial consistency/reconciliation policy, and Blazor render/circuit/session topology.
- **Phase 2:** choose the Workstation local DB after the smallest SQLite/libSQL proof needed for a real local transaction.
- **Phase 3:** choose the initial central DB implementation capable of authoritative transaction + pooled isolation + normalized schema/index/query-plan proof, and close the first concrete API/sync compatibility versioning mechanism required by Workstation/server skew. The DB proof begins from an explicit SquiFlow workload profile rather than generic benchmark traffic.
- **Phase 6:** create `apps/admin-web`, **independent `services/admin-api`**, and `services/worker` only when their first real control/durable-work slice exists. Choose only the background mechanism required by that workload.
- **Phase 7:** use private Hugging Face through `IObjectStore` and encrypted private Kaggle through `IBackupTarget`.
- **Before paying-customer production:** actual rack inventory/recovery, reproducible deployment/rebuild, backup restore proof, provisional RPO/RTO, operator/break-glass access, printer support, edge/DNS/TLS/time recovery, scaling thresholds, provider migration readiness, and the hard resource/usage limits required by the production profile must be known honestly.

## Cross-phase consumption and limit gate

Do **not** postpone resource accounting/limits to a hypothetical future billing phase.

Whenever any phase introduces a resource whose usage matters for hard capacity, provider/account cost, abuse protection, tenant/manual contract policy, or another enforced limit, that same phase must define the smallest required consumption/limit contract from `docs/requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md`.

Where materially applicable, prove:
- stable meter meaning, unit and scope;
- exactly when consumption occurs;
- whether semantic retries count once or provider attempts genuinely count multiple times;
- durable/reconcilable usage state independent from analytics/telemetry;
- hard versus advisory limit semantics and consistency/freshness requirements;
- concurrency/atomic check-and-consume or bounded reservation behavior near the final unit;
- deterministic versioned policy precedence;
- explicit degraded behavior if limit/accounting state is unavailable;
- Workstation offline/current-server-limit behavior where relevant;
- restore/reconciliation behavior so usage cannot reset or double after recovery;
- tenant isolation and Admin authority for tenant/platform limit changes.

Do not build a generic metering/data-warehouse platform before the first real resource needs it. Implement only the meters/policies required by the current slice.

---

## Phase 0 — executable boundaries, not placeholder architecture

Create:

```text
apps/web
apps/desktop/workstation
apps/desktop/guard
services/core-api
modules/                 only first-slice capabilities
infrastructure/storage/  IObjectStore + HuggingFaceObjectStore shell/contract
infrastructure/backup/   IBackupTarget + KaggleBackupTarget shell/contract
infrastructure/identity/ ZITADEL integration boundary
infrastructure/authorization/ OpenFGA integration boundary
tests/ deploy/ docs/
```

Do not create yet:
- `apps/admin-web`;
- `services/admin-api`;
- Worker;
- generic repository/unit-of-work hierarchy;
- one interface per class/provider API;
- generic metering/billing/data-warehouse infrastructure;
- GraphQL/service-mesh/event-bus infrastructure;
- HTTP/gRPC endpoints between ordinary business modules;
- Kubernetes manifests/cluster machinery merely because server processes may later be containerized;
- dozens of empty modules/projects.

Deliver:
- solution/build structure;
- CI that produces a versioned immutable artifact/checksum, runs the first secret/dependency checks, and does not bake environment secrets into client/server artifacts;
- Web/Workstation/Guard/Core API launchable skeletons;
- Guard launches/supervises Workstation and records bounded lifecycle evidence;
- basic health endpoint;
- typed TenantContext boundary;
- `IObjectStore` and `IBackupTarget` contracts using SquiFlow-owned types only;
- architecture tests preventing provider SDK types from leaking into business/domain code;
- architecture test/convention that ordinary modules remain in-process rather than becoming accidental HTTP services;
- basic code-quality conventions: meaningful business names, no magic business/config values, no forwarding-only helper/interface chains;
- minimal rack hardware inventory;
- initial version-controlled `deploy/` representation/runbook showing how the current executable skeleton is started/stopped/configured without claiming the final production IaC tool is selected.

Attack:
- Workstation process exits unexpectedly while Guard survives;
- Guard exits while Workstation survives;
- both are terminated and restarted;
- incompatible Guard/Workstation protocol version;
- provider implementation accidentally leaks Hugging Face/Kaggle types into a business contract;
- a proposed SOLID/clean-code refactor creates an interface/helper with no real responsibility or replacement boundary;
- a developer proposes HTTP/gRPC between two modules that run in the same host without a real process/security/fault boundary;
- a deployment change exists only as an undocumented manual command and cannot be recreated from repository/runbook state;
- a secret or environment-specific credential is present in source/build output, or the same version is rebuilt into different bytes for each environment without explanation.

Gate:
- Web, Workstation, Guard and Core API build/run;
- Guard failure does not corrupt local business state;
- Guard can observe/recover Workstation process failure without owning business logic;
- `IObjectStore`/`IBackupTarget` are narrow enough to implement a second adapter later without mirroring whole third-party SDKs;
- Admin Web/Admin API/Worker remain documented future boundaries without empty placeholder projects;
- no architecture-style abstraction/network hop/orchestrator exists solely to satisfy a pattern slogan;
- the current development/deployment skeleton is reproducible enough that a second machine/operator is not forced to infer every startup step.

---

## Phase 1 — ZITADEL identity + OpenFGA smallest tenant

Deliver:
- configured ZITADEL OIDC applications for tenant Web and Workstation;
- Owner tenant bootstrap;
- invite one Staff user;
- `(issuer, subject)` account mapping;
- Workstation system-browser Authorization Code + PKCE `S256` login/device enrollment;
- authoritative SquiFlow membership → TenantContext resolution;
- first OpenFGA store and pinned authorization model ID;
- Owner/Staff relations;
- one tenant-defined custom role flow using tuples rather than model redeployment;
- Web-only role/permission assignment;
- ASP.NET Core semantic authorization requirement invoking OpenFGA;
- `TenantAuthorizationRevision` snapshot/audit correlation;
- durable/reconcilable authorization-change operation spanning SquiFlow DB/audit state and OpenFGA tuple write;
- chosen Blazor render/circuit/session state-placement model with known reconnect/memory behavior;
- cookie/session flags and CSRF/antiforgery behavior for the chosen Web topology;
- encoded-output default plus first Content Security Policy/security-header baseline for the implemented Web surface.

Do not:
- trust ZITADEL token roles as current SquiFlow authorization truth;
- let Desktop write OpenFGA tuples;
- equate a ZITADEL organization claim directly with SquiFlow TenantContext without server verification;
- put workflow/payment/stock/limit arithmetic into OpenFGA;
- rely on transient Blazor circuit memory to preserve valuable business drafts;
- create a parallel JWT/PASETO/WebAuthn/password subsystem that duplicates ZITADEL.

Attack:
- expired/duplicate invitation;
- forged/wrong issuer/audience token;
- PKCE/state/redirect tampering;
- client supplies another TenantId;
- Owner revokes Staff while Staff is logged in;
- tenant custom role created/edited;
- OpenFGA tuple write succeeds but SquiFlow completion persistence crashes;
- retry after ambiguous authorization-change outcome;
- request accidentally uses latest OpenFGA model instead of pinned model ID;
- stale/low-consistency authorization result immediately after a change;
- Desktop attempts permission change;
- PII accidentally used in tuple IDs;
- Web circuit/server restart during valuable draft editing;
- syntactically valid JWT from an untrusted issuer/audience is presented;
- ZITADEL is unavailable during new login, existing Web session use, and high-risk step-up;
- cookie-authenticated mutation without valid antiforgery evidence;
- stored/reflected/client-side XSS attempt through implemented customer/staff fields and validation/error rendering.

Gate:
- ZITADEL authenticates; OpenFGA authorizes; SquiFlow tenant/domain checks remain independent;
- role/grant UI reports applied only when intended OpenFGA state is known applied;
- ambiguous tuple writes have reconciliation, not guesswork;
- custom role does not require new authorization model deployment;
- no embedded reusable Workstation client secret;
- durable business work does not depend solely on process-local Blazor circuit state;
- outage/session behavior is explicit and no identity-provider failure turns into accidental application authorization.

---

## Phase 2 — first local-first Customer/Order slice + Guard recovery

Deliver:
- minimal Customer/walk-in + Order journey;
- selected local DB;
- one atomic local business + outbox transaction;
- immediate local UI result;
- stable semantic idempotency key;
- tenant default currency code used rather than a hardcoded currency;
- monetary record retains applied currency code where historical meaning requires it;
- Guard heartbeat/lifecycle integration, bounded restart/backoff, safe-start path, and update-handoff contract skeleton;
- normal Windows printing path only if this first journey actually prints.

If this slice introduces a real enforced resource limit, apply the cross-phase consumption/limit gate; otherwise do not create placeholder metering code.

Attack:
- Workstation process termination after durable local commit;
- Guard restarts Workstation;
- repeated startup crash reaches restart budget/safe mode instead of looping forever;
- Guard itself crashes and later restarts;
- sleep/hibernate/clock jump during heartbeat;
- disk full/DB locked;
- lost in-memory sync wakeup;
- sign-out/restart with pending work;
- printer unavailable after committed transaction.

Gate:
- local work survives all process lifecycle failures covered by the selected local DB durability contract;
- Guard recovery never deletes/rewrites pending business work;
- Guard resource use is measured but functionality is not removed to meet an arbitrary tiny footprint;
- no network dependency for explicitly local-capable work;
- UI clearly distinguishes local/pending state from server-authoritative state instead of calling it vaguely “eventually consistent.”

---

## Phase 3 — authoritative sync + central DB + pooled isolation

Deliver:
- bounded sync upload;
- semantic idempotency;
- ZITADEL-authenticated server session/device identity;
- authoritative TenantContext;
- tenant-scoped resource lookup;
- OpenFGA permission/resource check where applicable;
- separate domain/workflow/state validation;
- explicit request/response contracts;
- expected-version concurrency;
- selected central DB adapter;
- normalized authoritative schema for the implemented Customer/Order slice;
- bounded custom/extensible fields kept separate from core relational invariants;
- pooled tenant discriminator and provider-appropriate isolation proof;
- PostgreSQL RLS proof if PostgreSQL is selected;
- atomic business mutation + idempotency receipt + outbox where one store owns them;
- remote change feed + cursor;
- finite retry/backoff with an intentional retry owner for each remote path;
- **documented workload profile** covering read/write/delete mix, representative item sizes, tenant/data skew, normal concurrency, reconnect/sync/import burst concurrency, consistency needs, hot-query cardinality, and current HA/geographic assumptions;
- actual query-plan/index proof at current and projected larger cardinalities;
- measured write/WAL/storage/migration impact of the selected indexes;
- explicit consistency classification for authoritative versus derived state;
- if PostgreSQL is selected: measured connection/backend-process resource cost, WAL/checkpoint behavior, autovacuum, temp/sort spill, archive/log/disk growth, and crash/restart recovery under the SquiFlow workload;
- first explicit API/sync protocol compatibility/version contract for Workstation/server skew;
- REST/task-oriented resource/command shape without pretending every semantic transition is generic CRUD;
- per-invariant concurrency mechanism: expected version/conditional update, database constraint, or narrowly justified isolation/lock;
- classified deadlock/serialization/lock-timeout behavior with bounded whole-transaction retry only where safe;
- first compatible expand-migrate-switch-contract schema change exercised across supported old/new reader/writer behavior.

If the slice consumes a strict metered resource in the same authoritative store, prove whether business mutation + idempotency receipt + consumption/limit decision can commit atomically. If not, define the explicit reservation/reconciliation boundary.

If the slice consumes a strict metered resource in the same authoritative store, prove whether business mutation + idempotency receipt + consumption/limit decision can commit atomically. If not, define the explicit reservation/reconciliation boundary.

If the slice consumes a strict metered resource in the same authoritative store, prove whether business mutation + idempotency receipt + consumption/limit decision can commit atomically. If not, define the explicit reservation/reconciliation boundary.

If the slice consumes a strict metered resource in the same authoritative store, prove whether business mutation + idempotency receipt + consumption/limit decision can commit atomically. If not, define the explicit reservation/reconciliation boundary.

Attack:
- response lost after commit;
- duplicate same-intent POST command from caller retry;
- POST without an idempotency contract incorrectly treated as retry-safe;
- same key + changed intent;
- cross-tenant object/list/write attempt;
- valid OpenFGA permission but wrong TenantId/resource query;
- permission or hard limit changed while local operation pending;
- connection reused across tenants, including RLS/session-context leakage;
- partial batch failure;
- retry amplification across Workstation/API/provider layers;
- same semantic operation retried without double-counting a one-per-effect meter;
- two requests race for a final hard-limit unit when a limit exists;
- query that is fast at 10K rows but degrades at projected cardinality;
- Web-style read benchmark passes while Workstation reconnect burst causes unacceptable write/WAL/lock pressure;
- over-indexed schema causing unacceptable sync/import/write cost;
- checkpoint/autovacuum/temp spill causes latency/resource spikes on the selected DB;
- WAL/archive/log growth approaches finite disk capacity;
- stale derived projection accidentally used as current authority;
- out-of-order derived update overwrites a newer projection state;
- older Workstation sends a supported old protocol version;
- unsupported/breaking protocol version is silently interpreted as the latest contract;
- lost update under two valid concurrent commands;
- deadlock/serialization conflict retries only part of a transaction or exceeds its budget;
- contraction removes a column/shape still used by a supported Workstation, pending sync item, old backend, durable job/message, or stored snapshot.

Gate:
- no duplicate semantic effect or duplicate one-per-effect usage;
- no cross-tenant leakage even if an authorization relation exists incorrectly, and no cross-tenant consumption attribution;
- stale Workstation permission/limit snapshot is not server authority;
- central DB behavior proven against the real adapter and real SquiFlow workload shape;
- authoritative schema is not denormalized/EAV/JSON merely for UI convenience;
- each important index has a named query/invariant and measured cost;
- provider maintenance behavior fits the current rack resource/disk envelope;
- derived state has explicit source/freshness/rebuild semantics;
- compatible old client requests are handled deliberately and unsupported versions fail explicitly;
- chosen locks/isolation protect the invariant without an unbounded wait/deadlock/retry loop;
- schema contraction has reader/writer/data inventory plus drain/compatibility and rollback/roll-forward evidence;
- no system-wide `exactly once`, `eventually consistent`, or `strictly RESTful` claim is made from one narrower mechanism.

---

## Phase 4 — conflict and long-offline recovery

Deliver:
- one real aggregate conflict UX;
- protocol/schema/model compatibility checks;
- reauth/upgrade/resnapshot/rebase path;
- preservation of pending local intent;
- tombstone/change-history retention policy;
- large-backlog transfer policy;
- refreshed OpenFGA-derived effective permission snapshot after reconnect;
- refreshed authoritative usage/limit state for any server-enforced resource exposed to Workstation UX.

Attack:
- weeks/months offline;
- old protocol/schema/rules/permissions/limit snapshot;
- OpenFGA role revoked while client offline;
- tenant/resource hard limit lowered or exhausted while client offline;
- deleted/merged remote entity;
- large pending backlog;
- slow connection;
- local disk nearly full.

Gate:
- no silent pending-work deletion;
- client older than retained incremental history gets explicit recovery;
- old local permissions or cached limits never bypass current server OpenFGA/domain/limit authority.

---

## Phase 5 — one rule + workflow + dynamic form

Deliver:
- one bounded tenant rule;
- fact-authority classification;
- one configurable workflow stage/transition;
- one bounded versioned form;
- Web-only authoring/publication;
- immutable compatible snapshot;
- decision trace;
- one semantic authorization requirement whose permission side is OpenFGA-backed and whose state/transition side is SquiFlow-owned.

Attack:
- invalid/conflicting rule;
- stale central fact used offline;
- definition changes with active old instances;
- approver permission revoked in OpenFGA;
- simultaneous transitions;
- two-person approval deadlock.

Gate:
- authorization and workflow/domain validity remain separate;
- previous published definition survives failed publication;
- old instances remain explainable.

---

## Phase 6 — independent Platform Admin backend + Worker

Now create:

```text
apps/admin-web
services/admin-api
services/worker
```

Deliver:
- Platform Admin Web using **Admin API directly**;
- Admin API as a separate ASP.NET Core executable/composition/deployment boundary;
- no ordinary runtime dependency from Admin API to Core API;
- separate platform authentication/session validation and platform OpenFGA authorization scope;
- Admin API health/readiness, service credentials, endpoint inventory, rate/admission limits and audit/correlation;
- first real platform-control journey owned end-to-end by Admin API;
- first real background workload classified by trigger: user consequence, schedule, external system, batch/volume, or platform control;
- explicit message semantics: command/job versus committed event;
- the simplest durable execution pattern matching that workload;
- durable job/outbox consumption;
- if scheduled, a durable schedule occurrence identity/state before execution rather than `cron fired` as the only truth;
- claims/leases where needed;
- bounded concurrency/fairness;
- retry/no-progress/quarantine;
- idempotent/reconcilable consumer behavior;
- explicit shared-module/data ownership rules for any state touched by both Core API and Admin API;
- private infrastructure break-glass path when Admin API itself is unavailable.

If this phase implements the first platform/tenant limit control, Admin API owns versioned policy change/audit and does not expose generic raw-counter mutation. If the Worker workload is metered, define semantic-job versus provider-attempt consumption separately.

A shared edge reverse proxy/gateway may route Core API and Admin API separately. It must not make Admin API transit Core API. Do not add a service mesh unless real east-west service topology proves the need.

Platform-control flow:

```text
Admin Web
→ Admin API
→ ZITADEL platform session/step-up as required
→ platform OpenFGA authorization
→ exact diff/version/risk checks
→ durable platform command/proposal
→ Worker/system/provider execution if needed
→ verify
→ audit
```

Do **not** implement normal platform administration as:

```text
Admin Web → Admin API → Core API
```

and do not keep privileged `/platform-admin/...` endpoints on Core API as a compatibility bypass.

Attack:
- Core API stopped while Admin API remains healthy;
- Admin API stopped while Core API remains healthy;
- Admin API accidentally proxies a platform command through Core API;
- normal tenant user reaches Admin API;
- tenant OpenFGA role is incorrectly treated as platform authority;
- Admin API and Core API race on shared state and violate a common invariant;
- one host bypasses a shared business module with ad-hoc SQL against a reachable table;
- a synchronous internal call chain is introduced where an in-process/shared-module or durable async path should own the behavior;
- unauthorized tenant limit increase or raw usage mutation;
- limit lowered below current usage;
- Worker crash before/after external effect;
- stale lease owner;
- duplicate schedule firing;
- repeated transport redelivery;
- one tenant flooding work;
- Admin Web/Admin API unavailable during recovery.

Gate:
- a real platform-admin operation succeeds while Core API is intentionally unavailable, provided its own underlying dependencies are healthy;
- ordinary tenant business API operation remains possible while Admin API is intentionally unavailable;
- no platform/super-admin endpoint exists on Core API;
- shared DB/domain/usage invariants are identical across Core API and Admin API where both legitimately touch the same state;
- runtime-process separation has not accidentally become microservice/database-per-service ceremony;
- no generic force-success/mark-complete/set-raw-usage control;
- break-glass is infrastructure recovery, not hidden tenant API;
- the selected queue/job/pub-sub/event mechanism exists because the first workload needs its semantics, not because a managed messaging product was listed in an article;
- no service-mesh/gateway component exists unless its current responsibility is concrete and measured.

---

## Phase 7 — provider-bound files, documents, printing, backup restore

Deliver:
- `HuggingFaceObjectStore : IObjectStore` complete adapter;
- object metadata/key/hash/lifecycle in DB;
- authoritative/reconcilable retained-object-byte consumption measurement;
- capacity measurement/admission behavior for current ~100 GB provider/account envelope;
- any required platform/tenant object-storage limit policy without inventing commercial plan packaging;
- attachment staging/upload/retry;
- immutable/versioned application object keys;
- document generation for implemented journeys;
- printing path;
- `KaggleBackupTarget : IBackupTarget` complete adapter;
- backup orchestrator collecting all currently required recovery state, including implemented usage/limit state, not just application rows;
- encrypted opaque backup upload/download/restore proof.

Attack:
- object succeeds/metadata fails and usage must reconcile;
- metadata exists/object missing;
- duplicate upload/retry does not double retained-byte accounting;
- two uploads race against final available provider/tenant capacity;
- cross-tenant object reference or usage attribution;
- storage nears hard provider/account capacity;
- tenant limit lowered below current retained bytes without deleting retained customer objects;
- upload interrupted;
- printer/spooler failure after commit;
- Kaggle artifact corrupt/missing/wrong key;
- restore accidentally resets/doubles usage or applies stale limit policy;
- raw data accidentally selected for direct provider upload;
- second fake/test adapter proves interface is not accidentally Hugging-Face/Kaggle-shaped.

Gate:
- business code has no direct Hugging Face/Kaggle dependency;
- retained-byte usage is explainable/reconcilable independently from telemetry;
- provider/account capacity and any implemented tenant storage limit have explicit hard/soft admission behavior;
- one encrypted off-site backup actually restores usable SquiFlow state including usage/limit state that affects enforcement;
- interface abstractions do not hide provider-specific failures that must surface to operations;
- large transfer does not starve normal sync/API traffic.

---

## Phase 8 — API/performance/rate/network/observability/admin hardening

Deliver only hardening relevant to implemented surfaces:
- OpenTelemetry correlation across Core API/Admin API/Worker;
- New Relic + Aiven OpenSearch export;
- Backtrace path where applicable;
- Guard lifecycle/crash/resource evidence correlation;
- bounded telemetry queues/spools with explicit loss/backpressure behavior;
- tenant/platform audit stored independently of lossy operational logs where required;
- high-risk exact-diff/step-up Admin API flows actually implemented;
- generated endpoint inventories for Core API and Admin API separately;
- production CORS/cache/error/header policies;
- implemented browser/API application-security baseline: output/template encoding or narrow sanitization, CSRF where cookie-backed, field allow-lists, parameterized/allow-listed data access, and safe error disclosure;
- SSRF-safe outbound HTTP;
- dependency timeout/retry budgets;
- liveness/readiness/functional health per backend;
- layered rate/admission policy for login/recovery, account/device, tenant, route/work class, expensive provider actions, and Admin API as applicable;
- stable `429`/`Retry-After` behavior and client backoff;
- measured API performance baseline using pagination, bounded connection pooling, selective cache/compression only where justified;
- conditional cache contract where a cache exists: tenant/permission-safe keys, freshness/invalidation, capacity, stampede/miss amplification, outage bypass, and cold recovery;
- HTTPS/TLS production routing with direct edge routes to Core API and Admin API;
- HTTP transport-version negotiation treated as infrastructure behavior, not business semantics;
- WebSocket/SignalR, if used, kept non-authoritative;
- REST remains the baseline; no GraphQL surface unless an implemented client has already demonstrated the query-composition requirement and its authorization/query-cost design has been reviewed;
- observability of limit evaluation failure/latency, near-capacity, rejection/defer/throttle, reconciliation drift and stale reservations where implemented.

Attack:
- applicable OWASP API cases;
- telemetry quota/export failure while usage accounting/limit enforcement continues correctly;
- analytics/metric retention disabled without losing authoritative consumption;
- async log buffer saturation;
- retry storm;
- rate-limit bypass by changing route/identity dimensions;
- authorized tenant attempts to monopolize expensive work;
- client ignores `Retry-After`;
- noisy tenant;
- cache returns stale protected authority;
- pool exhaustion/tenant-context reuse;
- compression/buffering creates disproportionate CPU/memory usage;
- edge routes platform traffic through Core API instead of directly to Admin API;
- edge/gateway authentication causes backend authorization or hard-limit enforcement to be skipped;
- WebSocket/live signal lost after commit and system still recovers from durable state;
- DNS/custom-host manipulation attempts to change TenantContext;
- clock skew affects token/lease/schedule/limit-window behavior;
- sensitive content in logs/Guard/usage diagnostics;
- OpenFGA or ZITADEL degraded/unavailable and resulting fail-closed/degraded behavior;
- consumption/limit state unavailable with resource-specific degraded behavior;
- Core API overload while Admin API still needs emergency application-control capability;
- Admin API compromise attempt cannot pivot into tenant business authority without explicit platform operation path;
- SQL/identifier/filter/sort injection, mass assignment/excessive field, XSS/unsafe template, CSRF, SSRF redirect/private-network, and malicious file/path/size cases for implemented surfaces;
- cache outage, cold start, miss storm/stampede, cross-tenant key collision, and stale sensitive result where a cache is implemented;
- release artifact/configuration mismatch or migration preflight failure is stopped before unsafe exposure.

Gate:
- telemetry failure does not affect committed business truth or erase authoritative usage;
- auth/authorization dependency failures never become accidental allow;
- hard-limit decision unavailability cannot silently become universal allow;
- Core API and Admin API have separate privileged-surface inventories;
- rate/admission controls are bounded/fair without being one arbitrary global limit;
- performance improvements have measured benefit and do not weaken freshness/authority/resource bounds;
- edge/gateway routing preserves backend ownership and does not become business/usage authority;
- durable correctness does not depend on WebSocket/circuit/transient network state;
- no undocumented privileged endpoint;
- no GraphQL/service-mesh/gRPC/cache/gateway-management layer exists only because it is a common architecture pattern;
- application-security controls are exercised through real Web/API/data/file paths rather than declared complete from scanner output alone.

---

## Phase 9 — payments/credit/inventory hardening

Deliver:
- payment `OutcomeUnknown` semantics;
- provider-effect idempotency/reconciliation;
- refund/reversal/correction;
- inventory concurrency policy;
- credit authority;
- Owner/manual price permission/audit;
- OpenFGA permission + SquiFlow state/invariant checks for sensitive actions;
- minimal currency/quantity/time semantics without hardcoding;
- any paid-provider/payment-attempt meter required by the real provider, with semantic effect versus charged-attempt distinction.

Attack:
- charge succeeds/response lost;
- duplicate refund/webhook;
- provider attempt is retried and genuinely incurs another provider usage/cost unit without duplicating the semantic business effect;
- concurrent last-stock sale;
- stale offline credit;
- stale derived projection used as current credit/stock truth;
- permission revoked just before sensitive action;
- OpenFGA allowed but business invariant denies;
- currency/rounding edge cases of supported model.

Gate:
- payment/credit/stock correctness never depends on a merely eventual cache/projection;
- ambiguous external effects enter reconciliation rather than guessed success/failure.

---

## Phase 10 — paying-customer production qualification and provider migration readiness

Deliver/prove:
- actual rack resource tests;
- Workstation + Guard soak/recovery tests;
- Core API/Admin API independent deployment/restart test;
- node/SPOF inventory;
- restart/disk-full/recovery behavior;
- installer/update/rollback including Guard/Workstation compatibility;
- backup restore onto replacement environment;
- encrypted backup key recovery;
- Hugging Face object integrity/capacity and usage-reconciliation report;
- effective platform/provider hard limits for production resources;
- exact tenant-scoped limits actually required for the initial production/commercial arrangement, if any, without inventing unused plan tiers;
- restore/reconciliation proof for implemented consumption counters/ledger/policies/reservations;
- ZITADEL/OpenFGA deployment backup/reprovision procedure appropriate to managed/self-hosted choice;
- provisional/final RPO/RTO;
- private recovery runbook exercise;
- operator ownership;
- new paid `IObjectStore`/`IBackupTarget` adapter readiness;
- **clean/replacement-environment rebuild from the version-controlled deployment/IaC definitions and runbook**;
- **edge/DNS/TLS/certificate/time failure exercise** with a recovery path that does not rely on the same failed public edge;
- **measured capacity envelope and next-move table** for first-order bottlenecks (DB, Worker/CPU, disk, network/object transfer, external dependency, node saturation);
- **immutable-artifact promotion and release drill** including configuration/capacity/dependency/migration preflight, health plus authorized smoke checks, and the selected rollback/roll-forward/maintenance recovery path;
- **cross-version schema/durable-work proof** before any destructive contraction, including supported old backends/Workstations, pending sync, jobs/messages, idempotency results, and versioned snapshots;
- conditional final-container image hardening/scan/runtime-identity/resource proof if containers are selected.

Gate:
- do not accept a paying customer while backup restore is untested;
- do not accept a paying customer with a hard provider/resource limit that lacks defined measurement/admission/recovery behavior;
- telemetry/analytics is not the only copy of usage needed to enforce a production limit;
- do not accept a paying customer if the production environment can only be recreated from undocumented manual knowledge;
- migrate bootstrap storage at the first paying customer or earlier when constraints require it;
- no HA/zero-downtime/scalability claim without measured topology/capacity evidence;
- no blue-green/canary/progressive-delivery claim without spare capacity/routing, compatible data contracts, telemetry, and a tested stop/recovery path;
- Kubernetes remains absent unless the actual multi-node orchestration problem has been demonstrated;
- no simplification is accepted if it removes a core recovery/security/offline/control-plane/usage-limit behavior already relied on by the product.

---

## Deferred

Do not spend baseline work on:
- dedicated accessibility/a11y program;
- generic repository/unit-of-work/one-interface-per-class abstractions;
- arbitrary additional helper processes beyond Guard without a specific need;
- browser offline/PWA business sync;
- Kafka/mandatory Redis;
- generic pub/sub/event-bus infrastructure without a real multi-consumer requirement;
- event-driven-everything;
- full CQRS/event sourcing/Saga;
- eventual-consistency-everywhere;
- GraphQL/GraphQL Federation;
- service mesh;
- Kubernetes before a concrete cluster-orchestration problem exists;
- API-management platform selected before need;
- HTTP/gRPC between ordinary modules;
- database-per-service for the current modular monolith;
- MQTT/WebRTC/FTP/SFTP/raw TCP/UDP/gRPC without a concrete workload;
- custom Raft/consistent-hashing/Merkle-repair/distributed-database machinery;
- Operational Transformation without a real collaborative-editing requirement;
- denormalized authoritative core schema;
- global CRDTs;
- per-tenant schema/database/queue/stack by default;
- multi-currency/FX system;
- advanced peripheral suite;
- generic analytics/data-warehouse metering platform;
- SaaS billing/plan-pricing/ETL/search/MRP infrastructure without a current requirement.
