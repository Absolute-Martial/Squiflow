# API Contract, Idempotency, Retry, and Long-Running Operations

**Version:** v0.1.0

This document turns the Stripe, AWS Builders' Library, ASP.NET Core, Azure Architecture, and reviewed ByteByteGo reliability/API material into the SquiFlow API contract without adding a new service or framework.

## 1. Idempotency is part of command semantics

For every mutating command that can be retried after an uncertain network outcome, the caller supplies a stable idempotency key for the **intended business operation**.

Examples:

```text
CreateOrder
RecordPayment
RefundPayment
SubmitQuotation
PublishRuleSet
RequestDocumentGeneration
PlatformControlProposal
```

Do not derive the idempotency key only from request parameters. Two identical-looking requests can legitimately represent two different business intents.

The key is scoped at least by:

```text
caller/tenant authority
+ operation kind
+ idempotency key
```

For Workstation synchronization, every logical outbox item has its own stable semantic idempotency key independent of the transport request/batch ID.

## 2. Same key, same intent

The server stores enough request identity to detect whether a repeated key represents the same semantic request.

If the same key is replayed with the same semantic payload:
- do not perform the business effect again;
- return the prior/semantically equivalent result or current operation status.

If the same key is reused with materially different parameters:
- reject it as an idempotency-key mismatch;
- do not guess which request the caller intended.

## 3. Atomicity of receipt and effect

Where the selected authoritative store supports the business mutation and idempotency receipt in one transaction, commit them atomically:

```text
BEGIN
  check/create idempotency receipt
  validate current business state
  apply mutation
  write audit/outbox
  persist semantic result/reference
COMMIT
```

Never intentionally create either of these windows:

```text
business effect committed
but idempotency receipt missing
```

or:

```text
idempotency receipt committed
but business effect never happened
```

If an external provider effect cannot participate in that transaction, use provider idempotency/effect references plus `OutcomeUnknown` reconciliation.

## 4. Idempotency result states

A receipt may conceptually move through:

```text
Accepted/Processing
Succeeded
FailedFinal
OutcomeUnknown
```

A duplicate request must not enqueue a second long-running job merely because the first is still running.

For an asynchronous command, the duplicate returns the same operation/status resource.

## 5. Idempotency retention

Idempotency records are not necessarily retained forever.

Every command family declares a retention rule based on:
- plausible retry/late-arrival window;
- business/legal significance;
- lifetime of the created effect/resource;
- storage cost;
- whether replay after expiry is safe.

High-risk financial/effect receipts can require much longer durable evidence than low-risk operational commands.

The retention period is part of the guarantee. After the deduplication/receipt evidence expires, SquiFlow must not keep advertising the same duplicate-suppression guarantee unless another durable business invariant still proves it.

## 6. Duplicates can enter at several stages

Do not treat one deduplication table or broker feature as an end-to-end exactly-once guarantee.

A logical operation can duplicate at different points:

```text
producer/caller
  response loss or local retry can send the same intent again

transport/broker
  redelivery/reconnect/republication can deliver the same envelope again

consumer/effect
  consumer crash after an effect but before acknowledgement can repeat execution
```

SquiFlow therefore uses different evidence for different boundaries:

```text
semantic IdempotencyKey
  identifies the intended business operation

MessageId / transport metadata
  helps reason about one envelope/delivery attempt

business/effect receipt or provider reference
  proves or reconciles the actual semantic effect
```

A broker-level duplicate filter does not replace semantic idempotency. A semantic API receipt does not prove an external provider effect if the provider response is lost. A provider idempotency key does not replace SquiFlow's local record/reconciliation of what the application believes happened.

## 7. “Exactly once” requires a named scope

Use the phrase `exactly once` only when the scope is explicit and proven.

Examples:

```text
one local DB transaction
→ business change + local outbox commit atomically

one central DB transaction
→ mutation + idempotency receipt + outbox commit atomically
```

Those are meaningful atomicity guarantees **inside one store**.

They do not automatically make this end-to-end path exactly-once:

```text
Workstation
→ network
→ Core API
→ DB
→ Worker/broker
→ third-party payment/object/notification provider
```

Across distributed/external boundaries, the baseline is at-least-once/retryable delivery plus semantic idempotency and explicit reconciliation where outcome is ambiguous.

## 8. Transport IDs are separate

Keep distinct identifiers for different purposes:

```text
RequestId          one HTTP attempt
CorrelationId      traces an end-to-end flow
CausationId        links derived work to its cause
MessageId          one durable message/envelope
IdempotencyKey     one intended semantic business operation
BusinessId         order/payment/etc. identity
```

Do not use a transient HTTP request ID as the business idempotency key.

## 9. Retry classification

A client retries only when the failure contract says retry can plausibly succeed.

Typical retry candidates:
- network I/O interruption;
- selected timeout cases;
- 429 with `Retry-After`;
- selected 5xx/dependency-transient failures.

Do not automatically retry:
- validation errors;
- authentication failures;
- authorization failures;
- stale-version/business conflicts;
- malformed payloads;
- unsupported protocol/schema;
- deterministic domain rejection.

A retry must still use the same idempotency key when it represents the same intended operation.

## 10. Retry ownership, budget, backoff and jitter

Retries are bounded by:
- per-attempt timeout;
- maximum attempts;
- maximum elapsed duration;
- dependency/client retry budget.

Background/deferred retries normally use exponential backoff plus jitter and honor `Retry-After`.

For each remote dependency call path, deliberately decide **which layer owns retries**. Do not layer independent retry loops blindly at HTTP client + application service + Worker + provider SDK.

A useful review question is:

```text
If the lowest dependency call fails once,
how many actual outbound attempts can the whole stack generate?
```

The answer must be bounded and intentional.

Never use an endless retry loop. Retry improves availability only when the failure is transient; indiscriminate retry can amplify latency/load and turn a dependency problem into a broader outage.

## 11. HTTP method and semantic endpoint rules

Use HTTP semantics where they naturally match the resource operation, but do not force complex business commands into generic CRUD shapes merely to look RESTful.

- GET/HEAD do not produce business side effects.
- PUT/DELETE are implemented idempotently when used.
- POST is **not automatically retry-safe**; POST business commands that can duplicate effects use SquiFlow semantic idempotency.
- PATCH is not assumed idempotent unless the particular contract explicitly proves it.
- Ordinary resources use predictable noun-oriented paths where natural.
- Explicit command/action subresources are valid for semantic transitions such as approval, refund, publication, reconciliation or administrative proposals.

Examples:

```text
Core API:
POST /api/orders
POST /api/quotes/{id}/approval
POST /api/payments/{id}/refunds
POST /tenant-admin/rule-set-publications

Admin API:
POST /platform-admin/worker-control-proposals
```

The path alone is not authority. Core API and Admin API are separate backend executables/security planes; a `/platform-admin/...` route belongs to `services/admin-api`, not Core API.

The endpoint name should express business intent; authorization and domain state still decide whether it can execute.

## 12. Optimistic concurrency and ETags/version tokens

Normal collaborative editing uses explicit versions.

For HTTP clients, ETags/`If-Match` can expose the version contract where useful. The underlying domain/application command still carries/validates the expected version.

A stale write returns a stable conflict/precondition result and does not silently overwrite newer state.

Do not use one global last-write-wins policy for SquiFlow aggregates.

## 13. Pagination and query bounds

Every unbounded collection API requires server-enforced limits.

Use:
- default page size;
- maximum page size;
- stable ordering;
- filtering/sorting allow-list;
- tenant/permission scope before pagination;
- cursor/keyset pagination where offset pagination becomes incorrect or expensive under large/changing datasets.

Offset pagination is not mandatory merely because it is simple to explain. Choose the pagination contract that stays correct and performant for the actual query.

Client-selected projections cannot expose fields the caller is not authorized to see.

## 14. REST baseline without false REST-purity claims

SquiFlow uses **REST/task-oriented HTTP as a pragmatic application API style** because it controls its Web, Workstation, and Admin clients and benefits from explicit command/resource contracts, OpenAPI inventory, bounded request shapes, idempotency semantics, and straightforward route ownership.

SquiFlow does **not** claim every surface is a strict REST implementation.

Classic REST properties remain useful where they help:
- client/server separation;
- predictable resource naming and HTTP semantics;
- explicit cacheability;
- layered edge/backend topology;
- stateless business correctness in backend process memory: committed truth is not stored only in one request-process instance.

Current CoreApi responses classified as protected by endpoint metadata carry `Cache-Control: no-store` from middleware after routing and before authentication/authorization. This includes success, authentication/permission rejection, and handled error responses; individual endpoint headers are not the only guard. The public application bootstrap keeps its separately configured `public,max-age` and ETag policy. The real CoreApi host tests cover protected success, `401`, `403`, provider `503`, an unexpected handled exception, and bootstrap caching. Requalify this policy when endpoint classification, routing, authentication, or error middleware changes. It does not define the future Web browser-session cache policy.

But strict REST purity is not a product requirement. Tenant Web may use a server-backed browser session or Blazor Interactive Server circuit state; SquiFlow uses semantic action subresources for material transitions; many authoritative responses are intentionally non-cacheable; code-on-demand is not part of the business API contract.

Do not weaken domain clarity, security, idempotency, or client compatibility merely to make the API look more formally RESTful.

## 15. API versioning and compatibility

Version compatibility is mandatory because Workstations can skip releases and independently deployed backends can roll at different times.

Every public/long-lived surface has an explicit compatibility/retirement contract. The exact transport for API versioning (URI, header, media type, or a narrow combination) remains an implementation decision until the relevant phase proves which model best fits SquiFlow clients.

Rules:
- never silently reinterpret an old request as a materially different command;
- additive-compatible changes are preferred where practical;
- breaking changes have an explicit new version/compatibility path;
- Workstation sync protocol/schema compatibility is checked before applying durable changes;
- deprecated versions have a known retirement window and telemetry/inventory evidence before removal.

## 16. GraphQL boundary

Do not add GraphQL merely to reduce round trips or because clients can choose fields.

Revisit GraphQL only when a real implemented client has complex aggregation/evolving read needs that are materially awkward or expensive through the REST/query surface.

If GraphQL is ever introduced, it is a separately reviewed surface with:
- tenant/resource/field authorization;
- query depth/complexity/cost limits;
- pagination limits;
- N+1/data-loading strategy;
- cache/freshness rules;
- schema/deprecation ownership;
- introspection/persisted-query policy appropriate to its audience.

GraphQL Federation is not baseline while SquiFlow remains a modular-monolith business core with only a few justified backend executables.

## 17. Rate limiting and admission

Rate limiting is a reliability/fairness control as well as an abuse control. It is separate from authentication and authorization.

A caller can be fully authorized and still be told `not now` because current resource/provider/tenant policy requires throttling.

Rate/admission dimensions may include:
- unauthenticated/IP/network source for login/recovery/public abuse;
- account/device;
- tenant;
- endpoint/operation class;
- expensive upload/report/document/import action;
- platform-admin action;
- downstream paid/provider budget.

Do not force one global requests-per-second value onto every operation.

When HTTP work is temporarily throttled, use a stable `RateLimited`/`ResourceExhausted` error and `429 Too Many Requests` with `Retry-After` where appropriate. Clients must honor backoff rather than aggressively retry.

HTTP rate limiting alone does not protect Worker/database/provider capacity. Queued/background work also needs bounded concurrency, admission, per-tenant fairness, and provider budgets.

Do not create a separate rate-limiting service initially; use the appropriate edge/server/runtime controls until scale/topology proves another boundary is needed.

## 18. API performance techniques and their limits

Performance optimizations are selected from measurement, not enabled blindly.

### Pagination
Required for potentially large collections; see section 13.

### Asynchronous telemetry logging
Operational logs/traces may use bounded asynchronous export/buffering so disk/network I/O does not block every request. The buffer has explicit size/backpressure/drop behavior. Authoritative security/business audit cannot exist only in a lossy asynchronous telemetry queue.

### Caching
Use only where the freshness contract permits it. A stale cache must not become current permission, payment, stock, credit, or tenant authority.

A proposed cache identifies:
- authoritative source and whether cache bypass is always possible;
- tenant, permission, locale/currency/configuration, and version dimensions required in the key;
- TTL/invalidation and the user-visible meaning of stale data;
- maximum entries/bytes and eviction behavior;
- miss amplification, penetration/negative-lookup behavior, and stampede controls such as bounded request coalescing or TTL jitter where useful;
- cold-start/repopulation load and what happens when the cache service/process is unavailable;
- privacy/diagnostic rules for cached content and keys.

Cache failure may reduce performance. It must not grant access, lose committed truth, corrupt authority, or cause an unbounded retry/repopulation storm. These rules do not require Redis, Memcached, an in-process cache, or browser caching before a real workload earns one.

### Payload compression
Use for sufficiently large compressible responses/requests where CPU/memory trade-off is favorable. Do not recompress already-compressed PDFs/images/archives or buffer unbounded bodies merely to compress them.

### Connection pooling
Use normal provider pooling, but bound/max it from measured rack/database capacity. Pool reuse must not retain Tenant A's DB/RLS context for Tenant B.

Measure at least representative latency percentiles, throughput, query/dependency time, allocation/memory pressure, payload size, and pool wait under the real slice before adding another optimization layer.

## 19. Long-running request-reply

Long operations do not hold an HTTP request open indefinitely.

Baseline flow:

```text
POST command
→ authenticate/authorize/validate/idempotency
→ durable operation/job accepted
→ 202 Accepted
   Location: /api/operations/{id}
   Retry-After: ... where useful
```

Operation resource states are explicit:

```text
Pending
Running
Succeeded
Failed
Cancelled
OutcomeUnknown
```

The status resource includes stable timestamps and a structured error/result where applicable.

If completion creates a separate resource, the status resource can direct the caller to that resource after completion.

Cancellation is only exposed when the underlying operation has a safe cancellation or compensation contract.

## 20. Synchronous versus asynchronous threshold

Keep a command synchronous when its authoritative transaction/validation is expected to finish within the interactive request budget and the user needs the result immediately.

Use async request-reply when:
- work is long-running or resource-heavy;
- an external dependency can take too long;
- document/report/image processing is involved;
- platform maintenance/control work has durable progress;
- request-thread occupancy would create avoidable saturation risk.

Do not queue every command merely because a Worker exists.

## 21. Network and edge contract

Production external application traffic uses HTTPS/TLS. ZITADEL OIDC/OAuth flows also use HTTPS.

The edge may negotiate HTTP/1.1, HTTP/2, or HTTP/3 with clients/backends where supported, but SquiFlow command/resource semantics do not depend on a particular HTTP transport version.

A reverse proxy/API gateway may route traffic, terminate TLS, enforce request-size/WAF/private-access policy, and apply coarse rate limiting. It does not replace backend authentication/authorization/resource checks or make Admin API transit Core API.

WebSocket/SignalR, when used for live UI/signal/wakeup behavior, is non-authoritative: durable business/sync truth remains in DB/outbox/state records.

DNS/hostname information assists routing but is not tenant authority. SSH/private network access is infrastructure recovery/operations only, not a normal tenant business API.

Do not introduce gRPC, MQTT, WebRTC, FTP/SFTP, or raw TCP/UDP as application protocols without a concrete workload that justifies their latency/streaming/device/compatibility characteristics.

## 22. Problem details and error classification

HTTP errors expose safe structured machine-readable results, preferably based on Problem Details semantics, with SquiFlow failure codes such as:

```text
Validation
Unauthenticated
Forbidden
Conflict
PreconditionFailed
AlreadyApplied
RateLimited
DependencyTransient
DependencyPermanent
ResourceExhausted
UpgradeRequired
OutcomeUnknown
InternalDefect
```

Internal stack traces/provider details stay out of ordinary client responses.

Keep the HTTP status and SquiFlow failure code consistent. The following is a baseline mapping, refined by the concrete endpoint rather than invented independently per controller:

| HTTP status | Typical SquiFlow meaning |
|---:|---|
| `400 Bad Request` | malformed request/protocol shape that cannot be evaluated |
| `401 Unauthorized` | no acceptable authentication/session (it does not mean authenticated-but-forbidden) |
| `403 Forbidden` | authenticated actor is not permitted for the function/resource/field |
| `404 Not Found` | resource is absent or deliberately non-disclosed under the endpoint's enumeration policy |
| `409 Conflict` | current domain/workflow/uniqueness state conflicts with the requested transition |
| `412 Precondition Failed` | supplied `If-Match`/expected version is stale or otherwise not satisfied |
| `422 Unprocessable Content` | syntactically valid request fails stable field/business validation where this distinction is useful |
| `429 Too Many Requests` | admission/rate limit; include `Retry-After` when meaningful |
| `500 Internal Server Error` | unexpected SquiFlow defect; safe correlation detail only |
| `502` / `503` / `504` | dependency/gateway unavailable, overloaded, or timed out, with retry classification and no accidental authorization bypass |

Idempotent replay of an already-completed same-intent command normally returns the stored semantic result rather than manufacturing an error solely because it is a replay. `202 Accepted` is used only when a durable operation resource exists.

## 23. API implementation gate

A new mutating endpoint is incomplete until reviewers can answer:

1. What exact business intent does it represent?
2. Can the client retry it?
3. What is the idempotency-key scope?
4. What happens for same key + changed parameters?
5. Where can a duplicate enter: caller, transport, consumer/effect?
6. How long is duplicate suppression/effect evidence retained?
7. What `exactly once` claim, if any, is being made and what exact boundary proves it?
8. What is the expected-version/concurrency rule?
9. What is the authoritative transaction boundary?
10. Does it create async work?
11. What happens if the response is lost after commit?
12. What happens if an external effect succeeds but receipt persistence fails?
13. Which layer owns retry and what is the maximum total attempt budget?
14. Which 4xx/5xx results are retryable?
15. What are request/page/payload/resource/rate limits?
16. What authorization/resource requirement applies?
17. Which backend owns the route: Core API or Admin API?
18. What audit/trace identifiers are recorded?
19. What consistency/freshness contract does any returned derived data have?
20. What API/protocol version compatibility applies to older Workstations/clients?
21. Is this really an HTTP/network boundary, or should the responsibility stay an in-process module call?
