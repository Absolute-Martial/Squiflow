# Concept Dependency Map Extension — URL 031-040

This extends the cumulative ByteByteGo concept map through PDF page `284`. The standing rule remains **fit by SquiFlow boundary**, not winner/loser comparison.

```text
semantic business command
├─ caller retry / response loss
│  ├─ semantic IdempotencyKey
│  ├─ same-key/same-intent replay
│  ├─ same-key/different-intent reject
│  └─ receipt + mutation + outbox atomicity where possible
├─ transport redelivery
│  └─ MessageId / envelope evidence (not business identity)
└─ external effect
   ├─ provider idempotency/reference
   └─ OutcomeUnknown + reconciliation

read path
├─ authoritative normalized relational state
├─ provider-native index (transactionally maintained access path)
├─ optional derived projection/materialized view
├─ optional cache
├─ optional read replica
└─ optional specialized read/search store
   └─ each copy declares source + freshness + tenant/auth + rebuild + deletion + outage

API/read composition
├─ same-process module/query composition (current strongest fit)
├─ bounded task/query HTTP
├─ GraphQL candidate for nested/client-selected reads
├─ BFF candidate for materially divergent client families
├─ client composition for few independent calls
└─ edge composition only with concrete latency/cache/security fit

concurrency
├─ DB constraints
├─ atomic conditional update
├─ expected-version optimistic concurrency (ordinary collaborative edit)
├─ row/range/advisory lock when invariant/contention earns it
└─ stronger isolation/serializable when multi-row invariant earns it
   └─ retry whole transaction under semantic idempotency

schema/version evolution
├─ DB schema
├─ Workstation local schema
├─ HTTP API
├─ sync protocol
├─ durable jobs/messages
├─ Guard IPC
├─ OpenFGA model/tuples
└─ rule/workflow/config snapshots
   ├─ expand
   ├─ overlap readers/writers
   ├─ backfill/switch
   └─ contract only after evidence

verification/resilience
├─ deterministic invariant tests
├─ real-adapter/provider tests
├─ load/stress/endurance
├─ failure injection
├─ restore drills
└─ later bounded chaos experiments
   └─ hypothesis + steady state + blast radius + abort + recovery evidence

architecture-flow patterns
├─ request-response -> immediate answer
├─ API gateway -> north-south edge capability
├─ batching -> sync/import/finite work
├─ orchestration -> explicit owned multi-step workflow
├─ pub/sub -> later independent consumers
├─ ETL -> later analytics/migration movement
├─ stream processing -> later continuous replay/window workload
├─ Event Sourcing -> later event-authoritative domain
└─ peer-to-peer -> only if real independently deployed peer topology exists
```

## Dependency observations

1. **Idempotency depends on authority and restore, not transport choice.** Changing HTTP to gRPC or adding a broker leaves semantic operation identity and effect reconciliation intact.
2. **Read optimization depends on write-path cost and consistency.** A cache/replica/projection is not justified until the authoritative read bottleneck and staleness contract are known.
3. **GraphQL depends on a real composition problem.** It can improve a Web/Admin read surface without changing command idempotency, TenantContext/OpenFGA or persistence authority.
4. **Concurrency and idempotency interact but are distinct.** Idempotency answers repeated intent; optimistic/locking/isolation protects concurrent different or overlapping intents.
5. **API versioning depends on schema evolution but cannot replace it.** Old clients and durable state may outlive route labels.
6. **Chaos/failure injection depends on observability and recovery.** Injecting a fault without a measurable steady state and reconciliation path proves little and can be dangerous.
7. **Pattern catalogs are capability maps.** Request-response, batching, orchestration, pub/sub and streaming may all coexist at different SquiFlow boundaries.

## Questions carried forward

- Which first real Web/Admin screen, if any, proves GraphQL composition benefit?
- Which central DB candidate and exact concurrency primitives survive Phase-3 reconnect/contention tests?
- What idempotency-retention windows are justified per command family?
- What API-version transport fits the actual Web/Workstation/partner tooling once endpoints exist?
- Which failure-injection experiments become release-gate tests versus scheduled/manual qualification?
