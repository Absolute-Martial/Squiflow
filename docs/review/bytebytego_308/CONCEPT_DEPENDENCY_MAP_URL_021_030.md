# ByteByteGo Concept Dependency Map — URL Entries 021-030

**Coverage:** URL occurrences `021-030`, PDF pages `265-274`.

## 1. Interface/protocol selection starts from boundary semantics

```text
same runtime module
    -> in-process

ordinary browser/external command/resource
    -> HTTPS + task-oriented HTTP

real typed/streaming synchronous process boundary
    -> gRPC candidate by evidence

live UI signal
    -> polling/SSE/WebSocket/SignalR candidate
    -> never durable truth

long-running work
    -> durable operation + Worker
```

Protocol/channel establishment never replaces TenantContext, OpenFGA, domain rules, idempotency, or concurrency.

## 2. Messaging and broker escalation

```text
authoritative transaction
    -> DB commit + outbox where consequence exists

one durable task
    -> DB-backed job/competing Worker path first

several independent consumers
    -> durable fan-out/pub-sub candidate

replay / independent offsets / sustained stream semantics
    -> broker/event-stream platform candidate
```

Selection is driven by storage/delivery/retention/throughput/recovery requirements, not Kafka-vs-RabbitMQ imagery.

## 3. Delivery semantics remain application-aware

```text
transport message ID
    != business semantic identity

at-least-once/redelivery
    -> idempotent effect or reconciliation

publish/ack timeout
    -> OutcomeUnknown until resolved

exactly-once feature
    -> scoped guarantee only
    -> does not erase external side-effect ambiguity
```

## 4. Consistency follows the invariant

```text
payment / stock / credit / tenant-sensitive auth / hard limit
    -> current/transactional authority

derived dashboard/search/report/cache/projection
    -> eventual allowed when temporary staleness is safe
    -> source + sequence/effect identity + freshness + rebuild

Workstation local-first
    -> explicit LocalCommitted/PendingRemote/Conflict/etc.
    -> not vague eventual-consistency labeling
```

## 5. Async interaction lifecycle

```text
short authoritative command
    -> synchronous definitive result

long-running/resource-heavy operation
    -> 202/durable operation state + Worker + status

server-driven transient UI update
    -> SSE/WebSocket/SignalR candidate

external callback
    -> webhook authenticity + idempotency + retries + reconciliation
```

`202 Accepted` means accepted for durable processing, not business success.

## 6. Deployment strategy depends on real topology

```text
one active rack node
    -> reproducible maintenance deployment may be safest

multi-node/spare capacity + compatible state + routing
    -> rolling/canary/progressive options become real

parallel environment capacity
    -> blue-green-style option

separate deploy from user exposure
    -> feature/configuration gate candidate
```

Rollout strategy does not make incompatible data migration or one physical failure domain disappear.

## 7. Observability is correlated bounded evidence

```text
metrics
    -> trend/saturation/class-level signal

logs/events
    -> contextual transition/failure evidence

traces
    -> distributed execution path

TraceId != CorrelationId != CausationId

authoritative audit
    -> separate durable product/security state
```

Telemetry failure/quota/sampling must not change business correctness.

## 8. Batch versus streaming

```text
finite import/report/rebuild/reconciliation
    -> durable batch/job

continuous result + event-time/window/late-data need
    -> streaming candidate

independent replay/offset ownership at scale
    -> strengthens stream-platform case
```

Product logos are examples; no Kafka/Flink/Spark selection follows from the visual.

## 9. Multi-tenancy as several isolation axes

```text
actor identity
    -> ZITADEL

execution tenant scope
    -> authoritative TenantContext

resource/action permission
    -> OpenFGA + domain/current state

pooled data
    -> tenant-scoped persistence + constraints + provider defense in depth

shared compute/Worker/provider
    -> bounded global/per-tenant fairness/admission

cache/object/read-model/telemetry/backup
    -> explicit tenant isolation

future dedicated profile
    -> residency/compliance/contract/noisy-neighbor/SLO/enterprise trigger
```

The current pooled baseline is an operating-fit choice with strong isolation obligations, not a claim that pooled storage universally beats dedicated storage.

## 10. Integrated interrogation path

```text
article introduces a technology/pattern
    -> identify the exact SquiFlow boundary
    -> name the real invariant/problem
    -> explain why current mechanism exists
    -> identify what alternative property could help
    -> state authority and what remains unchanged
    -> model failure/recovery/operator burden
    -> require adoption evidence
    -> record falsification trigger
    -> only then propose owner-architecture change
```

No entry in URL `021-030` creates a winner/loser technology decision by comparison alone.
