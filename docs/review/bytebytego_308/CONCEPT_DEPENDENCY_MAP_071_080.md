# ByteByteGo Concept Dependency Map — Entries 071-080 Extension

**Status:** Sequential extension after `CONCEPT_DEPENDENCY_MAP_061_070.md`.  
**Coverage:** archive entries `071-080`; together with the prior maps, concept coverage is current through `080 — Where Do We Cache Data?`.

This extension also applies `CRITICAL_INTERROGATION_RULE.md`: every mechanism must answer what SquiFlow is doing, why, what invariant/failure it protects, what simpler alternative exists, and what evidence would change the decision.

## 1. Domain model / DDD

```text
071 DDD
    -> Entity
        -> stable identity + lifecycle
    -> Value Object
        -> structural/value equality
    -> Aggregate Root / Aggregate
        -> consistency + invariant boundary
        -> transaction / expected-version boundary where applicable
    -> Repository
        -> aggregate-root persistence abstraction where useful
    -> Factory
        -> complex invariant-heavy creation only when earned
    -> Domain Service
        -> domain behavior not naturally owned by one entity/value object
    -> Domain Event
        -> domain fact/notification
        != automatically durable integration event
        != automatically Event Sourcing

SquiFlow critical question
    -> which invariant must be consistent together?
    -> what must commit atomically?
    -> what can happen after commit/outbox?
    -> are we modeling business truth or copying a DDD diagram?
```

## 2. Logical stack versus deployment topology

```text
072 modern stack
    -> Presentation
    -> optional Edge
    -> Integration/API
    -> optional Messaging/Async
    -> Business Logic
    -> Data Access
    -> Data Storage
    -> optional Analytics/ML
    -> Infrastructure

SquiFlow
    -> logical boundaries may coexist in one process
    -> network/process boundary only for real security/fault/runtime need
    -> optional product/logo does not become dependency from the infographic

critical question
    -> what real problem does this added layer solve now?
    -> what failure/operational cost does separation add?
```

## 3. Concurrency and parallelism

```text
073 concurrency
    -> overlapping in-flight work
    -> races / ordering / cancellation
    -> transactions / optimistic concurrency
    -> backpressure / admission / fairness

parallelism
    -> simultaneous compute execution
    -> useful only when compute is actual bottleneck

SquiFlow bounds
    -> DB pool
    -> disk/WAL
    -> provider quota
    -> Worker concurrency
    -> RAM/CPU/network
    -> tenant fairness

critical question
    -> does increasing concurrency improve throughput or amplify contention?
```

## 4. Token format versus identity/authorization architecture

```text
074 JWT vs PASETO
    -> JWT
        -> configurable signed/encrypted token ecosystem
    -> PASETO
        -> version + purpose + opinionated crypto suites
        -> public signed
        -> local encrypted/authenticated

still required either way
    -> issuer/key lifecycle
    -> audience/scope/time
    -> replay/theft handling
    -> session/revocation policy
    -> authorization

SquiFlow
    -> ZITADEL/OIDC for human identity
    -> maintained .NET validation
    -> TenantContext + OpenFGA + domain/current-state authorization
    -> no custom token-format authority without real requirement

critical question
    -> do we actually have a token-format problem, or a different identity/session/authz problem?
```

## 5. Deployment lifecycle and orchestration

```text
075 Pod lifecycle
    -> API desired state
    -> scheduling
    -> runtime/network/storage preparation
    -> waiting/running + probes
    -> graceful termination
    -> cleanup

portable lessons even without Kubernetes
    -> startup != readiness
    -> readiness != liveness
    -> graceful drain
    -> process state disposable
    -> durable state outside process
    -> bounded shutdown/retry

SquiFlow
    -> simple host/container deployment first
    -> Kubernetes only after recurring multi-node orchestration pain

critical question
    -> what operational problem would Kubernetes solve that simpler service/container automation cannot?
```

## 6. CI/CD and release evidence

```text
076 CI/CD
    -> source/change
    -> build
    -> tests/checks
    -> immutable artifact
    -> release/deploy
    -> observe/smoke
    -> recover

SquiFlow release qualification
    -> checksum/provenance
    -> config/secrets outside artifact
    -> migration compatibility
    -> old Workstation / durable-work compatibility
    -> process health + authorized smoke journey
    -> rollback OR roll-forward OR maintenance restore
    -> actual-hardware/provider/restore exercises where CI cannot prove behavior

critical question
    -> what exact production claim does each pipeline stage prove, and what remains unproven?
```

## 7. Versioning domains

```text
077 versioning
    -> SemVer
    -> CalVer
    -> Sequential numbering
    -> API version carrier
        -> path
        -> query/request parameter
        -> header/media-type style

SquiFlow version domains
    -> product release
    -> HTTP API
    -> Workstation sync protocol
    -> central/local schema
    -> Guard/Workstation IPC
    -> durable messages/jobs
    -> auth/config model IDs
    -> rule/workflow/form/config snapshots

critical question
    -> what exactly is versioned, who consumes it, what does compatibility mean, and how is old usage retired?
```

## 8. Verification strategy

```text
078 Testing Pyramid
    -> unit
    -> integration
    -> E2E
    -> cost generally rises upward

SquiFlow refinement
    -> smallest layer that proves real invariant
    -> domain/property
    -> application
    -> real DB/local-store adapter
    -> real ZITADEL/OpenFGA/provider integration
    -> real API/security pipeline
    -> Guard/Workstation process tests
    -> observability runtime
    -> selective E2E
    -> failure injection / migration / restore / actual hardware

critical question
    -> does a mock remove the behavior we actually need to prove?
```

## 9. Conditional container hardening

```text
079 Docker best practices
    -> trusted/pinned base
    -> multi-stage build
    -> .dockerignore
    -> least privilege
    -> external configuration
    -> cache-efficient build
    -> labels/metadata
    -> scanning

SquiFlow additions
    -> no secrets in layers
    -> secret-safe injection
    -> active patch/update cadence
    -> only required ports/capabilities/writable paths
    -> health/shutdown/drain
    -> CPU/RAM/temp/log bounds
    -> immutable-image promotion/provenance

critical question
    -> does containerization actually improve packaging/recovery enough to justify another runtime layer?
```

## 10. Data copies, caches and lifecycle

```text
080 source 'cache everywhere' framing
    -> browser/CDN/cache
    -> service memory/disk
    -> distributed cache
    -> broker retention
    -> search index
    -> DB buffer/materialized view/WAL/logs

critical taxonomy correction
    -> cache
        -> disposable acceleration copy
    -> replica
        -> availability/read copy
    -> queue/stream/log
        -> durable delivery/replay state
    -> search index
        -> derived searchable representation
    -> materialized projection
        -> persisted derived read model
    -> WAL/transaction/replication log
        -> durability/recovery/replication record
    -> backup
        -> recovery copy

lifecycle contract for every copy
    -> authoritative source
    -> tenant/permission scope
    -> freshness
    -> retention
    -> rebuild/reconcile
    -> deletion/correction propagation
    -> outage behavior

critical question
    -> what type of copy is this really, why does it exist, and what does deletion mean for it?
```

## 11. Integrated SquiFlow interrogation after entry 080

```text
proposed mechanism
    -> name exact SquiFlow boundary
    -> name real problem/invariant
    -> state current simpler mechanism
    -> state why proposal helps
    -> enumerate new failure/ops cost
    -> classify authority/derived state
    -> define recovery
    -> define measurement/adoption trigger
    -> define falsification/change trigger
    -> only then consider implementation
```

## Pending future links visible from the index only

These remain **not yet studied**:

- `081` string implementations across languages (next)
- `082` CI/CD follow-up
- `083` Kafka vs RabbitMQ
- `084` database scaling patterns
- `085` Redis query lifecycle/cache follow-up
- `086` data replication
- `087` access control
- `088` API gateway
- `089` load balancing
- `090` third occurrence of architecture resources duplicate
- `093` modular monoliths
- `096` duplicate system-design concepts
- `097` slow API diagnosis
- `099-122` deeper networking/DNS/HTTP/service material
- archive through `123`, then URL section from PDF page `245` onward remains independently pending.
