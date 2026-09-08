# Sequential Implementation Phases and Gates

**Version:** v0.0.15

SquiFlow should not open many parallel incomplete tracks. Each phase proves a vertical slice and its failure/recovery behavior before the next dependent phase expands.

## Phase 0 — repository/runtime skeleton

Deliver:
- `apps/web`, `apps/admin-web`, `apps/desktop`;
- `services/core-api`, `services/worker`;
- business modules/packages/persistence abstractions;
- error/result/execution-context contracts;
- typed immutable request/application `TenantContext` contract;
- tenant-owned vs platform-global data classification convention;
- architecture dependency tests;
- basic CI/build/test/package pipeline;
- generated API endpoint inventory from executable endpoint metadata/OpenAPI.

Gate:
- each runtime builds separately;
- forbidden dependencies are caught;
- no provider/reference project leaks into domain/application contracts;
- tenant-owned repository/query contracts cannot casually omit tenant context;
- every externally reachable endpoint declares audience/owner/policy/version metadata;
- development/debug endpoints are absent or inaccessible in production configuration.

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
- simple server resource/action authorization using `IAuthorizationService`.

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
- handler-order assumption or a requirement handler performing a business side effect.

Gate:
- only Web administration can modify grants;
- identity token proves authentication but does not act as current SquiFlow permission or tenant-isolation truth;
- native client has no embedded reusable secret;
- server authorization rejects stale/forged/cross-tenant authority;
- redirect/issuer validation fails closed;
- TenantContext comes from authoritative membership/placement data, not untrusted client input;
- authorization handlers are side-effect free and do not depend on invocation order.

## Phase 2 — first local-first Workstation transaction

Deliver minimal Customer/Walk-in + Order slice:

```text
UI
→ local validation
→ local durable business + outbox transaction
→ immediate local result
```

Every local outbox item receives a stable semantic idempotency key separate from transport/message IDs.

Attack:
- power loss after commit;
- lost Channel signal;
- app restart;
- disk full;
- local DB busy/locked;
- user closes app immediately;
- same local intent gets scheduled for upload twice.

Gate:
- user work survives crashes;
- no server/network dependency for the approved local operation;
- duplicate scheduling cannot create a second semantic operation.

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
- finite retry classification and `Retry-After` handling.

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
- RLS/policy missing or disabled on a tenant-owned reference table.

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
- retry is finite, classified and budgeted.

## Phase 4 — conflict + long-offline

Deliver:
- one aggregate conflict UX;
- protocol/schema version negotiation;
- reauth/upgrade/resnapshot/repair paths;
- pending local work preservation;
- effective-permission snapshot revision refresh;
- idempotency retention policy suitable for long-offline Workstation retries.

Attack:
- weeks/months offline;
- tombstone expired;
- entity deleted/merged remotely;
- 10k+ pending changes;
- old rule snapshot;
- old permission snapshot after Owner revoked authority;
- very late retry arrives after the original operation succeeded and later business state changed.

Gate:
- no silent local data discard;
- server does not accept a stale Workstation permission snapshot as authority;
- late idempotent retry cannot recreate an already-completed business effect.

## Phase 5 — native rules + workflow

Deliver:
- one tenant rule;
- one configurable tenant workflow stage;
- one approval transition;
- Web-only authoring/publication;
- immutable effective snapshot to API/Workstation;
- decision trace;
- semantic resource authorization requirement for the approval action.

Attack:
- invalid rule;
- conflicting rules;
- workflow definition changes while instance active;
- approver loses permission;
- two approvers act simultaneously;
- approver changes resource ID/tenant context;
- old permission cache after revocation.

Gate:
- last known good rule/workflow remains usable if publication fails;
- authorization and workflow/domain validity are separate checks.

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
- duplicate async command with the same idempotency key returns the existing operation resource instead of enqueuing duplicate work.

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
- job TenantId and referenced business entity belong to different tenants.

Gate:
- critical Worker/server controls are reachable only from Platform Admin Web;
- no generic force-complete operation;
- committed consequences are not incorrectly cancelled by later actor revocation;
- deferred actor actions reauthorize where their semantics require it;
- third-party responses are bounded/validated and timeout-controlled;
- async command retries return one operation identity;
- priority/fairness policy prevents indefinite starvation;
- pooled processing enforces tenant-aware admission without requiring one physical queue per tenant.

## Phase 7 — files/documents/printing

Deliver:
- local attachment staging;
- object upload + metadata lifecycle;
- tenant-scoped object metadata/key policy;
- document helper where isolation justified;
- printing status independent from transaction status;
- upload/request size and processing budgets;
- claim-check/reference messages for large payloads rather than putting full files in durable queue envelopes;
- narrowly scoped signed upload/download capability only if provider/flow proves it simpler and safer than proxying all bytes through Core API.

Attack:
- object succeeds/metadata fails;
- metadata succeeds/object missing;
- Tenant A attempts to fetch Tenant B object by guessed key/reference;
- printer out of paper;
- helper crashes;
- disk full during staging;
- oversized/decompression-bomb input;
- malicious filename/content-type mismatch;
- signed object URL has too broad scope or excessive lifetime.

## Phase 8 — API reliability + observability/admin hardening

Deliver:
- OpenTelemetry traces/metrics/log correlation;
- tenant-safe telemetry dimensions and per-tenant workload/backlog evidence where operationally appropriate;
- New Relic + Aiven OpenSearch export;
- Backtrace crash path;
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
- queue age/oldest-item and completion latency metrics, not only queue depth/start counters.

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
- health endpoint says live while a critical dependency makes the node not ready.

Gate:
- telemetry failure never invalidates a business transaction;
- sensitive data is redacted/bounded;
- privileged API inventory has no undocumented production endpoint;
- applicable OWASP attack tests pass;
- retry volume cannot grow without aggregate budget bounds;
- noisy-tenant behavior is measurable and bounded before dedicated processing is considered;
- health/readiness correctly remove unhealthy nodes without exposing privileged diagnostics.

## Phase 9 — payments/credit/inventory hardening

Deliver:
- `OutcomeUnknown` payment state;
- provider idempotency/effect receipt linkage;
- refund/reversal/correction;
- inventory concurrency policy;
- current credit exposure authority;
- owner/manual price permissions/audit;
- semantic authorization requirements for refund/adjustment/approval actions.

Attack:
- provider charge succeeds but response lost;
- concurrent last-stock sale;
- offline stale credit exposure;
- duplicate refund/webhook;
- same payment/refund idempotency key reused with changed amount/target;
- ordinary Staff attempts refund/credit override by object ID, hidden property or guessed endpoint;
- permission revoked before a deferred sensitive action executes.

## Phase 10 — release/resource/recovery qualification

Deliver:
- Workstation resource/idle tests;
- constrained single-node server tests;
- installer/update/rollback;
- schema/protocol/rule/config/message compatibility;
- backup/restore drill;
- long-running leak/soak tests;
- cross-tenant negative isolation suite in release CI;
- privileged backup/migration isolation tests;
- authorization-revision/cache invalidation tests if a permission cache exists;
- OIDC/session/logout/version-skew tests for selected identity implementation;
- idempotency retention/cleanup test;
- retry-storm/queue-backlog/load-shedding tests;
- restore validation across DB/object/job/idempotency state where required.

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
- advanced manufacturing/MRP/wastage;
- specialized search infrastructure without workload evidence.
