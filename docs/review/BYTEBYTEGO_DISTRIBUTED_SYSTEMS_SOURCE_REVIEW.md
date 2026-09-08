# ByteByteGo Distributed-Systems Source Review — v0.0.15

**Status:** Source-backed follow-up review. Accepted architecture remains owned by the focused current documents.

## Reading limitation

The supplied ByteByteGo pages expose their title, introduction, and article scope publicly, but most detailed body sections are behind the publication paywall. This review records only what the accessible source text actually supports. It does not pretend the hidden paid sections were read. Where SquiFlow conclusions go beyond the visible source, they are explicitly labeled as SquiFlow synthesis and are checked against the already-reviewed Stripe/AWS/Azure/OWASP/OpenID/Zanzibar material.

Sources reviewed sequentially:

1. CQRS — https://blog.bytebytego.com/p/a-pattern-every-modern-developer
2. Retry — https://blog.bytebytego.com/p/a-guide-to-retry-pattern-in-distributed
3. Event-driven architecture — https://blog.bytebytego.com/p/a-guide-to-event-driven-architectural
4. Messaging patterns — https://blog.bytebytego.com/p/messaging-patterns-explained-pub
5. Idempotency/delivery/deduplication — https://blog.bytebytego.com/p/a-detailed-guide-to-idempotency-delivery
6. Background work — https://blog.bytebytego.com/p/background-work-from-cron-jobs-to
7. Multi-tenancy — https://blog.bytebytego.com/p/a-guide-to-multi-tenancy-benefits

---

## 1. CQRS

### Source-supported points

The article defines CQRS as separating commands/writes from queries/reads. It notes that the separation can exist at different levels, from distinct models to separate services or databases, and frames CQRS as a decision rather than something every application must implement identically.

### SquiFlow decision

**ADAPT, not full adoption.**

SquiFlow keeps clear command/query responsibility in application code:

```text
Command
→ expresses a business intent
→ may mutate authoritative state
→ authorization/domain/concurrency/idempotency apply

Query
→ returns data
→ does not perform business mutation
→ tenant/read authorization still applies
```

Material business actions remain task-oriented (`ApproveQuote`, `RefundPayment`, `AdjustInventory`) rather than collapsing into generic CRUD.

Do **not** infer from CQRS that SquiFlow needs:
- a separate read database;
- a separate write database;
- event sourcing;
- command/query microservices;
- a broker between every write and read;
- a read-model project for every screen.

Separate read projections/materialized views are introduced only when an implemented query/report proves the authoritative model is insufficient for latency/load/usability.

**Audit result: KEEP narrow command/query separation; DEFER full CQRS infrastructure.**

---

## 2. Retry pattern

### Source-supported points

The article starts from the fact that distributed calls cross unreliable networks. Retry can trade latency for availability, but indiscriminate retries can amplify latency, consume resources, and contribute to cascading failure.

### SquiFlow decision

**KEEP and strengthen the existing retry contract.**

A retry is allowed only when all of these are true:

```text
failure is plausibly transient
+ retry is inside a finite time/attempt budget
+ the operation is naturally safe or engineered idempotent/reconcilable
+ retry does not violate current authority/business state
```

One dependency call path should have an intentional retry owner. Do not accidentally multiply retries across Workstation → Core API → application code → Worker → provider SDK.

No automatic retry for deterministic validation, authorization, version conflict, unsupported protocol, or permanent provider rejection.

**Audit result: KEEP; no new retry subsystem.**

---

## 3. Event-driven architecture

### Source-supported points

The article contrasts direct synchronous calls, which can work well for smaller/predictable systems, with event-driven communication where a component publishes something meaningful that happened and other components react asynchronously. It also notes that EDA solves coupling/failure/slow-chain problems but introduces its own patterns/problems.

### SquiFlow decision

**ADOPT selectively, not as the system-wide architecture.**

SquiFlow distinguishes:

```text
Command / Job
= instruction that somebody owns and must execute

Event
= fact that already happened
```

A short authoritative business transaction remains synchronous when the user needs a definitive result. After commit, the transactional outbox can publish durable consequences/events for work that can occur later.

Examples:

```text
InvoiceIssued (fact)
→ generate document
→ send notification
→ update derived reporting view
```

The event does not replace the transaction that authoritatively issued the invoice.

Do not use hidden event choreography as the primary correctness mechanism for payments, stock, permission changes, or other flows where SquiFlow needs an explicit owner and recoverable state.

**Audit result: KEEP selective events/outbox; REJECT event-driven-everything.**

---

## 4. Messaging patterns: queue, pub/sub, event stream

### Source-supported points

The article distinguishes three patterns:
- queue/task distribution — one work item is handled by one consumer path;
- publish/subscribe — one published item fans out to multiple subscribers;
- event stream — a durable replayable log where consumers maintain their own progress.

It explicitly says each pattern solves a different problem and trades reliability, ordering, throughput, and complexity differently.

### SquiFlow decision

Select the messaging shape from the actual semantic requirement:

| Need | SquiFlow starting pattern |
|---|---|
| One durable background task should be processed by one worker | Queue/job |
| Several independent consumers must react to the same committed fact | Pub/sub or multiple durable outbox deliveries |
| Consumers need durable replay/history/independent offsets at meaningful scale | Event stream, only if proven |
| Caller needs immediate authoritative answer | Direct synchronous API/application call |

The queue article introduction uses simplified wording about work being processed “once and only once.” SquiFlow does **not** turn that phrase into a transport guarantee. The separate ByteByteGo idempotency article explicitly focuses on delivery semantics, duplicates, deduplication windows, and the boundaries of “exactly once.” SquiFlow therefore retains its current assumption:

```text
transport/redelivery may duplicate
→ semantic consumer/effect must be idempotent or reconcilable
```

No Kafka/event-stream platform is baseline merely because event streams are a valid messaging pattern.

**Audit result: KEEP Worker queue baseline; pub/sub/stream are conditional tools.**

---

## 5. Idempotency, delivery semantics, deduplication

### Source-supported points

The article's public scope explicitly covers:
- multiple delivery semantics;
- duplicates entering at producer, broker, and consumer stages;
- natural idempotency versus an endpoint engineered to behave idempotently;
- idempotency-key failure modes;
- finite deduplication windows;
- the limited/bounded meaning of “exactly once” in real systems.

### SquiFlow decision

This strengthens the existing end-to-end model.

Duplicate defense is not one table at one layer:

```text
producer/caller retry
→ stable semantic IdempotencyKey

transport/broker redelivery
→ MessageId/transport evidence where useful

consumer/effect replay
→ semantic effect receipt/idempotent operation/reconciliation
```

A broker dedupe feature does not replace business idempotency. An API idempotency receipt does not make an external payment provider exactly-once. A provider idempotency key does not eliminate the need to reconcile a response-loss/`OutcomeUnknown` case locally.

Every dedupe guarantee has a retention boundary. High-risk financial/business-effect evidence may outlive ordinary queue/message dedupe records.

Use the phrase **exactly once** only with a named scope, for example one ACID transaction in one database. Do not claim end-to-end exactly-once for a distributed workflow unless every boundary actually proves it.

**Audit result: KEEP and clarify end-to-end duplicate handling.**

---

## 6. Background work: cron to distributed systems

### Source-supported points

The article identifies several reasons work moves out of the request path:
- a user action triggered it;
- time/clock triggered it;
- another system triggered it;
- volume/batching made deferred processing worthwhile.

It also describes a progression from a simple scheduled script on one machine toward more capable background-work strategies as workload/reliability needs grow.

### SquiFlow decision

Background work is classified by trigger and execution semantics rather than by one generic `Job` label:

```text
User-triggered consequence
Scheduled occurrence
External-system-triggered work
Batch/volume-triggered work
Platform control command
```

A schedule/cron expression is only the **trigger**. Important work becomes a durable occurrence/job before execution so crashes, retries, duplicate schedule firings, and operator recovery can be reasoned about.

SquiFlow synthesis for scheduled work:
- give each scheduled occurrence a stable identity;
- persist whether it is pending/running/completed/retryable/failed;
- define timezone/DST behavior where business time matters;
- do not let two scheduler instances create duplicate business effects silently;
- do not move short authoritative commands to the background merely because a Worker exists.

The first Worker implementation may use a simple durable database-backed job/schedule mechanism if it meets the real workload. A distributed broker/scheduler is not required by the article's progression.

**Audit result: KEEP separate Worker boundary; start with the simplest durable mechanism that satisfies the first real workload.**

---

## 7. Multi-tenancy

### Source-supported points

The article's public overview covers:
- shared versus dedicated customer data placement;
- isolation/sharing choices extending beyond the database into compute;
- noisy-neighbor effects and quotas/limits;
- blast radius;
- tenant context propagating through the system.

### SquiFlow decision

This strongly confirms the existing pooled-by-default design rather than changing it.

Current model remains:

```text
ZITADEL authenticated actor
→ authoritative SquiFlow TenantContext
→ OpenFGA application authorization
→ tenant-scoped data access
→ tenant-aware jobs/objects/logs/limits
```

Pooled storage does not mean unbounded shared compute. Worker concurrency, expensive report/document work, provider budgets, and queues remain tenant-aware so one tenant cannot consume the whole system.

Dedicated DB/object/worker/stack profiles remain future targeted isolation options when compliance, residency, SLA, workload, or contractual evidence requires them.

**Audit result: KEEP current multi-tenancy architecture.**

---

## Combined decision audit

These seven articles do **not** justify a rewrite into CQRS microservices, event sourcing, Kafka, or a general event bus.

They do justify keeping four distinctions extremely clear:

```text
command/query responsibility
synchronous authority vs asynchronous consequence
queue vs pub/sub vs stream
transport delivery vs semantic business effect
```

They also reinforce that background work and multi-tenancy require explicit failure/resource ownership rather than merely fewer components.

### Result for v0.0.15

KEEP:
- task-oriented commands and separate read responsibility;
- synchronous authoritative business transactions where appropriate;
- transactional outbox;
- durable Worker jobs;
- finite classified retry;
- semantic idempotency + consumer reconciliation;
- pooled tenancy + tenant-aware resource limits;
- Guard and other previously accepted correctness/recovery boundaries.

DEFER unless evidence appears:
- separate CQRS read/write databases;
- event sourcing;
- event-driven-everything;
- Kafka/event-stream infrastructure;
- generic pub/sub broker;
- distributed scheduler infrastructure beyond the first proven Worker requirement.

The objective is not minimal component count. It is the smallest architecture that preserves the complete behavior each implemented slice actually requires.
