# Core API and Worker Architecture

**Version:** v0.0.15

## 1. Core API

`services/core-api` is the ASP.NET Core HTTP/composition host.

It owns:
- request pipeline;
- ZITADEL-backed OIDC/session integration;
- tenant/platform context resolution;
- ASP.NET endpoint policy/authorization integration;
- OpenFGA client integration for application relationship/permission checks;
- input/schema validation;
- application command/query dispatch;
- rate limiting/admission control;
- health/readiness;
- correlation/trace context;
- dependency composition.

It does not own business-domain implementation merely because the HTTP request arrives there. Business behavior belongs in modules/application code.

## 2. Authorization pipeline

For an existing resource:

```text
ZITADEL-authenticated actor/session
→ derive authoritative SquiFlow TenantContext
→ coarse ASP.NET endpoint/function policy
→ tenant-scoped resource lookup where practical
→ IAuthorizationService semantic requirement
→ OpenFGA Check where role/relation/permission belongs in OpenFGA
→ SquiFlow domain/workflow/state invariant validation
→ concurrency/version check
→ execute transaction
```

The route path is organizational, not authority.

### Responsibility split

- **ZITADEL:** authentication/account identity/MFA/SSO/session-provider capability.
- **OpenFGA:** application roles, tenant custom-role relationships, permissions/resource relationships.
- **ASP.NET Core `IAuthorizationService`:** in-process semantic policy/requirement integration.
- **SquiFlow domain/rules/workflow:** business validity and canonical state.
- **Persistence:** tenant isolation, transaction/constraints/concurrency.

An OpenFGA `allow` cannot bypass wrong tenant scope, invalid workflow state, stock/payment invariant, or stale expected version.

ASP.NET handlers must not depend on invocation order and must not mutate business state as an authorization side effect.

Use explicit request DTOs and response projections; never bind arbitrary client JSON directly to persistence/domain entities.

## 3. OpenFGA dependency behavior

Production checks target the configured/pinned OpenFGA authorization model ID.

Authorization dependency failures are not permission grants:
- explicit deny → authorization denied;
- provider/network failure → dependency unavailable/fail-safe result according to operation class;
- model mismatch/configuration error → operational/security fault, not fallback allow.

For permission/relation changes, the Web/API uses the durable/reconcilable process owned by `docs/security/TENANT_PERMISSIONS.md`. Do not pretend OpenFGA tuple updates and SquiFlow DB/audit updates form one cross-system ACID transaction.

Consistency mode is selected deliberately by risk. Mutation/revocation-sensitive checks may require stronger freshness than ordinary low-risk reads.

## 4. API security baseline

Every API group is reviewed against applicable OWASP API Security Top 10 classes.

Required gates include:
- object-level authorization for client-supplied resource identifiers;
- function-level authorization for normal/tenant-admin/platform-admin operations;
- property-level allowlists for request/response contracts;
- strong ZITADEL authentication/recovery/step-up abuse controls;
- bounded request/upload/page/batch/resource budgets;
- sensitive-business-flow controls;
- SSRF controls before arbitrary webhook/remote-fetch URLs;
- production security headers/CORS/cache/error policy;
- generated endpoint/version inventory;
- validation/timeouts/limits for third-party APIs including OpenFGA/ZITADEL/storage providers.

An endpoint inventory is generated from executable metadata/OpenAPI; it is not a hand-maintained architecture CSV.

## 5. API version/surface ownership

Every externally reachable API surface declares:
- audience: tenant Web, Workstation sync, client portal, tenant admin, platform admin, or integration;
- authentication method;
- authorization policy family;
- current version/compatibility rules;
- owner/module;
- retirement/deprecation policy.

Development/debug/test endpoints are absent or inaccessible in production configuration.

## 6. Synchronous versus asynchronous HTTP

Keep ordinary short authoritative business transactions synchronous when the user needs a definitive result in the interactive request budget.

Use asynchronous request-reply only for long-running/resource-heavy work:

```text
POST
→ authenticate/authorize/validate/idempotency
→ durable operation accepted
→ 202 Accepted
   Location: /api/operations/{id}
```

Operation states can include:

```text
Pending
Running
Succeeded
Failed
Cancelled
OutcomeUnknown
```

Duplicate POST with the same semantic idempotency key returns the existing operation/status rather than creating duplicate work.

## 7. API idempotency and retry

Mutating commands that can be retried after uncertain outcome use caller-provided semantic idempotency keys.

```text
same key + same intent      → same semantic result
same key + different intent → reject mismatch
```

Where business mutation, receipt and outbox share one authoritative store, commit them together.

Retry is finite/classified:
- retry transient conditions only;
- honor `Retry-After`;
- per-attempt timeout;
- backoff/jitter;
- capped attempts/elapsed time/aggregate retry budget;
- avoid retry multiplication;
- never endlessly retry deterministic auth/domain/validation failures.

OpenFGA/ZITADEL SDK retries must be included in the aggregate retry budget rather than stacking invisibly under application retries.

## 8. Resource consumption/admission

Bound:
- body/upload/page/sync-batch sizes;
- rule/form complexity;
- document/image/report concurrency;
- DB connections;
- OpenFGA/ZITADEL/provider call concurrency;
- endpoint deadlines where safe;
- per-tenant/noisy-neighbor consumption;
- aggregate retry budget.

Rate limiting alone is not enough for one expensive operation.

## 9. Worker

`services/worker` is created when Phase 6 introduces real durable asynchronous work.

Examples:
- documents/reports;
- image processing;
- notifications/integrations;
- reconciliation;
- projection maintenance;
- scheduled jobs;
- rule/workflow snapshot distribution where asynchronous;
- diagnostics packaging.

## 10. Durable work lifecycle

```text
Pending
→ Claimed
→ Running
→ Completed
```

Alternative states:

```text
RetryScheduled
Failed
Quarantined
Cancelled
OutcomeUnknown
```

Claims have leases/ownership expiry; use fencing/generation where stale owners could cause unsafe duplicate effects.

## 11. Worker loop requirements

A process may run indefinitely. A loop may not spin indefinitely.

Required:
- bounded queues/concurrency;
- tenant-aware fairness;
- cancellation propagation;
- event/signal wait rather than hot polling;
- periodic reconciliation fallback;
- deadline/no-progress detection;
- graceful drain/shutdown;
- retry classification + backoff/jitter;
- poison-work quarantine;
- crash-loop protection;
- queue age/oldest-item monitoring, not only depth;
- priority with fairness/aging.

## 12. Idempotent consumers

Assume at-least-once delivery/redelivery.

Each handler must either:
- make semantic effect idempotent;
- detect already-applied effect;
- or enter explicit reconciliation when external outcome is unknown.

Transport/message ID never replaces business idempotency identity when the same intent can be resent through another envelope.

## 13. Authorization semantics for durable jobs

Do not reauthorize every queued job identically. Classify why it exists.

### A. Committed business consequence

Example: invoice issuance already committed and outbox schedules PDF generation.

Worker executes the committed consequence under system authority, preserving original actor/correlation for audit. Later OpenFGA role revocation does not erase already committed business truth.

### B. Deferred actor action

Actual business effect is not yet committed. Re-check current ZITADEL/session state where applicable, OpenFGA authorization, and current domain state at execution when the action semantics require it.

If authority changed, return explicit `AuthorizationChanged`/review rather than perform the effect.

### C. Platform control-plane command

A platform command originates from Platform Admin Web, passes ZITADEL authentication + platform authorization + risk/step-up/approval, then is persisted as an exact durable command. Worker executes that command under system execution authority.

No second hidden parameter set from Desktop/job payload is accepted.

## 14. External effect safety

For external payment/webhook/provider actions:
1. before effect — cancellation may be safe;
2. request sent, response missing — `OutcomeUnknown`;
3. provider confirms success, local completion write fails — reconcile using provider idempotency/reference;
4. local completion committed — retry returns same semantic result.

Never infer cancellation undid an external effect.

Third-party responses are untrusted:
- validate schema/status;
- bound size;
- TLS;
- timeout;
- bounded redirects where applicable;
- isolate malformed/unexpected response from authoritative state transitions.

## 15. Load isolation patterns

Use patterns only where the problem exists:
- bulkhead-style bounded work classes/dependencies;
- queue load leveling for bursty async work;
- competing consumers when multiple Worker replicas exist;
- priority with fairness/aging;
- circuit breaker only for dependencies where persistent failure makes retries harmful;
- claim-check/reference for large files rather than queueing full binary payloads.

Do not build a cell architecture, BFF fleet, or queue every transaction merely because those patterns exist.

## 16. Server concurrency

Independent work runs in parallel. Correctness is scoped to the relevant aggregate/resource, not one global writer.

Final correctness comes from the selected central store through transactions, constraints, optimistic concurrency and locking where required.

## 17. Platform-critical Worker controls

Pause/drain/resume/retry/quarantine/reconcile controls that materially affect server operation are invoked only through Platform Admin Web and privileged `/platform-admin/...` APIs during normal operation.

Tenant Web/Workstation cannot turn ZITADEL login or tenant OpenFGA roles into platform operator authority.

If the application control plane is down, infrastructure recovery uses the separate private runbook.

## 18. Dependency degradation expectations

### ZITADEL unavailable
- existing server session behavior follows the selected session/revocation design;
- new authentication/reauthentication/step-up may be unavailable;
- never mint local fake identities to stay online.

### OpenFGA unavailable
- operations requiring current application authorization fail closed/degraded according to risk;
- already committed business consequences can continue where they no longer require actor authorization;
- never interpret provider error as `allowed=true`.

### Guard unavailable
- Workstation may continue running if healthy;
- supervision/recovery is degraded and Guard should be restartable independently;
- no business data becomes invalid merely because Guard process is down.

## Source basis

- ZITADEL OIDC documentation
- OpenFGA current modeling/consistency/model-version guidance
- OWASP API Security Top 10 2023
- ASP.NET Core policy/resource-based authorization
- Zanzibar consistency lessons
- Stripe/AWS idempotency guidance
- Azure API/background/transient-fault patterns

See:
- `docs/security/IDENTITY_AND_SESSIONS.md`
- `docs/security/TENANT_PERMISSIONS.md`
- `docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md`
- `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`
