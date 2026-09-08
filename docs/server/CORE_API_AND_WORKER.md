# Core API and Worker Architecture

**Version:** v0.0.15

## 1. Core API

`services/core-api` is the ASP.NET Core HTTP/composition host.

It owns:
- request pipeline;
- OIDC/session integration;
- tenant/platform context resolution;
- endpoint policy/authorization;
- input/schema validation;
- application command/query dispatch;
- rate limiting/admission control;
- health/readiness;
- correlation/trace context;
- dependency composition.

It does not own business-domain implementation merely because the HTTP request arrives there. Business behavior belongs in modules/application services.

## 2. Authorization pipeline

For an existing resource:

```text
Authenticate
→ derive authoritative tenant/platform context
→ coarse endpoint/function policy
→ tenant-scoped resource lookup
→ IAuthorizationService resource/action requirement
→ domain/workflow invariant validation
→ concurrency/version check
→ execute
```

The endpoint path is organizational, not the security boundary.

Resource authorization is imperative when the decision requires the loaded resource. ASP.NET Core `IAuthorizationService` and `AuthorizationHandler<TRequirement,TResource>` are the default framework primitives.

Do not bind arbitrary request JSON directly onto domain/persistence entities. Use explicit command/request DTOs and explicit response projections to prevent property-level over-posting/exposure.

## 3. API security baseline

Every API group is reviewed against the OWASP API Security Top 10 classes that apply.

Required release gates include:
- object-level authorization for every client-supplied resource identifier;
- function-level authorization for normal/tenant-admin/platform-admin operations;
- property-level allowlists for request and response contracts;
- strong authentication/recovery/step-up abuse controls;
- bounded request/upload/page/batch sizes and execution/resource budgets;
- business-flow-specific abuse controls where automation can cause material harm;
- SSRF controls before introducing arbitrary webhook/remote-fetch URLs;
- production security headers, CORS/cache/error policy;
- generated endpoint/version inventory and explicit retirement policy;
- validation/timeouts/limits for data consumed from third-party APIs.

An endpoint inventory is generated from executable endpoint metadata/OpenAPI in CI/release. It is not a hand-maintained architecture CSV.

## 4. API version/surface ownership

Every externally reachable API surface declares:
- audience: tenant Web, Workstation sync, client-client, tenant admin, platform admin or specific integration;
- authentication method;
- authorization policy family;
- current version/compatibility rules;
- owner/module;
- retirement/deprecation policy.

Development/debug/test endpoints are not simply hidden; they are absent or inaccessible in production configuration.

## 5. Resource consumption/admission

Rate limiting is only one control.

Also bound:
- maximum body/upload sizes;
- page/result limits;
- sync batch count/bytes;
- rule/form complexity;
- document/image/report concurrency;
- request/handler deadlines where safe;
- outbound provider calls and paid-operation budgets;
- per-tenant/noisy-neighbor consumption.

Expensive work moves to the Worker rather than keeping request threads occupied indefinitely.

## 6. Worker

`services/worker` executes durable asynchronous work that should not keep API requests open.

Examples:
- documents/reports;
- image processing;
- notifications/integrations;
- reconciliation;
- projection maintenance;
- scheduled jobs;
- rule/workflow snapshot distribution where asynchronous;
- diagnostic packaging.

## 7. Durable work lifecycle

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
Quarantined/DLQ
Cancelled
OutcomeUnknown
```

A claim has a lease/ownership expiry. Use fencing/claim generations for work where a stale previous owner could cause an unsafe duplicate effect.

## 8. Worker loop requirements

A process may run indefinitely. A loop may not spin indefinitely.

Required:
- bounded queues;
- bounded concurrency;
- cancellation propagation;
- event/signal wait rather than hot polling;
- periodic reconciliation as fallback;
- deadline and no-progress detection;
- graceful drain/shutdown;
- retry classification with exponential backoff + jitter;
- poison-work quarantine;
- crash-loop protection.

## 9. Authorization semantics for durable jobs

Do not use one vague rule such as “always re-check the original user's permission” for every queued job. Classify why the job exists.

### A. Committed business consequence

Example: an authorized invoice issuance transaction committed and its outbox schedules document generation.

The business decision is already authoritative. The Worker executes the committed consequence under system authority while retaining the original actor/correlation for audit. A later user-role revocation does not erase the already committed business fact.

### B. Deferred actor action

Example: a request queues an action whose actual business effect has **not** yet been authorized/committed and will occur later.

Reauthorize the actor/current authority at execution when that is semantically required. If authority changed, produce an explicit authorization-changed/review result rather than silently performing the effect.

### C. Platform control-plane command

A platform-critical command originates from Platform Admin Web, passes risk/step-up/approval checks, and is persisted as a durable control-plane command/proposal. The Worker executes exactly that authorized command under system execution authority. It must not accept a second hidden set of control parameters from a Desktop or arbitrary job payload.

This classification prevents both unsafe stale-authority execution and the opposite error of cancelling valid committed consequences merely because a user was later suspended.

## 10. External effect safety

For a side effect such as an external payment, webhook or remote provider action:

1. before effect — cancellation can be safe;
2. request sent, response missing — `OutcomeUnknown`;
3. provider confirms success, local completion write fails — reconcile using provider idempotency/reference;
4. local completion committed — retry must return the same semantic result.

Never infer that cancellation undid an external effect.

Third-party responses are untrusted inputs even when the provider is managed/well-known:
- validate payload/schema;
- bound response size;
- use TLS;
- set timeouts;
- do not blindly follow redirects;
- isolate malformed/unexpected responses from authoritative state transitions.

## 11. Server concurrency

The application handles independent work in parallel. Correctness is scoped to the relevant aggregate/resource, not one global writer.

Final correctness is enforced by the selected central store through transactions, constraints, optimistic concurrency and locking where appropriate.

## 12. Platform-critical Worker controls

Pause/drain/resume/retry/quarantine/reconcile controls that can materially affect server operation are invoked only through Platform Admin Web and `/platform-admin/...` APIs.

Do not expose those controls through Workstation or ordinary tenant business endpoints.

## Source basis

- OWASP API Security Top 10 2023
- ASP.NET Core resource-based authorization
- Zanzibar authorization consistency lessons

See `docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md` for the detailed source mapping.
