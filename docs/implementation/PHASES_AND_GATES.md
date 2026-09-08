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

- **Phase 0:** ZITADEL/OpenFGA are selected; no final central/local DB is required yet. Create the real Guard and the two already-justified provider contracts (`IObjectStore`, `IBackupTarget`) without scaffolding unrelated abstractions.
- **Phase 1:** close ZITADEL Cloud vs self-hosted, instance/project/application layout, first OpenFGA store/model, model-ID rollout, initial consistency/reconciliation policy, and Blazor render/circuit/session topology.
- **Phase 2:** choose the Workstation local DB after the smallest SQLite/libSQL proof needed for a real local transaction.
- **Phase 3:** choose the initial central DB implementation capable of authoritative transaction + pooled isolation + normalized schema/index/query-plan proof, and close the first concrete API/sync compatibility versioning mechanism required by Workstation/server skew.
- **Phase 6:** create `apps/admin-web`, **independent `services/admin-api`**, and `services/worker` only when their first real control/durable-work slice exists. Choose only the background mechanism required by that workload.
- **Phase 7:** use private Hugging Face through `IObjectStore` and encrypted private Kaggle through `IBackupTarget`.
- **Before paying-customer production:** actual rack inventory/recovery, backup restore proof, provisional RPO/RTO, operator/break-glass access, printer support, and provider migration readiness must be known honestly.

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
- GraphQL/service-mesh/event-bus infrastructure;
- HTTP/gRPC endpoints between ordinary business modules;
- dozens of empty modules/projects.

Deliver:
- solution/build structure;
- CI;
- Web/Workstation/Guard/Core API launchable skeletons;
- Guard launches/supervises Workstation and records bounded lifecycle evidence;
- basic health endpoint;
- typed TenantContext boundary;
- `IObjectStore` and `IBackupTarget` contracts using SquiFlow-owned types only;
- architecture tests preventing provider SDK types from leaking into business/domain code;
- architecture test/convention that ordinary modules remain in-process rather than becoming accidental HTTP services;
- basic code-quality conventions: meaningful business names, no magic business/config values, no forwarding-only helper/interface chains;
- minimal rack hardware inventory.

Attack:
- Workstation process exits unexpectedly while Guard survives;
- Guard exits while Workstation survives;
- both are terminated and restarted;
- incompatible Guard/Workstation protocol version;
- provider implementation accidentally leaks Hugging Face/Kaggle types into a business contract;
- a proposed SOLID/clean-code refactor creates an interface/helper with no real responsibility or replacement boundary;
- a developer proposes HTTP/gRPC between two modules that run in the same host without a real process/security/fault boundary.

Gate:
- Web, Workstation, Guard and Core API build/run;
- Guard failure does not corrupt local business state;
- Guard can observe/recover Workstation process failure without owning business logic;
- `IObjectStore`/`IBackupTarget` are narrow enough to implement a second adapter later without mirroring whole third-party SDKs;
- Admin Web/Admin API/Worker remain documented future boundaries without empty placeholder projects;
- no architecture-style abstraction/network hop exists solely to satisfy a pattern slogan.

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
- chosen Blazor render/circuit/session state-placement model with known reconnect/memory behavior.

Do not:
- trust ZITADEL token roles as current SquiFlow authorization truth;
- let Desktop write OpenFGA tuples;
- equate a ZITADEL organization claim directly with SquiFlow TenantContext without server verification;
- put workflow/payment/stock arithmetic into OpenFGA;
- rely on transient Blazor circuit memory to preserve valuable business drafts.

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
- Web circuit/server restart during valuable draft editing.

Gate:
- ZITADEL authenticates; OpenFGA authorizes; SquiFlow tenant/domain checks remain independent;
- role/grant UI reports applied only when intended OpenFGA state is known applied;
- ambiguous tuple writes have reconciliation, not guesswork;
- custom role does not require new authorization model deployment;
- no embedded reusable Workstation client secret;
- durable business work does not depend solely on process-local Blazor circuit state.

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
- actual query-plan/index proof at current and projected larger cardinalities;
- measured write/WAL/storage/migration impact of the selected indexes;
- explicit consistency classification for authoritative versus derived state;
- first explicit API/sync protocol compatibility/version contract for Workstation/server skew;
- REST/task-oriented resource/command shape without pretending every semantic transition is generic CRUD.

Attack:
- response lost after commit;
- duplicate same-intent POST command from caller retry;
- POST without an idempotency contract incorrectly treated as retry-safe;
- same key + changed intent;
- cross-tenant object/list/write attempt;
- valid OpenFGA permission but wrong TenantId/resource query;
- permission revoked while local operation pending;
- connection reused across tenants, including RLS/session-context leakage;
- partial batch failure;
- retry amplification across Workstation/API/provider layers;
- query that is fast at 10K rows but degrades at projected cardinality;
- over-indexed schema causing unacceptable sync/import/write cost;
- stale derived projection accidentally used as current authority;
- out-of-order derived update overwrites a newer projection state;
- older Workstation sends a supported old protocol version;
- unsupported/breaking protocol version is silently interpreted as the latest contract.

Gate:
- no duplicate semantic effect;
- no cross-tenant leakage even if authorization relation exists incorrectly;
- stale Workstation permission snapshot is not server authority;
- central DB behavior proven against the real adapter;
- authoritative schema is not denormalized/EAV/JSON merely for UI convenience;
- each important index has a named query/invariant and measured cost;
- derived state has explicit source/freshness/rebuild semantics;
- compatible old client requests are handled deliberately and unsupported versions fail explicitly;
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
- refreshed OpenFGA-derived effective permission snapshot after reconnect.

Attack:
- weeks/months offline;
- old protocol/schema/rules/permissions;
- OpenFGA role revoked while client offline;
- deleted/merged remote entity;
- large pending backlog;
- slow connection;
- local disk nearly full.

Gate:
- no silent pending-work deletion;
- client older than retained incremental history gets explicit recovery;
- old local permissions never bypass current server OpenFGA/domain authorization.

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
- shared DB/domain invariants are identical across Core API and Admin API where both legitimately touch the same state;
- runtime-process separation has not accidentally become microservice/database-per-service ceremony;
- no generic force-success/mark-complete control;
- break-glass is infrastructure recovery, not hidden tenant API;
- no service-mesh/gateway component exists unless its current responsibility is concrete and measured.

---

## Phase 7 — provider-bound files, documents, printing, backup restore

Deliver:
- `HuggingFaceObjectStore : IObjectStore` complete adapter;
- object metadata/key/hash/lifecycle in DB;
- capacity measurement/admission behavior for current ~100 GB envelope;
- attachment staging/upload/retry;
- immutable/versioned application object keys;
- document generation for implemented journeys;
- printing path;
- `KaggleBackupTarget : IBackupTarget` complete adapter;
- backup orchestrator collecting all currently required recovery state, not just application rows;
- encrypted opaque backup upload/download/restore proof.

Attack:
- object succeeds/metadata fails;
- metadata exists/object missing;
- cross-tenant object reference;
- storage nears hard capacity;
- upload interrupted;
- printer/spooler failure after commit;
- Kaggle artifact corrupt/missing/wrong key;
- raw data accidentally selected for direct provider upload;
- second fake/test adapter proves interface is not accidentally Hugging-Face/Kaggle-shaped.

Gate:
- business code has no direct Hugging Face/Kaggle dependency;
- one encrypted off-site backup actually restores usable SquiFlow state;
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
- SSRF-safe outbound HTTP;
- dependency timeout/retry budgets;
- liveness/readiness/functional health per backend;
- layered rate/admission policy for login/recovery, account/device, tenant, route/work class, expensive provider actions, and Admin API as applicable;
- stable `429`/`Retry-After` behavior and client backoff;
- measured API performance baseline using pagination, bounded connection pooling, selective cache/compression only where justified;
- HTTPS/TLS production routing with direct edge routes to Core API and Admin API;
- HTTP transport-version negotiation treated as infrastructure behavior, not business semantics;
- WebSocket/SignalR, if used, kept non-authoritative;
- REST remains the baseline; no GraphQL surface unless an implemented client has already demonstrated the query-composition requirement and its authorization/query-cost design has been reviewed.

Attack:
- applicable OWASP API cases;
- telemetry quota/export failure;
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
- edge/gateway authentication causes backend authorization to be skipped;
- WebSocket/live signal lost after commit and system still recovers from durable state;
- DNS/custom-host manipulation attempts to change TenantContext;
- clock skew affects token/lease/schedule behavior;
- sensitive content in logs/Guard diagnostics;
- OpenFGA or ZITADEL degraded/unavailable and resulting fail-closed/degraded behavior;
- Core API overload while Admin API still needs emergency application-control capability;
- Admin API compromise attempt cannot pivot into tenant business authority without explicit platform operation path.

Gate:
- telemetry failure does not affect committed business truth;
- auth/authorization dependency failures never become accidental allow;
- Core API and Admin API have separate privileged-surface inventories;
- rate/admission controls are bounded/fair without being one arbitrary global limit;
- performance improvements have measured benefit and do not weaken freshness/authority/resource bounds;
- edge/gateway routing preserves backend ownership and does not become business authority;
- durable correctness does not depend on WebSocket/circuit/transient network state;
- no undocumented privileged endpoint;
- no GraphQL/service-mesh/gRPC/cache/gateway-management layer exists only because it is a common architecture pattern.

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
- minimal currency/quantity/time semantics without hardcoding.

Attack:
- charge succeeds/response lost;
- duplicate refund/webhook;
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
- Hugging Face object integrity/capacity report;
- ZITADEL/OpenFGA deployment backup/reprovision procedure appropriate to managed/self-hosted choice;
- provisional/final RPO/RTO;
- private recovery runbook exercise;
- operator ownership;
- new paid `IObjectStore`/`IBackupTarget` adapter readiness.

Gate:
- do not accept a paying customer while backup restore is untested;
- migrate bootstrap storage at the first paying customer or earlier when constraints require it;
- no HA/zero-downtime claim without proven topology;
- no simplification is accepted if it removes a core recovery/security/offline/control-plane behavior already relied on by the product.

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
- API-management platform selected before need;
- HTTP/gRPC between ordinary modules;
- database-per-service for the current modular monolith;
- MQTT/WebRTC/FTP/SFTP/raw TCP/UDP/gRPC without a concrete workload;
- denormalized authoritative core schema;
- global CRDTs;
- per-tenant schema/database/queue/stack by default;
- multi-currency/FX system;
- advanced peripheral suite;
- SaaS billing/ETL/search/MRP infrastructure without a current requirement.
