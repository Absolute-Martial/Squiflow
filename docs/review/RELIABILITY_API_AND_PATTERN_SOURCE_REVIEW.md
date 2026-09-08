# Reliability, API, Idempotency, and Cloud Pattern Source Review

**Version:** v0.0.15

**Purpose:** Review the supplied sources sequentially, extract only what improves SquiFlow, and explicitly reject/defer patterns that would add complexity without a present problem.

Sources reviewed:

1. ASP.NET Core policy-based authorization
   - https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0
2. System Design Tradeoffs
   - https://newsletter.systemdesign.one/p/system-design-tradeoffs
3. Stripe — Designing robust and predictable APIs with idempotency
   - https://stripe.com/blog/idempotency
4. AWS Builders' Library — Making retries safe with idempotent APIs
   - https://aws.amazon.com/builders-library/making-retries-safe-with-idempotent-APIs/
5. Azure Architecture Center cloud design patterns catalog and the current implementation-relevant pattern pages
   - https://learn.microsoft.com/en-us/azure/architecture/patterns/
6. Azure API design/implementation and related best-practice pages
   - https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design
   - https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-implementation
   - background jobs, caching, CDN, monitoring, transient faults, autoscaling, data partitioning, host preservation and message encoding pages from the same best-practices catalog.

---

## 1. ASP.NET Core policy-based authorization

### Source-supported observations

ASP.NET Core policies are named sets of requirements. Multiple requirements inside one policy are ANDed. Multiple handlers for the same requirement can provide OR-style success. `IAuthorizationService` evaluates requirements/policies, optionally against a resource. Handlers must not depend on execution order; handlers can still run even when authentication fails, and `context.Fail()` has stronger semantics than simply not succeeding a requirement.

The framework also supports custom policy providers that can generate policies dynamically.

### SquiFlow decision

**Adopt the framework; do not build a competing authorization engine.**

Use ASP.NET Core policies/requirements as the runtime primitive for HTTP authorization.

Examples:

```text
TenantAuthenticatedRequirement
PlatformOperatorRequirement
ManageRolesRequirement
ApproveQuoteRequirement
RefundPaymentRequirement
AdjustInventoryRequirement
```

Resource-dependent checks use `IAuthorizationService` after tenant-scoped resource loading.

### What not to do

- Do not encode all business authorization into JWT claims.
- Do not rely on endpoint/UI visibility.
- Do not depend on handler invocation order.
- Do not put business side effects in authorization handlers.
- Do not call `context.Fail()` casually when multiple handlers might legitimately satisfy a requirement.
- Do not introduce a custom dynamic policy provider merely because the framework supports it. Use one only if stable permission-policy generation becomes repetitive enough to justify it.

### Implementation implication

Endpoint policies should be coarse and reusable; semantic business requirements stay explicit.

```text
request
→ authentication
→ coarse endpoint policy
→ tenant-scoped resource load
→ semantic resource authorization
→ domain/workflow validation
→ transaction
```

---

## 2. System Design Tradeoffs

This source's useful contribution is not a pattern to copy. It reinforces that every architecture choice trades one constraint for another.

### SquiFlow tradeoff position

| Tradeoff | v0.0.15 choice | Why |
|---|---|---|
| Latency vs throughput | Small bounded sync/job batches | Avoid per-item network overhead without delaying interactive work excessively |
| Cache hit rate vs freshness | Freshness wins for permissions/financial truth; cache static/reference data selectively | Stale authority is more dangerous than a cache miss |
| Utilization vs headroom | Keep server burst/protection headroom | Initial hardware is constrained and retry/load spikes must not collapse it |
| Consistency vs availability | Consistency for payments/stock/credit/permissions; local availability for explicitly local-capable Workstation operations | One policy does not fit every aggregate |
| Serializable vs weaker isolation | Select per invariant after DB POC | Stronger isolation is not free and exact product remains open |
| Optimistic vs pessimistic concurrency | Optimistic for normal edits; locking/claims for high-contention/ownership cases | Most edits should not block, but job claims/stock can require stronger coordination |
| Last-write-wins vs explicit conflicts | Reject global LWW | Orders/payments/stock/permissions cannot safely accept blanket LWW |
| Normalization vs denormalization | Normalize authoritative model first; derive read models selectively | Keeps write truth explainable while allowing measured read optimization |
| Relational vs NoSQL | Relational semantics remain strongest hypothesis; provider remains open | Domain has relationships, financial allocations, constraints and transactional outbox needs |
| Vertical vs horizontal scaling | Vertical/single-node qualification first, horizontal app nodes later | Avoid distributed complexity before workload requires it |
| Stateful vs stateless compute | Stateless server compute; stateful local Workstation | Matches deployment and offline requirements |
| Monolith vs microservices | Modular monolith business core | Independent deployment only where already justified: Web/API/Worker/Desktop |
| Shared DB vs DB per service | One central authoritative transactional store baseline | We do not have independent microservice data ownership yet |
| Single model vs CQRS | Single authoritative write model; command/query separation in code; projections only when measured | Full CQRS infrastructure is not free |
| Sync vs async calls | Synchronous for short authoritative work; async for durable long-running work | User needs immediate result for ordinary transactions but not PDF/report/maintenance execution |
| Queue vs event log | Durable work queue/outbox baseline; no Kafka/event-log baseline | Worker tasks do not require many replaying consumers |
| At-least-once vs exactly-once | At-least-once delivery + idempotent semantic effects | More realistic than promising transport-level exactly-once |
| Total vs partitioned ordering | No global order; enforce order only per consistency key where required | Preserves concurrency |
| REST vs GraphQL | REST baseline | GraphQL has no demonstrated requirement yet |
| Retry vs load amplification | Bounded retries + retry budgets + backoff/jitter | Retry storms can turn a dependency issue into a system outage |
| Deep queue vs backpressure | Bounded queues with age/bytes/count plus admission/backpressure | Infinite queue depth hides failure and creates stale work |
| Active-active vs active-passive | Neither as baseline; start single region/node, define HA only after RTO/RPO | HA architecture must follow recovery requirements, not precede them |

### Main skeptical rule

Do not cite a generic tradeoff to justify a technology. State the SquiFlow workload/invariant that makes one side of the tradeoff preferable.

---

## 3. Stripe idempotency article

### Source-supported observations

Stripe exposes caller-provided idempotency keys on mutating requests so a failed network request can be retried with the same key without duplicating the charge. It also recommends exponential backoff and jitter to avoid hammering a degraded service.

### SquiFlow decision

**Adopt the semantic idea broadly for retryable mutating APIs.**

The semantic business idempotency key is separate from request/trace/message IDs.

```text
same business intent
→ same idempotency key
→ same effect / semantically equivalent result
```

Use the pattern for sync items, payments/refunds, order creation where retry ambiguity exists, durable admin proposals, document requests and other non-harmless mutations.

Do not make every GET/read invent an idempotency record.

---

## 4. AWS Builders' Library idempotent APIs

This source materially strengthens the Stripe-level design.

### Caller intent beats request hashing

AWS explains why deriving a duplicate token from request parameters can be wrong: two identical parameter sets can be two intentionally separate requests. Therefore SquiFlow uses caller-generated semantic keys rather than treating a payload hash as the idempotency identity.

### Receipt + mutation atomicity

Where one data store owns both, the idempotency receipt and business mutation must be in one ACID transaction.

### Semantic-equivalent retry result

A retry should generally receive the same business meaning as the original successful operation, rather than a confusing `already exists` response that forces the caller to determine whether it created the resource.

### Same key, different parameters

Reject as a validation/idempotency mismatch. Store enough canonical request information to detect this safely.

### Late arrivals and retention

Idempotency records need explicit retention by command family. Infinite retention is not automatically correct, but a short arbitrary TTL is also unsafe for long-offline Workstations or significant financial effects.

### SquiFlow implementation contract

See `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`.

---

## 5. Azure cloud design pattern catalog

Microsoft explicitly recommends selecting patterns based on the problem/constraint being solved and accepting each pattern's tradeoffs. SquiFlow follows that rule. The catalog is **not a backlog**.

### Current classification of the catalog

| Pattern | SquiFlow status | Reason |
|---|---|---|
| Ambassador | Defer | No current need for a helper network proxy per consumer |
| Anti-Corruption Layer | Adapt selectively | Good boundary for legacy/external systems with different semantics; keep translation out of domain |
| Asynchronous Request-Reply | Adopt selectively | Fits long-running document/report/admin operations; use 202 + operation status rather than holding HTTP request |
| Backends for Frontends | Defer | Web/Desktop/Admin can use one Core API initially; separate BFFs add services/network hops until interface pressure proves need |
| Bulkhead | Adapt | Separate Worker work classes/dependency concurrency pools; do not introduce cell architecture automatically |
| Cache-Aside | Adapt when measured | Reconstructable cache only; no Redis baseline |
| Choreography | Defer/avoid for core workflows | SquiFlow has explicit workflow/Worker ownership; hidden event choreography makes recovery harder for current scale |
| Circuit Breaker | Adapt per failing remote dependency | Useful when retry alone is harmful; not universal or a substitute for exceptions |
| Claim Check | Adapt for large payloads | Store large files/diagnostics in object storage and pass a reference rather than giant queue messages |
| Compensating Transaction | Adapt for distributed/irreversible effects | Use business corrections/refunds/reversals where one ACID rollback cannot undo external effects |
| Competing Consumers | Adopt in Worker evolution | Multiple Worker instances can claim independent durable work; ordering still scoped per key |
| Compute Resource Consolidation | Adopt operationally | Co-locate separate deployables on the initial server where resource tests permit; do not erase process boundaries |
| CQRS | Adapt narrowly | Explicit commands/queries and optional projections; no separate read store/event bus until measured |
| Deployment Stamps | Defer | Multi-stamp scale/isolation not needed for initial deployment |
| Event Sourcing | Reject baseline | Audit/history needs do not justify making event replay the authoritative persistence model |
| External Configuration Store | Adapt | SquiFlow already needs durable typed/versioned admin configuration; use selected central store/secret mechanisms rather than add Azure App Configuration by default |
| Federated Identity | Adopt concept | OIDC/federated browser identity is already current direction |
| Gatekeeper | No extra service | Edge/Core API already authenticate/validate/admit requests; a dedicated gatekeeper host would duplicate responsibility |
| Gateway Aggregation | Defer | No demonstrated need for a separate aggregation tier |
| Gateway Offloading | Adapt at edge only where natural | TLS/CDN/access/routing can be provider-managed; business authorization remains SquiFlow |
| Gateway Routing | Adapt | Reverse proxy/edge routes Web/API/admin/custom domains; do not make it a new domain service |
| Geode | Defer | Active-active multi-region is far beyond current RTO/RPO/workload evidence |
| Health Endpoint Monitoring | Adopt | Separate liveness/readiness/functional health with safe dependency detail |
| Idempotent Consumer | Adopt | Worker/outbox/sync consumers assume duplicate delivery can occur |
| Index Table | Defer as separate pattern | Start with selected DB indexes; add derived index structures only after query evidence |
| Leader Election | Avoid baseline | Prefer atomic claims/leases/scheduler occurrence identity; introduce a leader only for a proven singleton coordination need |
| Materialized View | Later/measured | Useful reporting/read optimization after schema/query/index work is insufficient |
| Messaging Bridge | Defer | No two incompatible message systems to bridge |
| Pipes and Filters | Selective | Useful for document/import/image pipelines; don't split simple jobs into gratuitous stages |
| Priority Queue | Adapt | Keep explicit business work classes/priorities and prevent starvation through fairness/aging |
| Publisher-Subscriber | Selective internal events | Use outbox/domain notifications when multiple consumers genuinely exist; no broker requirement just to publish events |
| Quarantine | Adopt selectively | Poison jobs, suspicious uploads/imports can be isolated pending operator/review path |
| Queue-Based Load Leveling | Adopt for Worker | Buffer bursty heavy async work; do not queue low-latency synchronous business commands by default |
| Rate Limiting | Adopt | Protect APIs/providers/tenant fairness; distinct from resource-aware admission |
| Retry | Adopt with classification | Retry only transient failures; finite budgets/backoff/jitter/Retry-After |
| Saga | Defer for core domain | Modular-monolith + central transaction is simpler. Use orchestration/compensation only when a real distributed transaction spans external participants |
| Scheduler Agent Supervisor | Adapt concepts | Checkpoints, durable progress, retries and supervision already belong in SquiFlow Worker; don't create another generic framework unless needed |
| Sequential Convoy | Defer until ordering need | Use per-key sequencing only for specific ordered message families |
| Sharding | Defer | No evidence central store needs horizontal shards; design IDs/tenant scope so future evolution remains possible |
| Sidecar | Selective | Guard/helpers can isolate native/heavy work when same-host lifecycle/resource isolation justifies it; don't sidecar every cross-cutting concern |
| Static Content Hosting | Adopt for Web assets | CDN/static hosting for immutable versioned assets; authenticated business data remains API-controlled |
| Strangler Fig | Historical/selective migration tactic | Useful concept for incremental legacy replacement, not a permanent runtime component |
| Throttling | Adopt | Tenant/workload/dependency limits protect small server and noisy-neighbor scenarios |
| Valet Key | Adapt for large object transfer | Short-lived narrowly scoped signed upload/download capability can avoid streaming all large bytes through Core API; authorize before issuing it |

### Important pattern combinations for SquiFlow

Use only where the workload exists:

```text
Retry + timeout + retry budget + optional circuit breaker
Queue load leveling + competing consumers + idempotent consumer
Priority classes + fairness/aging + throttling
Async request-reply + idempotency + durable Worker status
Valet key + object metadata lifecycle + authorization
Compensation + workflow only when external/distributed effects escape one transaction
```

---

## 6. Azure API design and implementation guidance

### Resource-oriented HTTP, without REST dogma

Use nouns/resources for normal resource APIs, but SquiFlow business transitions are not forced into generic CRUD if doing so hides intent.

Good current style can include both:

```text
GET /api/orders/{id}
POST /api/orders
POST /api/quotes/{id}/approval
POST /api/payments/{id}/refunds
```

### HTTP semantics

- GET/HEAD have no business mutation side effects.
- PUT/DELETE are idempotent when used.
- POST commands that can duplicate significant effects require SquiFlow idempotency semantics.
- Correct status codes and safe structured error responses are part of the API contract.

### Long-running operations

Adopt 202 Accepted + an operation status resource for work that should not hold the request connection. Include `Location` and useful `Retry-After`. Persist Pending/Running/Succeeded/Failed/Cancelled/OutcomeUnknown rather than making status a transient in-memory concept.

A retry with the same idempotency key returns the same status resource instead of enqueuing duplicate work.

### Pagination/filtering

Every large collection has defaults and maximums. Field selection/projection is authorization-aware so callers cannot request hidden cost/margin/security fields merely by naming them.

### ETags and optimistic concurrency

ETags/`If-Match` can expose resource versions to Web clients. SquiFlow still treats the underlying domain version as the correctness boundary; ETags are one HTTP representation of it.

### HATEOAS

**Do not adopt full HATEOAS as a baseline requirement.** The source itself notes there is no general-purpose standard for modeling it. SquiFlow clients are controlled first-party clients with versioned contracts; full hypermedia everywhere would add payload/design complexity without a demonstrated need.

Use explicit links/action metadata selectively if it materially improves a client workflow.

### API versioning

Compatibility matters because Workstations can skip releases and Web/API may roll independently. Exact public URL/header/media-type versioning remains an implementation decision; the core requirement is explicit contract/schema versioning and an overlap window rather than silent breaking changes.

---

## 7. Azure API/background/reliability best practices

### Background jobs

The source reinforces existing Worker requirements:
- restart-safe checkpoints;
- graceful shutdown/drain;
- at-least-once duplicate handling;
- transient versus permanent failure classification;
- dead-letter/quarantine;
- least-privilege Worker identity;
- avoid sensitive data in queue payloads where a reference is sufficient;
- track completion, queue age and end-to-end correlation;
- scale background work separately from the Web/API when needed.

For SquiFlow, queue **age/oldest work** is often more useful than raw depth because a large fast-moving low-value queue may be healthy while a small stalled critical queue is not.

### Caching

Use cache-aside/reconstructable caches only after measured value.

- Cache is not authoritative or a backup.
- Permission/financial/stock freshness can require bypass/revision validation.
- Shared Redis is not baseline.
- Local bounded caches are preferred first when useful.

### CDN

Use CDN/static caching for hashed Web assets and repeated immutable/public-safe object delivery where policy allows. Do not let CDN caching silently expose authenticated tenant data.

### Monitoring

Keep correlation from user/API operation through DB/outbox/Worker/external dependency. Track job completion, not just start; queue age, retry volume and missed schedules are operational signals.

### Transient faults

Retry only transient faults, use finite budgets, set timeouts before retries, honor `Retry-After`, avoid nested retry multiplication, add jitter for background operations, and never endlessly retry deterministic failures.

### Autoscaling

Future horizontal scale should follow useful signals such as queue age/business critical time and dependency capacity—not CPU or raw queue depth alone. This is later-deployment guidance, not a requirement to add autoscaling to the initial homelab.

### Data partitioning

Do not shard now. Keep tenant/aggregate keys explicit so data can be partitioned later if proven. Cross-partition transactions/joins introduce cost and complexity; the current central transactional model benefits from staying together initially.

### Host name preservation

Custom-domain host values are request input, not a security proof. Validate forwarded-host/proxy configuration against trusted proxy boundaries and authoritative SquiFlow domain registration. Never infer tenant authorization solely from an arbitrary Host header.

### Message encoding/versioning

Durable messages/contracts include schema/version metadata and remain compatible across rolling/skipped versions. Avoid provider-specific envelope types in application contracts.

---

## 8. Resulting implementation rules

1. Do not add an architecture pattern because it appears in a catalog.
2. For every pattern, identify the concrete SquiFlow failure/performance/security problem first.
3. Semantic idempotency is mandatory for mutating operations whose retries could duplicate a meaningful effect.
4. Same idempotency key + different intent is an error.
5. Retry receipt + authoritative mutation are atomic when they share one store.
6. At-least-once Worker/message delivery is assumed; effects are made idempotent/reconcilable.
7. Retry is finite, classified and budgeted; no nested retry storms.
8. Queue age and business criticality matter more than queue depth alone.
9. Long-running operations use durable async status; ordinary short transactions remain synchronous.
10. ASP.NET Core policy/resource authorization remains the runtime; no SquiFlow authorization microservice is added.
11. Full CQRS, event sourcing, Saga, BFFs, sharding, leader election and multi-region patterns are deferred until real constraints justify them.
12. No full HATEOAS baseline.
13. Use ETags/version tokens and explicit conflict handling rather than global LWW.
14. Web stays online-only for business operations in v0.0.15.
15. Workstation local-first sync retains stable item-level idempotency across transport retries/batches.
