# Sequential Implementation Phases and Gates

**Version:** v0.0.15

SquiFlow should not open many parallel incomplete tracks. Each phase proves a vertical slice and its failure/recovery behavior before the next dependent phase expands.

## Phase 0 — repository/runtime skeleton

Deliver:
- `apps/web`, `apps/admin-web`, `apps/desktop`;
- `services/core-api`, `services/worker`;
- business modules/packages/persistence abstractions;
- error/result/execution-context contracts;
- architecture dependency tests;
- basic CI/build/test/package pipeline;
- generated API endpoint inventory from executable endpoint metadata/OpenAPI.

Gate:
- each runtime builds separately;
- forbidden dependencies are caught;
- no provider/reference project leaks into domain/application contracts;
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
- effective permission retrieval with `TenantAuthorizationRevision`;
- simple server resource/action authorization using ASP.NET Core `IAuthorizationService`.

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
- login/recovery brute force according to the chosen identity-provider boundary.

Gate:
- only Web administration can modify grants;
- identity token proves authentication but does not act as current SquiFlow permission truth;
- native client has no embedded reusable secret;
- server authorization rejects stale/forged/cross-tenant authority;
- redirect/issuer validation fails closed.

## Phase 2 — first local-first Workstation transaction

Deliver minimal Customer/Walk-in + Order slice:

```text
UI
→ local validation
→ local durable business + outbox transaction
→ immediate local result
```

Attack:
- power loss after commit;
- lost Channel signal;
- app restart;
- disk full;
- local DB busy/locked;
- user closes app immediately.

Gate:
- user work survives crashes;
- no server/network dependency for the approved local operation.

## Phase 3 — authoritative synchronization + object authorization

Deliver:
- bounded upload batch;
- idempotency receipt;
- server tenant derivation/authorization;
- coarse endpoint/function policy;
- tenant-scoped resource resolution;
- resource/action authorization;
- explicit request/response DTO allowlists;
- one authoritative central transaction;
- per-item result;
- remote change feed + cursor;
- local result/cursor durability.

Attack:
- server commits, response lost;
- duplicate request;
- permission revoked while pending;
- malformed/tampered tenant ID;
- partial batch failure;
- substitute another tenant's object ID;
- change HTTP method/path to reach a privileged function;
- add hidden/privileged JSON properties;
- request sensitive response fields without permission;
- stale `TenantAuthorizationRevision` snapshot.

Gate:
- no duplicate business effect;
- no cursor advancement before local apply;
- explicit `AuthorizationChanged`/conflict states;
- BOLA/BFLA/property-level authorization tests pass;
- cross-tenant existence/data is not exposed through unrestricted resource lookup.

## Phase 4 — conflict + long-offline

Deliver:
- one aggregate conflict UX;
- protocol/schema version negotiation;
- reauth/upgrade/resnapshot/repair paths;
- pending local work preservation;
- effective-permission snapshot revision refresh.

Attack:
- weeks/months offline;
- tombstone expired;
- entity deleted/merged remotely;
- 10k+ pending changes;
- old rule snapshot;
- old permission snapshot after Owner revoked authority.

Gate:
- no silent local data discard;
- server does not accept a stale Workstation permission snapshot as authority.

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

## Phase 6 — Worker and platform control plane

Deliver:
- durable job/outbox;
- Worker claim/lease/retry;
- no-progress detection;
- pause/drain/resume;
- quarantine/reconciliation;
- Platform Admin Web controls;
- explicit job authorization/execution classification:
  - committed business consequence;
  - deferred actor action;
  - platform-control command.

Attack:
- Worker crashes after external effect;
- stale lease owner resumes;
- admin retries repeatedly;
- pause during active job;
- external outcome unknown;
- actor permission revoked after enqueue;
- provider response malformed/oversized/slow;
- normal tenant user guesses a platform-admin endpoint.

Gate:
- critical Worker/server controls are reachable only from Platform Admin Web;
- no generic force-complete operation;
- committed consequences are not incorrectly cancelled by later actor revocation;
- deferred actor actions reauthorize where their semantics require it;
- third-party responses are bounded/validated and timeout-controlled.

## Phase 7 — files/documents/printing

Deliver:
- local attachment staging;
- object upload + metadata lifecycle;
- document helper where isolation justified;
- printing status independent from transaction status;
- upload/request size and processing budgets.

Attack:
- object succeeds/metadata fails;
- metadata succeeds/object missing;
- printer out of paper;
- helper crashes;
- disk full during staging;
- oversized/decompression-bomb input;
- malicious filename/content-type mismatch.

## Phase 8 — API security + observability/admin hardening

Deliver:
- OpenTelemetry traces/metrics/log correlation;
- New Relic + Aiven OpenSearch export;
- Backtrace crash path;
- tenant/platform audit;
- step-up and exact-diff high-risk admin workflows;
- generated endpoint/version inventory and deprecation/retirement check;
- production CORS/cache/security-header/error policy;
- endpoint/work-class resource budgets;
- SSRF-safe outbound HTTP policy before enabling user-configured webhooks/remote fetches.

Attack against applicable OWASP API Security Top 10 categories:
- object/function/property authorization bypass;
- auth/recovery abuse;
- expensive single-request resource exhaustion;
- excessive provider-cost operations;
- automated abuse of a sensitive business flow;
- SSRF/private-network/metadata target;
- stale/beta/debug API version;
- unsafe cache/CORS/error configuration;
- malicious/unexpected third-party provider response.

Gate:
- telemetry failure never invalidates a business transaction;
- sensitive data is redacted/bounded;
- privileged API inventory has no undocumented production endpoint;
- applicable OWASP attack tests pass.

## Phase 9 — payments/credit/inventory hardening

Deliver:
- `OutcomeUnknown` payment state;
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
- ordinary Staff attempts refund/credit override by object ID, hidden property or guessed endpoint;
- permission revoked before a deferred sensitive action executes.

## Phase 10 — release/resource/recovery qualification

Deliver:
- Workstation resource/idle tests;
- constrained single-node server tests;
- installer/update/rollback;
- schema/protocol/rule/config compatibility;
- backup/restore drill;
- long-running leak/soak tests;
- authorization-revision/cache invalidation tests if a permission cache exists;
- OIDC/session/logout/version-skew tests for selected identity implementation.

## Deferred until baseline proves itself

Do not spend baseline implementation effort on:
- full browser offline/PWA business sync;
- Kafka;
- YugabyteDB;
- mandatory Redis;
- microservice-per-module extraction;
- global CRDT data model;
- Zanzibar-style authorization service/relation-tuple graph/specialized set index;
- Dynamic OpenID Connect Client Registration;
- OpenID Native SSO for Mobile Apps as a Windows login mechanism;
- advanced manufacturing/MRP/wastage;
- specialized search infrastructure without workload evidence.
