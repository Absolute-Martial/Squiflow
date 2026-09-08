# Reliability, API, Idempotency, and Cloud Pattern Source Review

**Version:** v0.0.15

**Status:** source/reasoning record only. Current decisions are owned by the focused architecture documents.

Sources reviewed sequentially:
1. ASP.NET Core policy authorization — https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0
2. System Design Tradeoffs — https://newsletter.systemdesign.one/p/system-design-tradeoffs
3. Stripe idempotency — https://stripe.com/blog/idempotency
4. AWS Builders' Library idempotent APIs — https://aws.amazon.com/builders-library/making-retries-safe-with-idempotent-APIs/
5. Azure Architecture Center patterns — https://learn.microsoft.com/en-us/azure/architecture/patterns/
6. Azure API/background/transient-fault best practices — https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design

## 1. ASP.NET Core policies after OpenFGA selection

ASP.NET Core policies/requirements remain the **in-process API integration primitive**. Multiple requirements in a policy are ANDed; handlers do not rely on execution order or perform business side effects.

The earlier wording "do not build a competing authorization engine" still means SquiFlow should not create a home-grown authorization framework around ASP.NET Core. It does **not** exclude the now-selected OpenFGA external authorization engine.

Current split:

```text
ZITADEL              authentication
ASP.NET Core policy  request/semantic integration
OpenFGA              role/permission/relationship decision
SquiFlow domain       workflow/state/business invariant
DB                    tenant isolation/transaction
```

Resource-dependent checks still use `IAuthorizationService` after tenant-scoped resource loading where practical.

## 2. Tradeoff discipline

Useful current positions:

| Tradeoff | v0.0.15 choice |
|---|---|
| Latency vs throughput | small bounded sync/job batches |
| Freshness vs cache | freshness for auth/financial truth; cache only where safe |
| Utilization vs headroom | preserve burst/recovery headroom |
| Consistency vs availability | stronger authority for payments/stock/permissions; local availability for approved Workstation operations |
| LWW vs conflict | reject global LWW |
| Vertical vs horizontal scale | qualify constrained current hardware first |
| Stateful vs stateless | stateful local-first Workstation; server process memory non-authoritative |
| Monolith vs microservices | modular monolith business core |
| Queue vs event log | durable queue/outbox; no Kafka baseline |
| At-least-once vs exactly-once | at-least-once + idempotent/reconcilable effects |
| Deep queue vs backpressure | bounded queues/admission/fairness |
| Minimal components vs resilience | keep Guard/provider/auth boundaries where they protect a real requirement |

The main rule is: state the concrete SquiFlow invariant/workload before citing a generic tradeoff.

## 3. Stripe + AWS idempotency

Current accepted consequences:
- caller-provided semantic idempotency key for retryable meaningful mutations;
- request/trace/message/entity IDs remain separate;
- same key + same intent returns the same/semantically equivalent result;
- same key + changed intent is rejected;
- do not infer idempotency identity solely from payload hash;
- where mutation + receipt + outbox share a store, commit them atomically;
- retain idempotency evidence long enough for the operation family, including long-offline Workstation retries;
- finite retry + backoff/jitter and no nested retry storms.

See `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`.

## 4. Azure pattern catalog classification

The catalog is not an implementation backlog.

### Adopt/adapt where already justified
- Asynchronous Request-Reply — for genuinely long-running operations.
- Bulkhead-style bounded work/dependency pools.
- Claim Check — large files referenced rather than embedded in messages.
- Compensating Transaction — business correction/reversal for effects outside one ACID transaction.
- Competing Consumers — when multiple Worker instances actually exist.
- Health Endpoint Monitoring.
- Idempotent Consumer.
- Priority Queue with fairness/aging.
- Queue-Based Load Leveling for heavy async work.
- Rate Limiting/Throttling.
- Retry with classification/budget.
- Static Content Hosting/CDN for safe immutable assets.
- Valet Key only when object transfer flow proves signed direct access useful.
- Sidecar/process-isolation concept **selectively**: `SquiFlow.Guard` is now an accepted independent supervision/recovery process; additional helpers still require a specific native/heavy/driver fault need.

### Defer/reject until evidence
- per-frontend BFF services;
- event sourcing;
- full CQRS dual-store architecture;
- Saga as default core transaction model;
- sharding;
- deployment stamps/geodes/active-active multi-region;
- leader election where atomic claims/leases suffice;
- gateway/aggregation services without need;
- Redis/cache infrastructure without measured value.

## 5. API design consequences

Use resource-oriented HTTP where natural and semantic commands where they clarify the business action:

```text
GET  /api/orders/{id}
POST /api/orders
POST /api/quotes/{id}/approval
POST /api/payments/{id}/refunds
```

Other retained rules:
- GET/HEAD have no business mutation side effects;
- retryable POST mutations use SquiFlow idempotency;
- long-running work uses `202 Accepted` + durable operation resource;
- pagination/result limits are server-enforced;
- projection cannot bypass property authorization;
- ETags/`If-Match` can expose optimistic concurrency while domain version remains correctness boundary;
- no full HATEOAS baseline.

## 6. Background/retry reliability

When Worker exists:
- restart-safe durable work;
- graceful drain;
- at-least-once duplicate handling;
- transient/permanent/unknown classification;
- quarantine;
- least-privilege identity;
- queue age and completion latency, not only depth;
- tenant fairness;
- large payloads externalized by reference.

Retry rules:
- only transient faults;
- finite attempt/elapsed budget;
- honor `Retry-After`;
- jitter where useful;
- include ZITADEL/OpenFGA/provider SDK retries in aggregate retry budgets rather than multiplying unseen retries.

## 7. Host/custom-domain and external dependency lessons

- Host headers are input, not tenant authority.
- Custom domains map only through verified SquiFlow registration.
- Third-party responses remain untrusted input even when provider is managed.
- ZITADEL/OpenFGA failure cannot become accidental allow.
- Hugging Face/Kaggle provider errors must map to stable SquiFlow results without erasing provider-specific evidence operators need.

## 8. Corrected implementation rules

1. Do not add a pattern because a catalog lists it.
2. Do not remove a boundary merely because a smaller diagram looks cleaner.
3. Semantic idempotency is mandatory where retry can duplicate meaningful effects.
4. Retry is finite/classified/budgeted.
5. At-least-once transport implies idempotent/reconcilable effects.
6. ASP.NET Core remains the API authorization integration layer; OpenFGA is the selected external application-authorization engine.
7. ZITADEL is the selected identity provider/platform.
8. `SquiFlow.Guard` is accepted for Workstation supervision/recovery; arbitrary additional helper processes remain evidence-driven.
9. `IObjectStore` and `IBackupTarget` are accepted because provider migration at first paying customer is already planned; this does not justify generic one-interface-per-provider architecture elsewhere.
10. Full CQRS/event sourcing/Saga/sharding/multi-region remain deferred.
11. Web remains online-only for business operations in v0.0.15.
12. Workstation local-first correctness keeps all defined crash/offline/conflict/recovery behavior even when the codebase is kept structurally lean.
