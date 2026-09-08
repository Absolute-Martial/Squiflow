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
- **Phase 1:** close ZITADEL Cloud vs self-hosted, instance/project/application layout, first OpenFGA store/model, model-ID rollout, and initial consistency/reconciliation policy.
- **Phase 2:** choose the Workstation local DB after the smallest SQLite/libSQL proof needed for a real local transaction.
- **Phase 3:** choose the initial central DB implementation capable of authoritative transaction + pooled isolation proof.
- **Phase 6:** choose only the background mechanism required by the first durable Worker workload. Decide from the real semantic need (single durable job, scheduled occurrence, multi-consumer event, or replayable stream); do not select a broker/event platform first and search for a use case afterward.
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
- Worker;
- Platform Admin Web;
- generic repository/unit-of-work hierarchy;
- one interface per class/provider API;
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
- minimal rack hardware inventory.

Attack:
- Workstation process exits unexpectedly while Guard survives;
- Guard exits while Workstation survives;
- both are terminated and restarted;
- incompatible Guard/Workstation protocol version;
- provider implementation accidentally leaks Hugging Face/Kaggle types into a business contract.

Gate:
- Web, Workstation, Guard and Core API build/run;
- Guard failure does not corrupt local business state;
- Guard can observe/recover Workstation process failure without owning business logic;
- `IObjectStore`/`IBackupTarget` are narrow enough to implement a second adapter later without mirroring whole third-party SDKs;
- no empty future project tree exists.

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
- durable/reconcilable authorization-change operation spanning SquiFlow DB/audit state and OpenFGA tuple write.

Do not:
- trust ZITADEL token roles as current SquiFlow authorization truth;
- let Desktop write OpenFGA tuples;
- equate a ZITADEL organization claim directly with SquiFlow TenantContext without server verification;
- put workflow/payment/stock arithmetic into OpenFGA.

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
- PII accidentally used in tuple IDs.

Gate:
- ZITADEL authenticates; OpenFGA authorizes; SquiFlow tenant/domain checks remain independent;
- role/grant UI reports applied only when intended OpenFGA state is known applied;
- ambiguous tuple writes have reconciliation, not guesswork;
- custom role does not require new authorization model deployment;
- no embedded reusable Workstation client secret.

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
- no network dependency for explicitly local-capable work.

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
- pooled tenant discriminator and provider-appropriate isolation proof;
- PostgreSQL RLS proof if PostgreSQL is selected;
- atomic business mutation + idempotency receipt + outbox where one store owns them;
- remote change feed + cursor;
- finite retry/backoff with an intentional retry owner for each remote path.

Attack:
- response lost after commit;
- duplicate same-intent command from caller retry;
- same key + changed intent;
- cross-tenant object/list/write attempt;
- valid OpenFGA permission but wrong TenantId/resource query;
- permission revoked while local operation pending;
- connection reused across tenants;
- partial batch failure;
- retry amplification across Workstation/API/provider layers.

Gate:
- no duplicate semantic effect;
- no cross-tenant leakage even if authorization relation exists incorrectly;
- stale Workstation permission snapshot is not server authority;
- central DB behavior proven against the real adapter;
- no system-wide `exactly once` claim is made from one local transaction guarantee.

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

## Phase 6 — Worker + Platform Admin when their first real jobs exist

Now create:

```text
services/worker
apps/admin-web
```

Deliver:
- first real background workload classified by trigger: user consequence, schedule, external system, batch/volume, or platform control;
- explicit message semantics: command/job versus committed event;
- the simplest durable execution pattern matching that workload;
- durable job/outbox consumption;
- if scheduled, a durable schedule occurrence identity/state before execution rather than `cron fired` as the only truth;
- if multiple consumers genuinely need one fact, explicit durable fan-out; otherwise do not add pub/sub;
- claims/leases where needed;
- bounded concurrency/fairness;
- retry/no-progress/quarantine;
- idempotent/reconcilable consumer behavior for producer retry, transport redelivery, and consumer crash after effect;
- one real long-running `202 Accepted` operation if justified;
- Platform Admin controls for implemented Worker/runtime controls;
- ZITADEL authentication + separate platform OpenFGA/application authorization model/scope as designed for platform operators;
- private infrastructure break-glass path when app control plane is unavailable.

Do not add Kafka/event streaming unless this phase proves an actual requirement for replay/history/independent consumer offsets that the normal durable job/outbox approach cannot satisfy.

Attack:
- same job produced twice after response loss;
- duplicate schedule firing;
- Worker crash before effect;
- Worker crash after effect but before acknowledgement;
- stale lease owner;
- repeated transport redelivery;
- `OutcomeUnknown`;
- one tenant flooding work;
- lower-priority work starvation;
- normal tenant user reaches platform route;
- ZITADEL-authenticated tenant user has no platform OpenFGA authority;
- Admin Web/Core API unavailable during recovery.

Gate:
- no generic force-success/mark-complete;
- tenant authorization cannot become platform authority;
- break-glass is infrastructure recovery, not hidden tenant API;
- one queue/broker feature is not described as end-to-end exactly-once;
- event/pub-sub/stream infrastructure exists only if the first real workload proves the matching semantic need.

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

## Phase 8 — API/observability/admin hardening

Deliver only hardening relevant to implemented surfaces:
- OpenTelemetry correlation;
- New Relic + Aiven OpenSearch export;
- Backtrace path where applicable;
- Guard lifecycle/crash/resource evidence correlation;
- bounded telemetry queues/spools;
- tenant/platform audit;
- high-risk exact-diff/step-up admin flows actually implemented;
- generated endpoint inventory;
- production CORS/cache/error/header policies;
- SSRF-safe outbound HTTP;
- dependency timeout/retry budgets;
- liveness/readiness/functional health.

Attack:
- applicable OWASP API cases;
- telemetry quota/export failure;
- retry storm;
- noisy tenant;
- sensitive content in logs/Guard diagnostics;
- OpenFGA or ZITADEL degraded/unavailable and resulting fail-closed/degraded behavior.

Gate:
- telemetry failure does not affect committed business truth;
- auth/authorization dependency failures never become accidental allow;
- no undocumented privileged endpoint.

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
- permission revoked just before sensitive action;
- OpenFGA allowed but business invariant denies;
- currency/rounding edge cases of supported model.

---

## Phase 10 — paying-customer production qualification and provider migration readiness

Deliver/prove:
- actual rack resource tests;
- Workstation + Guard soak/recovery tests;
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
- no simplification is accepted if it removes a core recovery/security/offline behavior already relied on by the product.

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
- global CRDTs;
- per-tenant schema/database/queue/stack by default;
- multi-currency/FX system;
- advanced peripheral suite;
- SaaS billing/ETL/search/MRP infrastructure without a current requirement.
