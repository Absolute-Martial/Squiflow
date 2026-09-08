# Core API and Worker Architecture

**Version:** v0.0.15

## 1. Core API

`services/core-api` is the ASP.NET Core tenant/business HTTP/composition host.

It owns:
- request pipeline;
- ZITADEL OIDC/session integration for tenant/business surfaces;
- tenant context resolution;
- ASP.NET/OpenFGA tenant authorization integration;
- input/schema validation;
- application command/query dispatch;
- tenant/business rate limiting/admission control;
- health/readiness;
- correlation/trace context;
- dependency composition.

It does **not** host the Platform Admin API. Platform/super-admin HTTP operations belong to the independently deployable `services/admin-api` described in `docs/admin/ADMIN_SURFACES.md` and `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`.

Core API does not own business-domain implementation merely because the HTTP request arrives there. Business behavior belongs in modules/application code.

## 2. Authorization pipeline

For an existing tenant/business resource:

```text
ZITADEL-authenticated actor/session
→ derive authoritative SquiFlow TenantContext
→ coarse ASP.NET endpoint/function policy
→ tenant-scoped resource lookup where practical
→ IAuthorizationService semantic requirement
→ OpenFGA relationship/permission check where applicable
→ SquiFlow domain/workflow invariant validation
→ concurrency/version check
→ execute
```

The endpoint path is organizational, not the security boundary.

ASP.NET Core policies/requirements are the framework integration primitive. OpenFGA is the selected application-authorization engine for relationship/permission decisions. Handlers must not depend on invocation order and must not perform business mutations as side effects.

Resource authorization is imperative when the decision requires the loaded resource. `IAuthorizationService` and typed authorization handlers are the server integration point.

Do not bind arbitrary request JSON directly onto domain/persistence entities. Use explicit command/request DTOs and explicit response projections to prevent property-level over-posting/exposure.

## 3. Command/query responsibility

SquiFlow keeps a clear distinction between commands and queries without forcing a full CQRS deployment topology.

```text
Command
→ expresses business intent
→ may change authoritative state
→ authorization/domain/concurrency/idempotency apply

Query
→ returns information
→ does not perform business mutation
→ tenant/read authorization still applies
```

Important business actions should be task-oriented, for example `ApproveQuote`, `RefundPayment`, or `AdjustInventory`, rather than hiding domain intent behind generic `Update`.

This separation does **not** imply:
- separate command/query databases;
- separate command/query services;
- event sourcing;
- a read projection for every endpoint.

Introduce read-optimized projections/materialized views only when an implemented workload justifies the extra freshness/rebuild/operational contract.

## 4. API cross-cutting concerns and pipeline ownership

Authentication, safe logging/correlation, generic rate/admission limits, safe error shaping, and input/schema boundaries affect many endpoints. They should be applied uniformly through ASP.NET Core middleware, endpoint filters/metadata, policies, and shared host configuration where those mechanisms fit.

Do **not** respond to cross-cutting concerns by building one giant middleware that owns all business decisions.

Correct split:

```text
request/correlation + safe logging
→ generic request/rate/admission controls
→ authentication
→ TenantContext/platform context resolution
→ coarse endpoint/function policy
→ input/schema validation
→ tenant-scoped resource loading
→ resource/OpenFGA authorization
→ domain/workflow/concurrency validation
→ transaction/effect
→ safe result/error + trace/audit evidence
```

Some concerns intentionally span the whole pipeline (for example trace correlation). Others require the actual resource or domain state and therefore belong later.

Every externally reachable endpoint must be classifiable by executable metadata/configuration as one of the intended audiences, with its authentication/policy/resource-limit behavior or an explicit reviewed public exception. CI/release endpoint inventory tests should fail on accidental unclassified privileged/business endpoints rather than relying on developers remembering to add security route by route.

Health/liveness endpoints, OIDC callbacks, provider webhooks and other special routes can have different policies, but they are explicit exceptions with their own abuse/input/authenticity controls.

## 5. API security baseline

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

A valid ZITADEL identity/token does not itself authorize a resource. A valid OpenFGA relation does not bypass TenantContext/data isolation or SquiFlow business state checks.

An endpoint inventory is generated from executable endpoint metadata/OpenAPI in CI/release. It is not a hand-maintained architecture CSV.

## 6. API version/surface ownership

Every externally reachable API surface declares:
- owning backend: Core API or Admin API;
- audience: tenant Web, Workstation sync, client-client, tenant admin, platform admin or specific integration;
- authentication method;
- authorization policy family;
- current version/compatibility rules;
- owner/module;
- retirement/deprecation policy.

Development/debug/test endpoints are not simply hidden; they are absent or inaccessible in production configuration.

REST/task-oriented HTTP is the baseline, but SquiFlow does not claim strict REST purity. GraphQL/Federation are not added unless a real read-composition problem proves their extra query-cost/authorization/cache/schema complexity is worthwhile.

## 7. Synchronous versus asynchronous HTTP

Keep ordinary short authoritative business transactions synchronous when the user needs a definitive result in the interactive request budget.

Use asynchronous request-reply for long-running/resource-heavy work:

```text
POST
→ authenticate/authorize/validate/idempotency
→ durable operation accepted
→ 202 Accepted
   Location: /api/operations/{id}
   Retry-After: ... where useful
```

The operation resource persists states such as:

```text
Pending
Running
Succeeded
Failed
Cancelled
OutcomeUnknown
```

A duplicate POST with the same semantic idempotency key returns the existing operation/status resource rather than creating another Worker item.

Do not queue every command merely because a Worker exists.

## 8. API idempotency and retry

Mutating commands that can be retried after an uncertain outcome use caller-provided semantic idempotency keys.

The server distinguishes:

```text
same key + same intent
→ same/semantically equivalent result

same key + different intent
→ validation/idempotency mismatch
```

Do not derive identity only by hashing request parameters; identical parameter sets can represent separate intentional operations.

Where the business mutation, receipt and outbox share one authoritative store, commit them in one transaction.

Retry policy is finite and failure-classified:
- retry only transient conditions;
- honor `Retry-After`;
- set per-attempt timeouts;
- use backoff/jitter where appropriate;
- cap attempts/elapsed time and aggregate retry budget;
- choose an intentional retry owner per dependency call path;
- avoid nested retry multiplication across Workstation/API/application/Worker/provider SDK layers;
- never endlessly retry deterministic domain/auth/validation failures.

Full details: `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`.

## 9. Rate limiting, admission, and resource consumption

Rate limiting is only one control and is separate from authorization.

A caller can be authorized but temporarily throttled because tenant/system/provider capacity says `not now`.

Rate/admission dimensions can include:
- unauthenticated/IP/network source for public/login/recovery abuse;
- account/device;
- tenant;
- route/operation class;
- expensive report/document/import/upload;
- downstream paid/provider budget;
- platform-admin operations on Admin API.

Also bound:
- maximum body/upload sizes;
- page/result limits;
- sync batch count/bytes;
- rule/form complexity;
- document/image/report concurrency;
- request/handler deadlines where safe;
- outbound provider calls and paid-operation budgets;
- per-tenant/noisy-neighbor consumption;
- aggregate retry budget.

Use `429`/`Retry-After` for temporary HTTP throttling where applicable. Background work still needs its own bounded admission/concurrency/fairness; an edge or HTTP limiter does not protect every downstream resource by itself.

Expensive work moves to the Worker rather than keeping request threads occupied indefinitely.

## 10. API performance without correctness regression

Common techniques such as pagination, caching, compression, asynchronous telemetry export, and connection pooling are used selectively after measurement.

Rules:
- large collections are paginated/bounded;
- a cache must declare its freshness and cannot become permission/payment/stock/credit authority;
- telemetry/log export may buffer asynchronously only with bounded backpressure/drop behavior; authoritative audit is separate;
- compress sufficiently large compressible payloads, not already-compressed media or unbounded bodies;
- DB connection pools are bounded from measured DB/rack capacity and must not leak tenant/RLS context between reused connections.

Measure representative latency percentiles, throughput, dependency/query time, allocation/memory, payload size, and pool wait before adding another performance layer.

## 11. Worker

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

## 12. Background trigger taxonomy

Background work can originate for different reasons. The trigger does not define the reliability contract by itself.

```text
User-triggered consequence
  e.g. committed invoice schedules PDF/notification

Scheduled occurrence
  e.g. periodic report/reconciliation/maintenance

External-system-triggered work
  e.g. webhook/provider callback/file-arrival signal

Batch/volume-triggered work
  e.g. bounded import/rebuild/maintenance batch

Platform control command
  e.g. approved maintenance/reconciliation operation
```

Important scheduled work must not rely on “cron fired” as the only truth. The scheduler creates or claims a durable occurrence/job with a stable occurrence identity before the business effect executes.

For scheduled jobs whose meaning depends on business time, define timezone/DST behavior explicitly. Duplicate scheduler firings must not silently create duplicate business effects.

The first Worker implementation may use a simple durable database-backed job/schedule mechanism if it satisfies the actual workload. Do not introduce a distributed scheduler/broker merely because background-work architectures can grow into one.

## 13. Command/job versus event

Keep instruction and fact semantics distinct:

```text
Command / Job
= please perform this intended work
= has an execution owner and completion/failure semantics

Event
= this fact already happened
= zero, one, or many consumers may react
```

Example:

```text
IssueInvoice command
→ authoritative invoice transaction commits
→ InvoiceIssued event/fact in transactional outbox
→ independent consequences may generate PDF, notify customer, update a read projection
```

The asynchronous event does not make the invoice issuance itself eventually authoritative.

Avoid hidden event choreography for flows that require one explicit business owner/state machine, especially money, stock, permissions, and other protected transitions.

Transactional outbox/audit/history is not the same thing as event sourcing. Current relational state remains authoritative unless a future explicit event-sourcing decision changes that for a demonstrated domain.

## 14. Eventual/derived consistency boundary

SquiFlow does not make the whole product eventually consistent.

Eventually updated consumers/projections are acceptable only where temporary disagreement is safe and the contract is explicit.

Each derived projection/consumer should define:
- authoritative source;
- source version/sequence/effect identity as needed;
- duplicate/out-of-order handling;
- freshness evidence where material;
- rebuild/reconciliation behavior;
- what happens when propagation stalls.

Late/out-of-order derived messages must not overwrite a newer business meaning. A stale report/search/cache result cannot become current stock, credit, payment, tenant-isolation, or authorization authority.

Workstation `LocalCommitted`/`PendingRemote` remains a distinct local-first authority model, not a vague claim that the central business system will “eventually become consistent.”

## 15. Messaging-pattern selection

Use the simplest pattern matching the semantic need:

### Queue / competing consumers
Use when one durable work item should be processed by one worker path.

Examples:
- generate one PDF;
- send one delivery attempt;
- execute one reconciliation item.

### Publish/subscribe
Use only when several independent consumers genuinely need the same committed fact.

A transactional outbox can create multiple durable deliveries without requiring a separate generic pub/sub platform in the first implementation.

### Event stream
Use only when consumers actually need durable replay/history/independent offsets or throughput/ordering characteristics that a normal job/outbox system cannot provide economically.

Kafka/event-log infrastructure is not baseline.

### Direct synchronous call
Use when the caller needs the authoritative answer now and the work fits the bounded interactive budget.

## 16. Durable work lifecycle

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

## 17. Worker loop requirements

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
- crash-loop protection;
- queue-age/oldest-item monitoring in addition to depth;
- business priority classes with fairness/aging so lower-priority work cannot starve forever.

## 18. Idempotent consumers and duplicate-entry points

Assume at-least-once delivery/redelivery can occur.

Every message-driven handler must either:
- make the semantic effect idempotent;
- detect an already-applied semantic effect;
- or enter explicit reconciliation when the external effect outcome is unknown.

Duplicates can enter before the Worker as producer retries/republication, at the transport as redelivery, and inside the consumer when a crash occurs after the external/business effect but before acknowledgement.

A queue/message ID can help transport deduplication but is not sufficient to replace the business idempotency key when the same intent can be resent through a different transport attempt/batch.

Do not claim system-wide exactly-once because one broker or database offers a narrower exactly-once/transactional feature.

## 19. Authorization semantics for durable jobs

Do not use one vague rule such as “always re-check the original user's permission” for every queued job. Classify why the job exists.

### A. Committed business consequence

Example: an authorized invoice issuance transaction committed and its outbox schedules document generation.

The business decision is already authoritative. The Worker executes the committed consequence under system authority while retaining the original actor/correlation for audit. A later user-role revocation does not erase the already committed business fact.

### B. Deferred actor action

Example: a request queues an action whose actual business effect has **not** yet been authorized/committed and will occur later.

Reauthorize the actor/current authority at execution when that is semantically required. If authority changed, produce an explicit authorization-changed/review result rather than silently performing the effect.

### C. Platform control-plane command

A platform-critical command originates from Platform Admin Web through **Admin API**, passes risk/step-up/approval checks, and is persisted as a durable control-plane command/proposal. The Worker executes exactly that authorized command under system execution authority. It must not accept a second hidden set of control parameters from Desktop, Core API business routes, or arbitrary job payload.

This classification prevents both unsafe stale-authority execution and the opposite error of cancelling valid committed consequences merely because a user was later suspended.

## 20. External effect safety

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

## 21. Load isolation and container/distributed patterns

Use patterns only where the problem exists:

- **Bulkhead:** isolate expensive work classes/dependencies with bounded pools/concurrency; do not build a cell architecture by default.
- **Queue load leveling:** buffer bursty async work; do not put low-latency authoritative transactions behind a queue merely for architectural symmetry.
- **Competing consumers:** future Worker replicas can claim independent items; ordering stays per consistency key where required.
- **Priority queue:** honor P0–P3/business priority while preserving fairness and aging.
- **Circuit breaker:** add only for remote dependencies where sustained/slow failure makes retries harmful; do not wrap every local component.
- **Claim check:** keep large files/diagnostic payloads outside queue messages and pass protected references.

Container design patterns do not become automatic runtime architecture. Do not add per-service sidecars/proxies/adapters, leader election, or scatter/gather fan-out solely because the server is containerized. A pattern needs a measured coordination/deployment problem and must justify its memory/network/failure/operational cost on the owned rack.

`SquiFlow.Guard` is a native Windows supervision/recovery boundary, not evidence that server components should follow a sidecar-everywhere model.

## 22. Service-to-service communication and data ownership

SquiFlow distinguishes **module communication** from **service communication**.

Inside one runtime host, business modules communicate in-process. Do not create HTTP/gRPC calls between modules merely to imitate a service architecture.

At a real process/service boundary, choose communication from the semantics:

```text
immediate authoritative answer needed
→ synchronous request/response

long-running/after-commit consequence
→ durable Worker/job/outbox

multiple real consumers of one committed fact
→ durable fan-out/pub-sub when proven
```

Avoid long synchronous chains such as:

```text
client → Core API → service A → service B → service C
```

when the same business operation can stay one in-process application/transactional flow. Every network hop adds timeout, retry, partial-failure, versioning, authorization, observability, and deployment obligations.

Core API, Admin API, and Worker can legitimately share the same central database because they are runtime hosts of the same modular-monolith business core. Shared access still requires explicit module/data ownership and common invariants. One host must not use ad-hoc SQL to bypass another module's business rules merely because the table is reachable.

If a future capability is extracted into a genuinely independent service, its authoritative data ownership becomes explicit and other services should use stable APIs/events/read models rather than directly modifying its private tables.

## 23. Edge gateway versus service mesh

An edge reverse proxy/API-gateway capability may be useful for north-south concerns such as:
- TLS termination;
- hostname/custom-domain routing;
- request-size limits;
- WAF/private-access policy;
- coarse public/admin exposure;
- coarse rate limiting;
- transport/protocol negotiation and edge observability where supported.

That gateway is not the business authorization engine. Core API/Admin API still authenticate/authorize/validate resources and state independently.

A shared edge may route to both Core API and Admin API, but it must not reintroduce a runtime dependency where Admin API calls through Core API. Platform Admin remains an independent backend/security/availability plane.

Do not adopt a heavyweight API-management product merely because gateways can also offer payload transformation, analytics, version management, or authorization. Add only the edge capabilities SquiFlow actually needs and can operate on the owned deployment.

A service mesh is **not baseline**. Revisit only if independently deployed east-west traffic grows enough that service mTLS, traffic policy, discovery, and distributed observability cannot be handled reliably/economically by the simpler topology.

## 24. Network protocol boundaries

Current production protocol direction:
- external Web/Core API/Admin API/Workstation sync traffic uses HTTPS/TLS;
- ZITADEL uses OIDC/OAuth over HTTPS;
- HTTP/1.1, HTTP/2, or HTTP/3 may be negotiated by client/edge/server where supported, but application semantics do not depend on one transport version;
- DNS/hostnames are routing inputs, never tenant authority by themselves;
- WebSocket/SignalR, if used, carries live UI/signal/wakeup information only; durable business/sync truth remains DB/outbox/state records;
- SSH/private network access is infrastructure operations/recovery only, not a normal tenant business channel.

Time synchronization is operationally important for TLS/token validity, leases, schedules, and diagnostics. Where correctness cannot tolerate wall-clock ambiguity, use explicit versions, sequence/occurrence IDs, or monotonic/fencing evidence instead of trusting timestamps alone.

Do not add gRPC, MQTT, WebRTC, FTP/SFTP, or raw TCP/UDP as application protocols without a concrete latency/streaming/device/transport/compatibility requirement.

## 25. Server concurrency

The application handles independent work in parallel. Correctness is scoped to the relevant aggregate/resource, not one global writer.

Final correctness is enforced by the selected central store through transactions, constraints, optimistic concurrency and locking where appropriate.

## 26. Stateless server-process semantics

`Stateless` for Core API/Admin API/future Worker means their process memory is not the sole durable authority for business correctness.

It does **not** mean SquiFlow has no state.

Durable/shared state can live in:
- central DB;
- object storage;
- outbox/job store;
- ZITADEL/OpenFGA;
- shared configuration/session state where the selected Web topology requires it;
- backup/recovery systems.

Process-local cache/circuit/temporary state is permitted only with explicit loss/freshness behavior. A server restart must not cause committed orders, idempotency receipts, durable jobs, permissions, or business documents to disappear merely because they were only in memory.

This also does not imply automatic failover or zero downtime. If Blazor Interactive Server is used, Web circuits themselves are stateful and require an explicit circuit/session topology before multi-node failover claims are made.

## 27. Platform-critical Worker controls

Pause/drain/resume/retry/quarantine/reconcile controls that can materially affect server operation are invoked through Platform Admin Web → **Admin API**. They are not Core API routes.

Do not expose those controls through Workstation, ordinary tenant Web, `/sync`, or Core API business/tenant-admin endpoints.

## Source basis

- OWASP API Security Top 10 2023
- ASP.NET Core policy/resource-based authorization
- Zanzibar authorization consistency lessons
- Stripe idempotency article
- AWS Builders' Library idempotent API article
- Azure Architecture Center patterns, API design/implementation, background jobs and transient-fault guidance
- ByteByteGo CQRS/retry/event-driven/messaging/idempotency/background-work/multi-tenancy/container/cross-cutting/API-security/stateless/clean-code/eventual-consistency/gateway-mesh/schema/indexing/API-performance/SOLID/rate-limiting/GraphQL/API-gateway/service-communication/data-sharing/API-design/REST/network-protocol follow-up reviews

See:
- `docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md`
- `docs/review/RELIABILITY_API_AND_PATTERN_SOURCE_REVIEW.md`
- `docs/review/BYTEBYTEGO_DISTRIBUTED_SYSTEMS_SOURCE_REVIEW.md`
- `docs/review/BYTEBYTEGO_CODE_CONSISTENCY_DATA_API_SOURCE_REVIEW.md`
- `docs/review/BYTEBYTEGO_API_GATEWAY_SERVICE_PROTOCOL_SOURCE_REVIEW.md`
- `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`
