# Sequential Implementation Phases and Gates

**Version:** v0.0.15

Implementation is sequential. Default WIP limit: **one phase**.

For each phase:
1. close only the decisions required to start it;
2. implement one complete vertical slice;
3. run the relevant correctness/security/failure tests;
4. measure real behavior;
5. revise later assumptions only when evidence changes them.

Do not turn future architecture into Phase-0 scaffolding.

## Phase-start decisions

- **Phase 0:** no final DB, Worker, Admin Web, provider abstraction hierarchy or helper process is required.
- **Phase 1:** choose/configure the initial OIDC/session implementation needed by the identity slice.
- **Phase 2:** choose the Workstation local DB after the smallest SQLite/libSQL proof needed for a real local transaction.
- **Phase 3:** choose the initial central DB implementation/reference capable of proving the authoritative transaction + pooled isolation slice.
- **Phase 6:** choose only the background scheduling/messaging mechanism required by the first durable Worker workload.
- **Phase 7:** use the concrete bootstrap providers already chosen: private Hugging Face Storage Bucket + encrypted private Kaggle backup artifact path.
- **Before paying-customer production:** actual rack inventory/recovery, backup restore proof, provisional RPO/RTO, operator/break-glass access, printer support, and storage migration readiness must be known honestly.

---

## Phase 0 — smallest executable skeleton

Create only:

```text
apps/web          Blazor Web App
apps/desktop      Avalonia Workstation
services/core-api ASP.NET Core
modules/          only first-slice modules
infrastructure/   only current concrete integrations
 tests/ deploy/ docs/
```

Deliver:
- solution/build structure;
- basic CI;
- minimal error/result/execution context needed by the first slice;
- typed authoritative tenant-context boundary;
- architecture tests for real dependency boundaries that exist;
- basic executable health endpoint;
- minimal rack hardware inventory.

Do **not** create yet:
- `apps/admin-web`;
- `services/worker`;
- Guard/helper process;
- generic repository/unit-of-work projects;
- `packages/`, `contracts/`, `persistence/abstractions/` merely to match diagrams.

Gate:
- Web, Workstation and Core API build/run;
- CI runs useful fast tests;
- no provider type leaks into business/domain code where it does not belong;
- no empty future projects were scaffolded.

---

## Phase 1 — identity + smallest tenant

Deliver:
- canonical OIDC login;
- Owner tenant bootstrap;
- invite one Staff user;
- Web-only role/permission assignment;
- `(issuer, subject)` account mapping;
- Workstation system-browser Authorization Code + PKCE `S256` login;
- device enrollment/session context;
- authoritative membership → TenantContext resolution;
- effective permissions + `TenantAuthorizationRevision`;
- ASP.NET Core authorization requirements for the first real resource/action.

Attack:
- expired/duplicate invitation;
- forged/wrong issuer/audience token;
- PKCE/state/redirect tampering;
- client supplies another TenantId;
- custom-domain/Host confusion;
- Owner revokes Staff while Staff is logged in;
- Desktop attempts to change permissions.

Gate:
- Web is the only tenant permission-management surface;
- OIDC identity never substitutes for current SquiFlow authorization/tenant isolation;
- no embedded reusable Workstation secret.

---

## Phase 2 — first local-first Customer/Order slice

Deliver:
- minimal Customer/walk-in + Order journey;
- selected local DB;
- one atomic local business + outbox transaction;
- instant local UI result;
- restart recovery;
- stable semantic idempotency key per local operation;
- tenant default currency code used by the first monetary field rather than a hardcoded currency;
- monetary record keeps the applied currency code where it matters historically;
- ordinary Windows printing path only if this first slice needs printing.

Do **not** add:
- Guard/helper process;
- generic Money/FX framework;
- generic repository/unit-of-work interface hierarchy.

Attack:
- process termination after commit;
- lost in-memory sync wakeup;
- disk full/DB locked;
- duplicate local scheduling;
- sign-out/restart with pending work;
- wrong local clock;
- printer unavailable after a committed business transaction where printing is used.

Gate:
- committed local work survives restart;
- no network dependency for the explicitly local-capable operation;
- currency is configuration/data, not hardcoded application behavior.

---

## Phase 3 — authoritative sync + central DB + pooled isolation

Deliver:
- bounded sync upload;
- per-item semantic idempotency;
- authoritative tenant derivation;
- tenant-scoped resource lookup;
- resource/action authorization;
- explicit request/response contracts;
- expected-version concurrency;
- central DB adapter selected for this slice;
- pooled tenant discriminator;
- provider-appropriate isolation proof;
- PostgreSQL RLS proof if PostgreSQL is used;
- atomic business mutation + receipt + outbox where one store owns them;
- remote change feed + cursor;
- finite retry/backoff.

Attack:
- response lost after server commit;
- duplicate same-intent command;
- same key + changed intent;
- cross-tenant object/list/write attempt;
- connection reused across tenants;
- permission revoked while local operation is pending;
- partial batch failure;
- retry amplification.

Gate:
- no duplicate effect;
- no cross-tenant leakage;
- stale Workstation permission snapshot is not server authority;
- central DB behavior is proven with the real adapter, not an in-memory substitute.

---

## Phase 4 — conflict and long-offline recovery

Deliver:
- one real aggregate conflict UX;
- protocol/schema version negotiation;
- reauth/upgrade/resnapshot/rebase path;
- preservation of pending local intent;
- tombstone/change-history retention policy for the supported offline window;
- large-backlog transfer policy.

Attack:
- weeks/months offline;
- old protocol/schema/rules/permissions;
- deleted/merged remote entity;
- large pending backlog;
- slow connection;
- local disk nearly full during recovery.

Gate:
- no silent pending-work deletion;
- client older than retained incremental history gets explicit resnapshot/rebase rather than fake success.

---

## Phase 5 — one rule + workflow + dynamic form

Deliver:
- one bounded tenant rule;
- fact-authority classification for that rule;
- one configurable workflow stage/transition;
- one bounded versioned form;
- Web-only authoring/publication;
- immutable compatible snapshot to Workstation/API;
- decision trace;
- one semantic authorization requirement for the transition.

Attack:
- invalid/conflicting rule;
- stale central fact used offline;
- form/workflow definition changes while old instances exist;
- stage retired/renamed;
- approver permission revoked;
- simultaneous conflicting transitions;
- two-person approval deadlock.

Gate:
- previous published version survives failed publication;
- old instances remain explainable;
- local evaluation cannot make server-required facts authoritative.

---

## Phase 6 — create Worker + Platform Admin only when needed

Now create:

```text
services/worker
apps/admin-web
```

because this phase introduces their first real jobs/control flows.

Deliver:
- durable job/outbox consumption;
- claims/leases where needed;
- idempotent/reconcilable effects;
- bounded concurrency/fairness;
- retry/no-progress/quarantine;
- one real long-running `202 Accepted` operation if justified;
- Platform Admin controls for the implemented Worker/control case;
- private infrastructure recovery runbook for app/control-plane outage;
- first external notification/webhook only if the complete journey needs one.

Attack:
- Worker crash before/after external effect;
- stale lease owner;
- repeated retry;
- `OutcomeUnknown`;
- one tenant flooding expensive work;
- normal user guessing platform-admin endpoint;
- Admin Web/Core API unavailable during recovery.

Gate:
- no generic force-success/mark-complete controls;
- application control is Platform-Admin-Web only during normal operation;
- private recovery path is not a tenant/Desktop API.

---

## Phase 7 — Hugging Face files + documents + printing + Kaggle backup proof

Deliver:
- private Hugging Face Storage Bucket integration contained in infrastructure code;
- object metadata/key/hash/lifecycle in business DB;
- ~100 GB capacity measurement/admission behavior;
- local attachment staging and upload/retry;
- immutable/versioned application object keys for historical/issued objects;
- document generation needed by the implemented journey;
- in-process Windows printing path;
- encrypted opaque Kaggle backup artifact upload/download proof.

Do **not** create `IObjectStorage` merely because migration is planned. Extract a migration seam only when the paid-provider migration begins or another real implementation coexists.

Kaggle backup flow:

```text
required state
→ package/compress locally
→ authenticated encryption locally
→ opaque .sqfbak + checksum
→ private Kaggle Dataset version
→ download + verify
→ restore test
```

Attack:
- object succeeds/metadata fails;
- metadata exists/object missing;
- cross-tenant object reference;
- storage nears hard capacity;
- large upload interrupted;
- printer/spooler failure after commit;
- Kaggle backup corrupt/missing/wrong key;
- raw customer data accidentally selected for direct Kaggle upload.

Gate:
- no silent deletion of retained business objects;
- raw readable customer backups never go to Kaggle;
- one encrypted off-site backup can actually be restored;
- large transfer does not starve ordinary sync/API traffic.

---

## Phase 8 — API/observability/admin hardening

Deliver only the hardening relevant to implemented surfaces:
- OpenTelemetry correlation;
- New Relic + Aiven OpenSearch export;
- Backtrace path where applicable;
- bounded telemetry queues/spools;
- tenant/platform audit;
- high-risk exact-diff/step-up admin flows actually implemented;
- generated endpoint inventory;
- production CORS/cache/error/header policies;
- SSRF-safe outbound HTTP before user-configured remote URLs;
- dependency timeout/retry budgets;
- liveness/readiness/functional health.

Attack:
- applicable OWASP API authorization/resource/SSRF/resource-consumption cases;
- telemetry quota/export failure;
- retry storm;
- noisy tenant;
- sensitive content in logs/diagnostics.

Gate:
- telemetry failure does not affect committed business truth;
- no undocumented privileged production endpoint.

---

## Phase 9 — payments/credit/inventory hardening

Deliver:
- payment `OutcomeUnknown` semantics;
- provider-effect idempotency/reconciliation;
- refund/reversal/correction;
- inventory concurrency policy;
- credit authority;
- owner/manual price permission/audit;
- use of the minimal shared currency/quantity/time semantics rather than hardcoded/ad-hoc values.

Do not add FX/multi-currency behavior unless the implemented customer journey requires it.

Attack:
- charge succeeds/response lost;
- duplicate refund/webhook;
- concurrent last-stock sale;
- stale offline credit;
- currency/rounding edge cases of the supported single-currency-per-record model;
- permission revoked before deferred sensitive action.

---

## Phase 10 — paying-customer production qualification

Deliver/prove:
- actual rack resource tests;
- node/SPOF inventory;
- restart/disk-full/recovery behavior;
- installer/update/rollback;
- backup restore onto replacement environment;
- encrypted Kaggle backup key recovery;
- Hugging Face object integrity/capacity report;
- provisional/final RPO/RTO;
- private recovery runbook exercise;
- operator ownership;
- migration readiness from Hugging Face/Kaggle bootstrap storage to purpose-built paid providers.

Gate:
- do not accept a paying customer while the system still depends on an untested backup restore;
- planned storage migration occurs at the first paying customer, or earlier when constraints already require it;
- no HA/zero-downtime claim without a topology that actually proves it.

---

## Deferred

Do not spend baseline work on:
- dedicated accessibility/a11y program;
- Guard/supervisor/helper process without proven isolation need;
- generic repository/unit-of-work/provider abstractions;
- browser offline/PWA business sync;
- Kafka/mandatory Redis;
- full CQRS/event sourcing/Saga;
- global CRDTs;
- Zanzibar-style authorization service;
- per-tenant schema/database/queue/stack by default;
- multi-currency/FX system;
- advanced peripheral suite;
- SaaS billing/ETL/search/MRP infrastructure without a current requirement.
