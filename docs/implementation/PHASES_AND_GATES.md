# Sequential Implementation Phases and Gates

**Version:** v0.0.15

SquiFlow should not open many parallel incomplete tracks. Each phase proves a vertical slice and its failure/recovery behavior before the next dependent phase expands.

## Implementation-control rule

The architecture is detailed enough to begin implementation. Do not require every future enterprise decision to be solved before Phase 0.

Use a default **WIP limit of one implementation phase**:

1. close only the decisions needed to start the current phase;
2. implement one complete vertical slice;
3. run the defined failure/security/accessibility gates;
4. record measured evidence;
5. update later assumptions only when evidence changes them.

Additional source/architecture review is useful when it closes a current risk. It must not become a reason to avoid executable Phase 0 work.

## Phase-start decision gates

- **Phase 0:** no final DB/provider is required. Avalonia and Blazor Web App are already accepted. Establish solution/build/CI/dependency boundaries and actual deployment-hardware inventory.
- **Phase 1:** choose/configure the initial standards-compliant OIDC provider/session implementation needed for the identity slice; exact future federation can remain open.
- **Phase 2:** select the initial local-store adapter for the vertical slice after the SQLite/libSQL proof needed to commit real local work.
- **Phase 3:** select an initial central-store implementation/reference adapter capable of proving the authoritative transaction + pooled isolation slice. Long-term provider portability can remain an architecture boundary; the running slice still needs a real DB.
- **Phase 6:** choose only the messaging/scheduling mechanism needed by the first durable Worker path; do not preselect a stack of buses/schedulers.
- **Phase 7:** use the currently available S3/object-storage environment through the provider-neutral abstraction for qualification unless evidence requires another provider/tier; the current ~100 GB capacity is a hard planning constraint.
- **Before production:** close backup target/RPO/RTO, physical node inventory/power policy, initial printer support, infrastructure break-glass path and formal accessibility/compliance targets that apply.

## Cross-phase gates

Every phase that adds UI must satisfy the relevant accessibility requirements in `docs/ux/ACCESSIBILITY_AND_INTERACTION_QUALITY.md`.

Every phase that changes data/runtime behavior must identify the test layer/evidence category in `docs/testing/VERIFICATION_STRATEGY.md`.

Every phase must keep actual low-end hardware/resource limits in mind; cloud-style failover/autoscaling is never assumed merely because server processes are stateless.

## Phase 0 — repository/runtime skeleton

Deliver:
- `apps/web` using the accepted Blazor Web App foundation;
- `apps/admin-web` as a separate Blazor Web App/security surface;
- `apps/desktop` using the accepted Avalonia foundation;
- `services/core-api`, `services/worker`;
- business modules/packages/persistence abstractions;
- error/result/execution-context contracts;
- typed immutable request/application `TenantContext` contract;
- tenant-owned vs platform-global data classification convention;
- architecture dependency tests;
- basic CI/build/test/package pipeline;
- generated API endpoint inventory from executable endpoint metadata/OpenAPI;
- minimal actual deployment inventory for the available rack nodes (CPU/RAM/storage/network/role/SPOF) without pretending this is HA design.

Gate:
- each runtime builds separately;
- forbidden dependencies are caught;
- no provider/reference project leaks into domain/application contracts;
- tenant-owned repository/query contracts cannot casually omit tenant context;
- every externally reachable endpoint declares audience/owner/policy/version metadata;
- development/debug endpoints are absent or inaccessible in production configuration;
- README clearly distinguishes current implemented files from target architecture;
- CI runs fast deterministic/unit/architecture tests;
- no empty placeholder project tree is created beyond what the first slices actually need.

## Phase 1 — identity + smallest tenant

Deliver:
- configured canonical OpenID Connect identity authority;
- Owner tenant bootstrap;
- invite one Staff user;
- Web-only role/permission assignment;
- stable external account mapping using `(issuer, subject)`;
- Workstation system-browser Authorization Code + PKCE `S256` login/device enrollment;
- trusted issuer Discovery validation;
- exact registered Web/custom-domain redirect handling;
- authoritative membership → TenantContext resolution;
- effective permission retrieval with `TenantAuthorizationRevision`;
- ASP.NET Core policy/requirement registration;
- simple server resource/action authorization using `IAuthorizationService`;
- accessible login/error/team/role interaction for the implemented journey;
- explicit device enrollment identity distinct from user membership.

Attack:
- invitation expires;
- Owner removes/suspends Staff;
- permission changes while Staff is logged in;
- attempt to change permissions from Desktop;
- attempt to grant beyond delegation/tenant entitlement;
- forged/expired/wrong-audience ID token;
- wrong issuer/discovery metadata;
- authorization response replay/state mismatch;
- PKCE verifier mismatch/downgrade attempt;
- open redirect/return URL attempt;
- email changes while subject remains the same;
- login/recovery brute force according to the chosen identity-provider boundary;
- client supplies another TenantId after authenticating;
- custom-domain/Host value attempts to manufacture another TenantContext;
- handler-order assumption or a requirement handler performing a business side effect;
- keyboard-only login/role-management journey;
- session expires while a role form is open;
- lost/revoked device attempts to reuse credentials.

Gate:
- only Web administration can modify grants;
- identity token proves authentication but does not act as current SquiFlow permission or tenant-isolation truth;
- native client has no embedded reusable secret;
- server authorization rejects stale/forged/cross-tenant authority;
- redirect/issuer validation fails closed;
- TenantContext comes from authoritative membership/placement data, not untrusted client input;
- authorization handlers are side-effect free and do not depend on invocation order;
- implemented UI journey passes accessibility smoke tests.

## Phase 2 — first local-first Workstation transaction

Deliver minimal Customer/Walk-in + Order slice:

```text
UI
→ local validation
→ local durable business + outbox transaction
→ immediate local result
```

Every local outbox item receives a stable semantic idempotency key separate from transport/message IDs.

Also deliver:
- initial `SquiFlow.Guard` supervision skeleton with no business logic;
- local disk/staging usage measurement;
- explicit shared-PC/session policy for the supported scenario;
- the first cross-cutting Money/Time/identifier primitives needed by the order slice rather than raw unqualified numeric/date strings.

Attack:
- power loss after commit;
- lost Channel signal;
- app restart;
- disk full/low disk;
- local DB busy/locked;
- user closes app immediately;
- same local intent gets scheduled for upload twice;
- Guard crashes/restarts independently;
- user signs out with pending durable work;
- update/restart begins with pending outbox;
- wrong device clock/timezone;
- keyboard-only customer/order creation and validation.

Gate:
- user work survives crashes;
- no server/network dependency for the approved local operation;
- duplicate scheduling cannot create a second semantic operation;
- no restart loop is introduced by Guard;
- unsynced business work is not deleted on sign-out/update;
- Workstation resource behavior is measured on a low-spec supported machine rather than only a developer workstation.

## Phase 3 — authoritative synchronization + tenant isolation + object authorization + idempotent API

Deliver:
- bounded upload batch;
- item-level semantic idempotency key;
- durable idempotency receipt;
- canonical request fingerprint/semantic comparison sufficient to reject same key + different intent;
- server tenant derivation/authorization;
- coarse endpoint/function policy;
- tenant-scoped resource resolution;
- resource/action authorization;
- explicit request/response DTO allowlists;
- expected-version/concurrency contract;
- pooled tenant-owned central schema/model with explicit tenant discriminator;
- tenant-aware uniqueness/index conventions;
- provider-specific pooled-isolation adapter proof;
- PostgreSQL reference POC with RLS if PostgreSQL remains the reference candidate;
- one authoritative central transaction;
- idempotency receipt + business mutation + audit/outbox atomically when owned by the same store;
- per-item result;
- remote change feed + cursor;
- local result/cursor durability;
- finite retry classification and `Retry-After` handling;
- bandwidth-aware sync batch sizing so low uplink capacity is not ignored.

Attack:
- server commits, response lost;
- duplicate request with same key/same payload;
- same key with changed semantic parameters;
- duplicate item arrives in a different transport batch;
- permission revoked while pending;
- malformed/tampered tenant ID;
- partial batch failure;
- substitute another tenant's object ID;
- list/search/report/export omits explicit tenant filtering;
- background job receives mismatched TenantId/resource;
- change HTTP method/path to reach a privileged function;
- add hidden/privileged JSON properties;
- request sensitive response fields without permission;
- stale `TenantAuthorizationRevision` snapshot;
- nested client/API/SDK retries amplify one dependency failure;
- connection pool reuses a connection after Tenant A and serves Tenant B;
- PostgreSQL reference runtime connects as table owner/superuser/`BYPASSRLS` role;
- write attempts create/update a row under another TenantId;
- RLS/policy missing or disabled on a tenant-owned reference table;
- reconnecting Workstations saturate the limited uplink/API/DB connection pool.

Gate:
- no duplicate business effect;
- duplicate same-intent request receives the same/semantically equivalent result;
- same key + changed intent is rejected;
- no cursor advancement before local apply;
- explicit `AuthorizationChanged`/conflict states;
- BOLA/BFLA/property-level authorization tests pass;
- cross-tenant existence/data is not exposed through unrestricted resource lookup;
- pooled tenant isolation fails closed under the selected/reference persistence design;
- runtime connection pooling cannot carry Tenant A isolation context into Tenant B;
- PostgreSQL RLS proof, if used, covers reads and writes with a non-bypass runtime identity;
- retry is finite, classified and budgeted;
- actual low-end server hardware can sustain the target slice within defined headroom.

## Phase 4 — conflict + long-offline

Deliver:
- one aggregate conflict UX;
- protocol/schema version negotiation;
- reauth/upgrade/resnapshot/repair paths;
- pending local work preservation;
- effective-permission snapshot revision refresh;
- idempotency retention policy suitable for long-offline Workstation retries;
- tombstone/compaction retention policy tied to the supported offline window;
- large backlog/resnapshot transfer policy that does not starve normal traffic.

Attack:
- weeks/months offline;
- tombstone expired;
- entity deleted/merged remotely;
- 10k+ pending changes;
- old rule snapshot;
- old permission snapshot after Owner revoked authority;
- very late retry arrives after the original operation succeeded and later business state changed;
- large resnapshot on a slow connection;
- local disk almost full while resnapshot/pending work coexist.

Gate:
- no silent local data discard;
- server does not accept a stale Workstation permission snapshot as authority;
- late idempotent retry cannot recreate an already-completed business effect;
- recovery path is explicit when a client is older than supported tombstone/protocol history;
- small semantic sync remains usable during large attachment/backlog transfer.

## Phase 5 — native rules + workflow + one dynamic form

Deliver:
- one tenant rule;
- fact authority/freshness classification for facts used by the rule (`LocalSafe`, `LocalProvisional`, `ServerRequired` semantics);
- one configurable tenant workflow stage;
- one approval transition;
- one bounded/versioned dynamic form definition used by that journey;
- Web-only authoring/publication;
- immutable effective snapshot to API/Workstation;
- decision trace;
- semantic resource authorization requirement for the approval action;
- accessible generated form labels/validation/focus semantics.

Attack:
- invalid rule;
- conflicting rules;
- rule uses stale central credit/stock fact offline;
- dynamic form definition removes/changes a field while an old draft/instance exists;
- workflow definition changes while instance active;
- stage is renamed/retired while old instance references it;
- approver loses permission;
- two approvers act simultaneously;
- only configured approver is unavailable in a two-person tenant;
- approver changes resource ID/tenant context;
- old permission cache after revocation.

Gate:
- last known good rule/workflow/form remains usable if publication fails;
- authorization and workflow/domain validity are separate checks;
- Workstation cannot make a ServerRequired fact authoritative offline;
- old workflow/form instances remain explainable through pinned/versioned definitions;
- generated form is keyboard/screen-reader usable for the implemented field types.

## Phase 6 — Worker, long-running HTTP and platform control plane

Deliver:
- durable job/outbox;
- TenantId on tenant-owned jobs;
- Worker claim/lease/retry;
- idempotent consumer/effect contract;
- bounded global and tenant-aware concurrency/admission;
- fairness/aging so one tenant/tier cannot starve others;
- no-progress detection;
- pause/drain/resume;
- quarantine/reconciliation;
- Platform Admin Web controls;
- explicit job authorization/execution classification:
  - committed business consequence;
  - deferred actor action;
  - platform-control command;
- one long-running API using `202 Accepted` + durable operation/status resource + `Location` and appropriate `Retry-After`;
- duplicate async command with the same idempotency key returns the existing operation resource instead of enqueuing duplicate work;
- the private infrastructure break-glass runbook needed when Admin Web/Core API cannot serve normal control-plane commands;
- first external-delivery capability only if a complete journey actually needs notification/webhook delivery.

Attack:
- Worker crashes after external effect;
- stale lease owner resumes;
- admin retries repeatedly;
- pause during active job;
- external outcome unknown;
- actor permission revoked after enqueue;
- provider response malformed/oversized/slow;
- normal tenant user guesses a platform-admin endpoint;
- client loses the initial `202` response and retries the POST;
- status item is stuck in Running with no progress;
- queue contains high-priority work continuously and starves lower-priority work;
- one tenant floods expensive jobs and attempts to consume the entire worker/provider budget;
- job TenantId and referenced business entity belong to different tenants;
- Admin Web/Core API is unavailable and operator must use the private recovery path;
- telemetry/provider outage occurs during recovery.

Gate:
- critical Worker/server controls are reachable only from Platform Admin Web in normal operation;
- private break-glass recovery cannot be invoked from tenant/Workstation business APIs;
- no generic force-complete operation;
- committed consequences are not incorrectly cancelled by later actor revocation;
- deferred actor actions reauthorize where their semantics require it;
- third-party responses are bounded/validated and timeout-controlled;
- async command retries return one operation identity;
- priority/fairness policy prevents indefinite starvation;
- pooled processing enforces tenant-aware admission without requiring one physical queue per tenant.

## Phase 7 — files/documents/printing and device side effects

Deliver:
- local attachment staging;
- object upload + metadata lifecycle;
- tenant-scoped object metadata/key policy;
- explicit current ~100 GB object-capacity accounting by class/tenant;
- object retention/quota/admission behavior;
- backup storage kept conceptually separate from primary object capacity;
- document helper where isolation justified;
- printer adapter/attempt model according to `docs/workstation/GUARD_AND_DEVICE_INTEGRATION.md`;
- printing status independent from transaction status;
- upload/request size and processing budgets;
- claim-check/reference messages for large payloads rather than putting full files in durable queue envelopes;
- narrowly scoped signed upload/download capability only if provider/flow proves it simpler and safer than proxying all bytes through Core API.

Attack:
- object succeeds/metadata fails;
- metadata succeeds/object missing;
- Tenant A attempts to fetch Tenant B object by guessed key/reference;
- object storage approaches hard capacity;
- backup/diagnostic/export competes with retained customer objects;
- network fails mid-upload/resume;
- printer out of paper/offline/removed;
- spooler accepts job but physical output is unknown;
- alternate printer retry;
- helper crashes/hangs;
- disk full during staging;
- oversized/decompression-bomb input;
- malicious filename/content-type mismatch;
- signed object URL has too broad scope or excessive lifetime.

Gate:
- capacity exhaustion never silently deletes authoritative customer objects;
- optional heavy work is rejected/deferred before completely exhausting capacity;
- a printer/device failure cannot undo committed business truth;
- large transfers do not starve interactive API/sync traffic;
- object/metadata/backup restore mismatch has an explicit recovery path.

## Phase 8 — API reliability + observability/admin/accessibility hardening

Deliver:
- OpenTelemetry traces/metrics/log correlation;
- tenant-safe telemetry dimensions and per-tenant workload/backlog evidence where operationally appropriate;
- New Relic + Aiven OpenSearch export;
- Backtrace crash path;
- bounded telemetry spool/buffer and provider-quota health;
- tenant/platform audit;
- step-up and exact-diff high-risk admin workflows;
- generated endpoint/version inventory and deprecation/retirement check;
- production CORS/cache/security-header/error policy;
- endpoint/work-class resource budgets;
- SSRF-safe outbound HTTP policy before enabling user-configured webhooks/remote fetches;
- dependency-specific timeout/retry policies;
- retry budgets and retry telemetry;
- circuit breaker only for dependencies where persistent/slow failure proves retry alone harmful;
- liveness/readiness/functional health separation;
- queue age/oldest-item and completion latency metrics, not only queue depth/start counters;
- accessibility pass across all implemented Web/Admin/Workstation journeys;
- support-facing root-cause evidence path for the implemented failure classes.

Attack against applicable OWASP API Security Top 10 categories and reliability cases:
- object/function/property authorization bypass;
- auth/recovery abuse;
- expensive single-request resource exhaustion;
- excessive provider-cost operations;
- one tenant creates sustained noisy-neighbor pressure;
- automated abuse of a sensitive business flow;
- SSRF/private-network/metadata target;
- stale/beta/debug API version;
- unsafe cache/CORS/error configuration;
- malicious/unexpected third-party provider response;
- dependency returns 429/503 for a sustained period;
- many callers synchronize retries into a thundering herd;
- retry at several layers multiplies calls;
- cache unavailable or stale;
- health endpoint says live while a critical dependency makes the node not ready;
- telemetry provider quota exhausted or exporter unavailable;
- local telemetry spool approaches disk limit;
- tenant/customer content leaks into logs/diagnostics;
- keyboard/focus/screen-reader failure on high-risk Admin flow.

Gate:
- telemetry failure never invalidates a business transaction;
- sensitive data is redacted/bounded;
- privileged API inventory has no undocumented production endpoint;
- applicable OWASP attack tests pass;
- retry volume cannot grow without aggregate budget bounds;
- noisy-tenant behavior is measurable and bounded before dedicated processing is considered;
- health/readiness correctly remove unhealthy nodes without exposing privileged diagnostics;
- managed observability capacity exhaustion is visible and bounded;
- accessibility core-journey checks pass.

## Phase 9 — payments/credit/inventory hardening

Deliver:
- `OutcomeUnknown` payment state;
- provider idempotency/effect receipt linkage;
- refund/reversal/correction;
- inventory concurrency policy;
- current credit exposure authority;
- owner/manual price permissions/audit;
- semantic authorization requirements for refund/adjustment/approval actions;
- use of the shared money/currency/quantity/time primitives rather than module-specific ad hoc types.

Attack:
- provider charge succeeds but response lost;
- concurrent last-stock sale;
- offline stale credit exposure;
- duplicate refund/webhook;
- same payment/refund idempotency key reused with changed amount/target;
- rounding/allocation edge cases;
- timezone/business-date boundary;
- ordinary Staff attempts refund/credit override by object ID, hidden property or guessed endpoint;
- permission revoked before a deferred sensitive action executes.

## Phase 10 — release/resource/recovery qualification

Deliver:
- Workstation resource/idle tests;
- actual rack-node constrained server tests, not only an abstract `8 GB server` label;
- final deployment inventory/SPOF map;
- UPS/power-loss policy evidence according to selected hardware;
- installer/update/rollback;
- schema/protocol/rule/config/message compatibility;
- independent-enough backup target/configuration;
- DB + object + job/idempotency/config restore drill onto replacement hardware;
- provisional/final RPO/RTO recorded honestly;
- local/object storage capacity thresholds/alerts;
- bandwidth/backlog test;
- long-running leak/soak tests;
- cross-tenant negative isolation suite in release CI;
- privileged backup/migration isolation tests;
- authorization-revision/cache invalidation tests if a permission cache exists;
- OIDC/session/logout/version-skew tests for selected identity implementation;
- idempotency retention/cleanup test;
- retry-storm/queue-backlog/load-shedding tests;
- infrastructure break-glass runbook exercise;
- operator/alert ownership confirmed.

Gate:
- no claim of automatic HA/zero downtime unless the actual topology proves it;
- restore proves usable tenant-isolated business state rather than only `database restored`;
- primary object capacity and backup retention fit the available storage plan;
- a failed physical node has a documented recovery/replacement path;
- release qualification results identify the exact hardware/configuration used.

## Deferred until baseline proves itself

Do not spend baseline implementation effort on:
- full browser offline/PWA business sync;
- Kafka/event-log infrastructure;
- YugabyteDB;
- mandatory Redis/shared cache;
- microservice-per-module extraction;
- per-frontend BFF services;
- full CQRS dual-store architecture;
- event-sourced authoritative persistence;
- Saga as the default business transaction model;
- global CRDT data model;
- sharding;
- leader-election infrastructure where atomic claims/leases suffice;
- deployment stamps/geode/active-active multi-region;
- schema-per-tenant baseline;
- database-per-tenant baseline;
- physical queue/worker pool per tenant baseline;
- dedicated stack per tenant baseline;
- per-tenant cloud account/VPC infrastructure;
- full HATEOAS;
- Zanzibar-style authorization service/relation-tuple graph/specialized set index;
- Dynamic OpenID Connect Client Registration;
- OpenID Native SSO for Mobile Apps as a Windows login mechanism;
- full SaaS billing/metering engine without a commercial requirement;
- advanced peripheral suite without confirmed journeys;
- specialized import/ETL platform before onboarding data proves the need;
- advanced manufacturing/MRP/wastage;
- specialized search infrastructure without workload evidence.
