# Worker and Background Runtime — Adjacent Technology Audit

**Status:** Review/evidence record. Accepted selections are promoted to their focused owners; this file does not itself create new runtime authority.  
**Baseline reviewed:** v0.0.18 + accepted Proto.Actor/Quartz Worker selection.

## 1. Purpose

Selecting a Worker runtime and scheduler can accidentally pull in several overlapping frameworks: message broker, workflow engine, retry library, dashboard, distributed lock, actor persistence, actor cluster, second queue, or scheduler HTTP control surface.

This audit asks which adjacent mechanisms SquiFlow actually needs now and which should remain evidence-gated.

## 2. Result summary

| Concern | Current disposition | Reason |
|---|---|---|
| Worker execution/supervision | **SELECTED: Proto.Actor** | Useful actor serialization/supervision inside one Worker process |
| Durable scheduler | **SELECTED: Quartz.NET 4.x** | Mature recurrence/timezone/misfire/persistent-store semantics without replacing Proto.Actor execution |
| Durable business/job store | **KEEP: PostgreSQL/SquiFlow state** | One durable authority for jobs, occurrences, attempts, outbox and business effect state |
| Workstation local signal | **KEEP: System.Threading.Channels** | Lightweight non-durable wake/backpressure after SQLite durability |
| Generic Worker Channel queue | **REJECT AS BASELINE** | Duplicates Proto.Actor mailbox/dispatch and can become a second in-memory queue |
| Actor persistence/event sourcing | **DEFER** | Competes with current relational durable authority without proven benefit |
| Actor remote/cluster | **DEFER** | No initial multi-node distributed-actor requirement |
| Quartz clustering | **DEFER** | Single Worker initially; evaluate independently if multiple scheduler nodes are justified |
| MassTransit | **DEFER POSITIVE CANDIDATE** | Useful if durable messaging middleware/broker semantics become real; not needed for current DB-backed Worker |
| RabbitMQ/Kafka | **DEFER** | No broker/stream-specific workload yet |
| Hangfire | **REJECT BESIDE CURRENT MODEL** | Overlaps durable jobs, workers, retries and scheduling; would create competing runtime authority |
| TickerQ | **DEFER ALTERNATIVE** | Good lightweight scheduler/job runtime but overlaps Proto.Actor execution more than Quartz |
| Workflow/Saga engine | **DEFER** | Explicit SquiFlow durable state machines are sufficient until orchestration complexity proves otherwise |
| Generic distributed-lock service | **REJECT AS BASELINE** | Use database uniqueness/leases/fencing/constraints for the owning durable invariant |
| Direct scheduler dashboard/API | **REJECT AS NORMAL CONTROL PLANE** | Platform controls remain Admin Web → Admin API → versioned SquiFlow commands |
| Retry-everything framework | **REJECT AS GLOBAL POLICY** | Retry ownership remains per dependency/effect boundary |
| HTTP resilience package | **VALIDATE WHEN FIRST REMOTE PROVIDER PATH EXISTS** | Useful implementation aid, but must not create nested retries or hide OutcomeUnknown semantics |
| Separate job monitoring product | **DEFER** | Existing OpenTelemetry + Admin API/operational views should prove insufficient first |
| Service mesh | **REJECT AS BASELINE** | No east-west service topology requiring it |

## 3. Outbound HTTP resilience

When the first Worker workload calls an external HTTP provider, evaluate the current .NET resilience stack (`Microsoft.Extensions.Http.Resilience` / Polly-based resilience) as an implementation aid for bounded timeout/retry/circuit behavior.

Do not adopt one global retry policy for every provider.

Each outbound operation still defines:

- whether it is safe to retry;
- semantic/provider idempotency evidence;
- attempt timeout;
- total retry budget;
- `Retry-After` handling where applicable;
- transient versus deterministic failures;
- circuit/open behavior if sustained failure makes retries harmful;
- `OutcomeUnknown` and reconciliation when the provider may have applied an effect.

A resilience pipeline is execution machinery, not correctness authority.

## 4. Workflow/orchestration engines

Do not add Temporal, Durable Task, Elsa, MassTransit Saga, Quartz workflow chaining, or another orchestration framework merely because the Worker handles background work.

Current SquiFlow already has explicit requirements for:

```text
Pending
Claimed
Running
RetryScheduled
Completed
Failed
Quarantined
Cancelled
OutcomeUnknown
```

and explicit domain/workflow state machines.

Revisit a workflow/orchestration engine only when a real long-running process demonstrates several of:

- many durable steps spanning long time periods;
- wait-for-external-signal semantics;
- compensation across already-completed steps;
- branching/parallel durable orchestration;
- operator-visible resume/replay requirements;
- orchestration code becoming materially harder to test/recover than a purpose-built SquiFlow state machine.

The engine must not silently become business authority for orders/payments/stock/permissions merely because it coordinates steps.

## 5. Distributed locks

Do not introduce Redis/etcd/Consul/distributed-lock packages for ordinary Worker ownership.

Prefer the owning durable store:

- unique occurrence/idempotency constraints;
- atomic claim/update;
- lease expiry;
- claim generation/fencing token when stale owners can cause unsafe effects;
- narrowly selected PostgreSQL locking/isolation where the invariant requires it.

A separate distributed lock is reconsidered only when the protected resource is genuinely outside the authoritative database and no safer provider-native/idempotent mechanism exists.

## 6. Rate limiting versus concurrency limiting

Worker background admission is not the same as HTTP rate limiting.

Use workload/tenant-aware durable admission and actor/dispatcher concurrency bounds for Worker execution.

A remote provider may additionally impose request-rate limits. That belongs to the provider adapter/workload policy and can use a process-local limiter only when losing limiter state on restart is safe. Durable contractual/provider quotas remain in SquiFlow consumption/limit state when required.

Do not use one global token bucket to stand in for tenant fairness, CPU concurrency, provider quota and DB capacity simultaneously.

## 7. Dead letters and quarantine

Proto.Actor dead letters and Quartz error state are useful diagnostics but are not the final SquiFlow poison-work authority.

A durable job that cannot safely progress enters explicit SquiFlow failure/quarantine/reconciliation state with:

- stable job/effect identity;
- failure code/class;
- attempt evidence;
- next/manual action;
- tenant/scope/correlation;
- enough version/context evidence to explain why it stopped.

Framework dead-letter queues may supplement diagnostics, never replace this state.

## 8. Job dashboard and operational UI

Do not expose framework-specific operational dashboards as the normal administration surface.

SquiFlow should expose the operational concepts it owns through Platform Admin Web/Admin API when the first real Worker controls exist:

- backlog/oldest age;
- running/leased work;
- retries;
- quarantine;
- schedule state;
- last/next occurrence;
- pause/drain/resume;
- manual run;
- reconcile/retry under explicit authorization;
- dependency/provider health evidence.

Quartz/Proto.Actor diagnostic surfaces can remain operator/developer tools behind infrastructure controls, but they do not bypass Admin API authorization/audit.

## 9. Time abstraction

Time is important in schedules, leases, retry delays, token validity and observability, but wall clock is not distributed ordering authority.

For application-owned logic, prefer injectable/testable time abstractions (`TimeProvider` or a narrow SquiFlow wrapper only where additional semantics are needed) rather than direct scattered `DateTime.UtcNow` calls.

Quartz owns its trigger-time calculations; SquiFlow tests still need deterministic schedule publication, occurrence identity, lease expiry and retry-delay semantics.

Do not build a custom global clock service merely to wrap one BCL API.

## 10. CPU-heavy Worker workloads

Document generation, image conversion and similar work can differ radically from ordinary async I/O handlers.

Before implementing them:

- classify CPU, memory, temp-disk and native-process cost;
- use separate bounded concurrency/admission where needed;
- keep large payloads behind object-store/claim-check references rather than actor messages/job rows;
- do not execute blocking/native-heavy work on an unconstrained shared dispatcher;
- consider a dedicated helper/process only when crash/isolation/native-library evidence justifies another executable boundary.

Proto.Actor supports workload isolation, but actorization alone does not create resource limits.

## 11. Multi-node evolution

There are several different scale-out decisions and they must not be collapsed:

```text
multiple Worker processes claiming independent DB jobs
≠ Quartz clustering
≠ Proto.Cluster
≠ message broker
≠ Kubernetes
```

The simplest next move may be multiple Worker processes using PostgreSQL claims without distributed actors.

Quartz clustering becomes relevant if multiple scheduler nodes must safely share/recover trigger execution.

Proto.Cluster becomes relevant only when distributed actor identity/placement itself creates material value.

A broker becomes relevant when durable messaging/routing/independent-consumer/replay semantics create material value.

Kubernetes becomes relevant only when deployment/orchestration complexity justifies it.

Measure the actual bottleneck before selecting the next mechanism.

## 12. Package/dependency boundary

Framework types should stay in infrastructure/runtime adapters:

```text
Proto.Actor
Quartz
future resilience package
future broker SDK
```

must not become domain/application vocabulary.

SquiFlow modules expose semantic handler/schedule/job contracts. Runtime adapters translate those contracts into actor messages/Quartz triggers/provider calls.

This keeps later package replacement possible without pretending packages have identical semantics.

## 13. What still needs proof rather than another technology choice

The highest-value remaining Worker questions are empirical:

- first real workload shape and trigger source;
- workload duration distribution;
- I/O versus CPU/memory/temp-disk cost;
- tenant skew/fairness needs;
- expected backlog/burst size;
- provider rate/timeout/idempotency behavior;
- acceptable schedule precision;
- real business timezone/DST needs;
- safe overlap/misfire behavior per schedule kind;
- actual Worker memory/thread/DB connection footprint on the rack;
- whether a single Worker process is sufficient;
- whether Quartz operational overhead is proportionate on the actual deployment.

Do not answer those questions by adding more frameworks before Phase-6 evidence exists.

## 14. External references checked

- Proto.Actor documentation: https://proto.actor/
- Quartz.NET 4.x documentation: https://www.quartz-scheduler.net/documentation/quartz-4.x/
- Hangfire features: https://www.hangfire.io/features.html
- TickerQ documentation: https://tickerq.net/docs/what-is-tickerq
- Microsoft .NET resilience guidance/packages should be rechecked against the current target framework when the first outbound Worker provider path is implemented.
