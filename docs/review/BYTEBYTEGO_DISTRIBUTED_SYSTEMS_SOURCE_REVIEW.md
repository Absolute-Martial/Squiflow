# ByteByteGo Distributed-Systems Source Review — v0.0.15

**Status:** Source-backed follow-up review. Accepted architecture remains owned by the focused current documents.

## Reading limitation

The supplied ByteByteGo pages expose their title, introduction, and article scope publicly, but most detailed body sections are behind the publication paywall. This review records only what the accessible source text actually supports. It does not pretend the hidden paid sections were read. Where SquiFlow conclusions go beyond the visible source, they are explicitly labeled as SquiFlow synthesis and are checked against already-reviewed first-party/standards material where relevant.

## Batch 1 sources reviewed sequentially

1. CQRS — https://blog.bytebytego.com/p/a-pattern-every-modern-developer
2. Retry — https://blog.bytebytego.com/p/a-guide-to-retry-pattern-in-distributed
3. Event-driven architecture — https://blog.bytebytego.com/p/a-guide-to-event-driven-architectural
4. Messaging patterns — https://blog.bytebytego.com/p/messaging-patterns-explained-pub
5. Idempotency/delivery/deduplication — https://blog.bytebytego.com/p/a-detailed-guide-to-idempotency-delivery
6. Background work — https://blog.bytebytego.com/p/background-work-from-cron-jobs-to
7. Multi-tenancy — https://blog.bytebytego.com/p/a-guide-to-multi-tenancy-benefits

## Batch 2 sources reviewed sequentially

8. Container design patterns — https://blog.bytebytego.com/p/container-design-patterns-for-distributed
9. API cross-cutting concerns — https://blog.bytebytego.com/p/must-know-cross-cutting-concerns
10. Database performance strategies — https://blog.bytebytego.com/p/database-performance-strategies-and
11. API security — https://blog.bytebytego.com/p/how-to-implement-api-security
12. Event sourcing — https://blog.bytebytego.com/p/event-sourcing-explained-benefits
13. Stateless architecture — https://blog.bytebytego.com/p/stateless-architecture-benefits-and
14. Authentication techniques — https://blog.bytebytego.com/p/top-authentication-techniques-to

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

SquiFlow does **not** turn simplified queue wording into an end-to-end exactly-once transport guarantee. The separate ByteByteGo idempotency article explicitly focuses on delivery semantics, duplicates, deduplication windows, and the boundaries of “exactly once.” SquiFlow therefore retains:

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

This confirms the existing pooled-by-default design rather than changing it.

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

# Batch 2

## 8. Container design patterns for distributed systems

### Source-supported points

The public article frames containers as composable distributed-system building blocks rather than only packaging units. It says the article covers six patterns split between cooperation on one machine and coordination across multiple machines, and explicitly warns that the patterns are not rules; they are recurring answers to recurring problems.

The public ByteByteGo text does not expose the detailed paid pattern sections, so this review does not attribute hidden pattern names/details to the article.

### SquiFlow synthesis

Containerization is a deployment/runtime tool, not a reason to create more application boundaries.

Current implications:
- `SquiFlow.Guard` is a Windows Workstation companion process, not a container sidecar pattern.
- Core API and future Worker may be containerized for deployment if that helps the rack/deployment process, but container boundaries do not turn modules into services.
- do not add a per-process proxy/adapter/sidecar merely because container patterns exist;
- do not add leader election when database claims/leases/stable scheduler occurrences solve the actual coordination need;
- Worker's durable work queue is justified by workload semantics, not by a container-pattern catalog;
- scatter/gather-style distributed fan-out is deferred until an implemented report/search/batch workload proves it necessary.

Existing Azure/Google pattern reviews remain the source for any specific sidecar/ambassador/adapter/leader/work-queue terminology; ByteByteGo's publicly visible text only supports the broader compositional/pattern-not-rule lesson.

**Audit result: KEEP container deployment optional and problem-driven; no new container runtime components.**

---

## 9. Cross-cutting concerns in API development

### Source-supported points

The public article explicitly names authentication, logging, rate limiting, and input validation as concerns that apply across many routes and can be catastrophic when inconsistently applied. Its central point is uniform application across the API surface rather than endpoint-by-endpoint memory.

### SquiFlow decision

**ADOPT the uniformity requirement, not a giant middleware layer.**

The Core API should centralize truly cross-cutting behavior through ASP.NET Core pipeline/policies/endpoint metadata where appropriate:

```text
request correlation / safe logging
→ rate/admission limits
→ authentication
→ authoritative tenant/platform context
→ coarse endpoint policy
→ schema/input validation
→ tenant-scoped resource loading
→ OpenFGA/resource authorization
→ domain/workflow/concurrency checks
→ business execution
```

Important distinction:
- authentication, safe error formatting, correlation, generic request limits and coarse policies are broadly cross-cutting;
- resource authorization depends on the actual resource and often happens after load;
- business invariants remain in domain/application code, not generic middleware;
- health/public callbacks may legitimately use a different policy set but must be explicit exceptions rather than forgotten routes.

SquiFlow should generate/test endpoint metadata so every reachable endpoint declares its audience/authentication/policy/limits or an explicit public exception.

**Audit result: KEEP centralized cross-cutting enforcement + endpoint metadata completeness tests; REJECT one middleware that tries to own business authorization/validation.**

---

## 10. Database performance strategies and hidden costs

### Source-supported points

The article explicitly frames database optimization as trade-offs. Its public introduction gives three examples:
- indexes improve reads but add write cost;
- caching reduces database load but introduces stale data;
- denormalization speeds some reads but makes updates harder.

It also warns that a query that works on a small table can become slow after significant growth.

### SquiFlow decision

**ADOPT measurement-driven performance qualification.**

The central/local database POCs must not be judged only on tiny development datasets.

For implemented hot paths, test:
- representative small data and projected larger cardinalities;
- query plans and index usage;
- tenant-scoped composite indexes;
- write/import/sync cost after adding indexes;
- RLS overhead if PostgreSQL is selected;
- connection-pool wait/saturation;
- temp/WAL/disk growth;
- pagination behavior under changing/large data;
- any cache's freshness and invalidation contract.

Do not add an index because a column appears in a WHERE clause without checking write/storage cost. Do not denormalize authoritative business truth before a measured query requires it. Do not use cache for permissions/payments/stock unless its freshness contract preserves correctness.

No sharding, Redis, read replica, or materialized-view platform is selected merely from theoretical future scale.

**Audit result: KEEP relational-first hypothesis but strengthen performance evidence and growth tests.**

---

## 11. How to implement API security

### Source-supported points

The public article makes one particularly important distinction: an API can authenticate credentials correctly yet still be insecure if it does not authorize access to the specific requested resource. It frames API security as choosing multiple strategies according to threat/scenario rather than checking one security box.

### SquiFlow decision

This **confirms**, rather than replaces, the existing OWASP/ZITADEL/OpenFGA architecture:

```text
ZITADEL authentication
≠ OpenFGA permission/resource authorization
≠ TenantContext/data isolation
≠ SquiFlow business/workflow validity
```

HTTPS, a valid token, or a valid OpenFGA relationship never by themselves prove that an arbitrary tenant resource may be read/changed.

The existing API gate remains:
- authenticate;
- derive tenant context;
- tenant-scope lookup;
- coarse function authorization;
- resource/property authorization;
- validate business state/concurrency;
- bound resource use;
- audit/correlate important effects.

No additional SquiFlow authentication or authorization framework is introduced.

**Audit result: KEEP current layered API-security model.**

---

## 12. Event sourcing explained

### Source-supported points

The public article defines the motivation clearly: ordinary CRUD keeps current state but overwrites/deletes prior values, while event sourcing addresses systems that need to answer not only “what is the state?” but also “how did we get here?” It explicitly describes event sourcing as more demanding and says the article covers benefits and trade-offs.

### SquiFlow decision

**REJECT event sourcing as the v0.0.15 persistence baseline.**

SquiFlow does need history in selected areas, but that does not require rebuilding every aggregate from an event stream.

Current mechanisms remain:
- authoritative current relational state;
- immutable/append-only business records where the domain requires them (payments, stock movements, issued documents, corrections/reversals, etc.);
- explicit audit/security history for privileged changes;
- transactional outbox for post-commit integration/work;
- versioned rules/workflows/forms.

Important terminology rule:

```text
append-only audit/history
or transactional outbox
≠ event sourcing
```

Revisit event sourcing only if an implemented domain genuinely requires replay-derived authoritative state/time-travel semantics strongly enough to pay the event schema evolution, projection, rebuild, debugging, and operational costs.

**Audit result: KEEP explicit history/audit; event sourcing remains deferred/rejected as baseline.**

---

## 13. Stateless architecture

### Source-supported points

The public article corrects a common misconception: stateless architecture does not mean the application has no state; it means state is moved elsewhere. Sessions, tokens, preferences and other application memory still exist, and the trade-off is about where that state lives and what relocation costs.

### SquiFlow decision

This materially clarifies the existing server wording.

For SquiFlow:

```text
stateless Core API / Worker process
= process memory is not the only authoritative durable business state
```

It does **not** mean:
- the product has no sessions;
- no caches exist;
- no connection/circuit state exists;
- every node can replace another transparently without shared dependencies/topology support;
- Workstation/Guard are stateless.

Authoritative/recoverable state belongs in the appropriate durable/shared systems: central DB, object storage, job/outbox store, ZITADEL, OpenFGA, backup/configuration, and the local Workstation DB for offline work.

### Blazor-specific SquiFlow synthesis

Current .NET 10 documentation states that Interactive Server Blazor is stateful and normally holds user state in a server-memory circuit. A lost/original server may make that in-memory circuit unavailable; distributed circuit persistence and/or session affinity can matter for multi-node hosting.

Therefore SquiFlow must not claim Web nodes are transparently stateless until the chosen Blazor render/session mode is explicit. High-value business state still belongs in server-side business drafts/DB, not only component/circuit memory.

**Audit result: KEEP stateless server-compute goal, but explicitly model where state actually lives and keep Blazor circuit topology OPEN until Phase 1.**

---

## 14. Authentication techniques

### Source-supported points

The public article frames authentication as both a security and UX concern and says developers must trade off security, scalability, and usability while defending against problems such as session hijacking, token theft and replay. It introduces multiple authentication techniques but the detailed paid comparison is not publicly visible.

### SquiFlow decision

Do **not** interpret “multiple authentication techniques” as a requirement for SquiFlow to implement its own collection of password/OTP/passkey/MFA/session stacks.

ZITADEL remains the selected authentication platform and owns supported authentication methods, MFA/passkey/SSO policy, credential handling, and identity-provider functionality. SquiFlow owns:
- OIDC integration;
- session/application binding;
- tenant/device mapping;
- step-up requirements for risky application actions;
- rate/abuse controls around SquiFlow-facing flows;
- OpenFGA/application authorization after authentication.

Workstation still uses system browser + Authorization Code + PKCE `S256`; it does not collect the user's primary password itself.

Authentication-method expansion (for example passkeys or enterprise federation) is enabled/configured through the chosen identity platform only when the product/customer requirement justifies it.

**Audit result: KEEP ZITADEL/OIDC boundary; do not build competing authentication mechanisms inside SquiFlow.**

---

## Combined decision audit after both batches

The fourteen ByteByteGo articles do **not** justify a rewrite into CQRS microservices, event sourcing, Kafka, sidecar-heavy container architecture, distributed caches, sharding, or a home-grown identity platform.

They strengthen these distinctions:

```text
command vs query
command/job vs event
queue vs pub/sub vs stream
transport delivery vs semantic business effect
authentication vs authorization vs tenant isolation
stateless process vs durable/shared application state
cross-cutting infrastructure vs resource/domain-specific logic
performance optimization vs the cost it adds elsewhere
```

### Result for v0.0.15

KEEP / STRENGTHEN:
- task-oriented command/query separation;
- synchronous authoritative transactions where appropriate;
- transactional outbox and durable Worker jobs;
- finite classified retry and scoped idempotency;
- pooled tenancy with tenant-aware resource limits;
- centralized API cross-cutting enforcement plus explicit endpoint metadata;
- ZITADEL authentication + OpenFGA authorization + SquiFlow domain/isolation separation;
- measured DB/index/cache performance at realistic growth levels;
- explicit durable state placement rather than vague “stateless” claims;
- selective append-only audit/history without event-sourcing the product;
- Guard and other accepted correctness/recovery boundaries.

DEFER/REJECT unless evidence appears:
- separate CQRS read/write databases;
- event sourcing as the authoritative persistence model;
- event-driven-everything;
- Kafka/event-stream infrastructure;
- generic pub/sub broker;
- sidecar/ambassador/leader/scatter-gather infrastructure without a concrete deployment/workload problem;
- Redis/sharding/denormalization/read replicas merely for hypothetical scale;
- home-grown password/MFA/passkey authentication stack.

The objective remains disciplined completeness: remove accidental ceremony, never required correctness/recovery/security behavior.