# ByteByteGo Concept Dependency Map — URL Entries 011-020

**Coverage:** URL occurrences `011-020`, PDF pages `255-264`.

## 1. Data representation and access

```text
authoritative transactional model
    -> normalized around business identities/invariants
    -> constraints + concurrency + tenant scope

real read bottleneck
    -> query/index tuning first
    -> projection/materialized/denormalized read shape if proven
    -> source + freshness + rebuild + authorization
```

Normalization/denormalization are per responsibility, not project-wide philosophies.

## 2. Indexing and API performance

```text
real query/invariant
    -> candidate index
    -> plan/cardinality evidence
    -> measure write/WAL/storage/reconnect cost

API bottleneck
    -> pagination | async telemetry | cache | compression | pool
    -> choose only matching technique
    -> measure whole-system side effects
```

## 3. Code structure

```text
real replacement/provider seam
    -> narrow abstraction + contract test

ordinary cohesive business code
    -> concrete implementation allowed

speculative variation
    -> no ceremonial interface hierarchy
```

## 4. Admission and capacity

```text
authorization -> may actor do it?
rate/admission -> may work enter now?
hard meter/quota -> is durable allowance available?
concurrency bound -> how many execute now?
```

## 5. API styles by exact surface

```text
task/resource command API
    -> HTTP/REST-style where explicit intent is clearest

client-driven nested Web/Admin reads
    -> GraphQL candidate when real UI proves value

Workstation / high-frequency sync RPC
    -> gRPC candidate when streaming/binary/generated-contract value is measurable

live signal
    -> SignalR/WebSocket candidate

durable consequence
    -> outbox/Worker
```

GraphQL is not chosen/rejected from REST comparison. Federation is a separate service/schema-ownership question.

## 6. Edge and service boundaries

```text
Internet/custom domain
    -> edge gateway/reverse proxy capabilities
    -> backend still owns business authorization

same runtime module
    -> in-process

real synchronous service boundary
    -> HTTP/gRPC by evidence

after-commit work
    -> durable async
```

## 7. Data sharing by ownership

```text
current Core/Admin/Worker
    -> shared central DB acceptable
    -> one modular-monolith business authority

future independent service
    -> explicit private authoritative data ownership
    -> API/events/read models for sharing
    -> no other service mutates private tables
```

## 8. API correctness

```text
HTTP verb convention
    != semantic retry safety

retryable mutation
    -> semantic idempotency key + intent binding
    -> concurrency/domain authorization
    -> atomic receipt/mutation/outbox where owned together
```

The integrated rule remains: identify the exact SquiFlow problem and boundary first, then select technology from fit, evidence, recovery and falsification rather than comparison rankings.
