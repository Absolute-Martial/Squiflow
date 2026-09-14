# Workload-Driven Architecture Strategy Selection

**Status:** Accepted architecture-governance direction  
**Version:** v0.0.21  
**Scope:** material architecture, protocol, API, processing, persistence, deployment, compatibility, provider and runtime-mechanism choices

## 1. Decision

SquiFlow selects architecture from the **actual workload and protected requirements**, not from a preferred technology stack, pattern catalogue, maturity ladder, or desire for uniformity.

The governing principle is:

> **Choose the smallest correct strategy that satisfies the workload's hard invariants and material quality requirements. Pay for additional architectural complexity only when a named requirement or measured failure justifies it.**

`Smallest correct` does not mean the fewest files, the cheapest cloud bill, or the least code. It means the least accidental architecture that still truthfully covers correctness, security, availability/degraded behavior, recovery, compatibility, capacity, operability and the product consequence of being wrong.

This document promotes and unifies rules that already exist across:

- `docs/review/bytebytego_308/TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md`;
- `docs/review/bytebytego_308/CRITICAL_INTERROGATION_RULE.md`;
- `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md`;
- `docs/requirements/PRODUCT_TO_QUALITY_TRACEABILITY.md`;
- `docs/decisions/MATERIAL_DECISION_HISTORY.md`;
- focused API, sync, persistence, deployment, security, Worker and Workstation owners.

It does **not** introduce an automated architecture engine, runtime strategy selector, universal scoring algorithm, or another network service.

## 2. Hard invariants filter before optimization

Architecture selection is not a weighted contest where enough latency or cost benefit can cancel a correctness/security invariant.

Evaluate constraints in this order:

```text
1. HardInvariant / mandatory product or legal constraint
        │
        ├── candidate violates it -> eliminate candidate
        │
        ▼
2. DegradedMode / failure and recovery obligations
        │
        ▼
3. OperationalTarget / measurable quality targets
        │
        ▼
4. Cost, simplicity, developer/operator burden and optional preferences
```

Examples:

- stale authorization cannot win because it is faster;
- a cheaper store cannot win if it cannot preserve the required transactional invariant;
- an offline technique cannot win if the operation requires current central authority;
- an always-available fallback cannot win if the safe behavior for a security-sensitive operation is fail-closed.

See `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md` for the accepted `HardInvariant`, `OperationalTarget`, and `DegradedMode` classes.

## 3. Workload / decision profile

Before comparing material strategies, describe the workload sufficiently to explain the decision. Use only dimensions that can change the choice; do not fill a ceremonial checklist for trivial decisions.

A material profile can include:

| Dimension | Questions |
|---|---|
| Business consequence | What customer/business outcome fails if this workload is wrong, slow or unavailable? |
| Authority | What data/effect is authoritative, derived, provisional, cached or advisory? |
| Latency/freshness | Sub-second, seconds, minutes, hours? Is stale data safe? |
| Completeness | Must the answer/effect be final, or can it be provisional/partial? |
| Data size | Bounded request/set, large finite set, or effectively unbounded stream/history? |
| Throughput/volume | Typical and peak records/events/bytes/operations? |
| Burstiness | Steady, reconnect/import burst, scheduled spike, customer-driven burst? |
| Ordering | Is order semantically relevant? Per entity, partition, tenant, or global? |
| Event time / late data | Does when an event occurred matter? Can data arrive late/out of order? |
| Availability | What must continue when a dependency is unavailable? |
| Offline | Must Workstation work continue without network access? For how long? |
| Consistency | Which facts require current authority versus bounded derived staleness? |
| Replay/reprocessing | Must historical input be replayable or recomputable? |
| Recovery | Required restart/resume/reconcile behavior and acceptable recovery time? |
| Compatibility | Which old/new clients, contracts, stored work or binaries can coexist? |
| Client diversity | Web, Workstation, Admin, partner, future client—do their needs actually differ? |
| Security/privacy | Trust boundary, tenant/resource/field sensitivity, data lifecycle? |
| Capacity/resources | Rack/Workstation CPU, RAM, disk, DB connections, network/provider budgets? |
| Operator capacity | Who has to deploy, debug, recover and understand this mechanism? |
| Cost | Infrastructure/provider, engineering, support, failure and business-delay cost? |

Unknown values are recorded as unknown/measurement obligations rather than invented numbers.

## 4. Selection workflow

For a material choice, use this sequence:

```text
Workload / boundary
        │
        ▼
Authority + hard invariants
        │
        ▼
Quality / degraded-mode profile
        │
        ▼
Current / simplest credible mechanism
        │
        ▼
Plausible alternatives only
        │
        ▼
Correctness + failure + compatibility + cost comparison
        │
        ▼
Selected primary strategy
        │
        ├── degraded/fallback behavior where meaningful
        ├── recovery/reconciliation
        ├── migration/exit path
        └── verification evidence
        │
        ▼
Falsification / revisit trigger
```

Do not begin by enumerating every technology in the industry. Compare only alternatives capable of satisfying the workload's actual invariant/quality profile.

## 5. Complexity budget

`Complexity budget` is a review concept, not an arbitrary numeric score.

Every extra mechanism must earn the complexity it introduces. Typical complexity dimensions include:

- another process/service/container;
- another protocol/schema/version lifecycle;
- distributed state or coordination;
- new persistence/replay semantics;
- new credentials/secrets/certificate lifecycle;
- additional failure coupling;
- more deployment/rollback/migration states;
- new observability and support paths;
- specialized operator/developer knowledge;
- another vendor/provider and exit path;
- new local/client compatibility burden;
- extra test matrices and recovery drills.

Ask:

> **Which requirement forces SquiFlow to pay for this additional complexity?**

If no material requirement does, keep the simpler correct mechanism.

This is not an anti-complexity rule. Some complexity is essential. Guard, Workstation local persistence, Admin API isolation, OpenBao, or a future real stream processor can all be justified when the failure/security/workload property cannot be covered safely by something simpler.

## 6. Total cost of ownership, not implementation cost alone

A seemingly simple mechanism can be the wrong choice when it creates unacceptable business delay, failure exposure or recovery cost.

For material alternatives, consider total cost across the relevant dimensions:

```text
Total architecture cost
≈ infrastructure/provider/license
 + implementation/change cost
 + operational/support/on-call burden
 + testing/verification burden
 + failure and recovery cost
 + security/privacy/compliance burden
 + compatibility/migration cost
 + vendor/technology exit cost
 + performance/resource cost
 + business cost of latency, staleness or unavailability
```

Do not pretend these dimensions can always be converted to one exact currency value. The purpose is to expose hidden costs and trade-offs, not to manufacture false precision.

## 7. Primary, degraded, recovery and reconciliation strategies

A material design is incomplete when it describes only its preferred happy path.

Where meaningful, record:

```text
Primary strategy
    = normal preferred execution/data path

Degraded strategy
    = safe reduced behavior during a defined dependency/capacity failure

Fallback
    = alternate mechanism only when it preserves the required semantics

Recovery
    = how the system returns to the normal state

Reconciliation
    = how ambiguous/divergent durable state is compared and corrected
```

Not every workload needs an alternate fallback. For some security/current-authority operations, the correct degraded strategy is explicit unavailability/fail-closed.

Fallback must never silently weaken authority. Examples:

- cached authorization is not a generic fallback for unavailable current authorization;
- stale Workstation inventory is not automatically authoritative shared stock;
- a local queue is not a substitute central database when WebApi loses PostgreSQL;
- plaintext recovery is not a fallback for failed encryption/key management.

## 8. Record rejected alternatives

For a material architecture decision, preserve the plausible alternatives that were seriously considered and why they did not fit **this workload at this time**.

Use reasons such as:

- violates a hard invariant;
- latency/freshness too weak;
- recovery semantics insufficient;
- no replay requirement to justify a stream;
- client divergence too small to justify a BFF;
- process boundary absent, so gRPC adds no useful property;
- operational burden exceeds demonstrated value;
- current provider/hardware cannot support the topology safely;
- requirement is speculative/unmeasured;
- transition/migration risk is disproportionate.

Avoid permanent global statements such as `Kafka is bad`, `GraphQL is better`, or `REST always wins`.

## 9. Processing strategy selection

Batch, incremental processing, micro-batch, durable jobs and streaming are **not a maturity ladder**.

Choose by workload semantics.

### Synchronous/in-process work

Use when the bounded authoritative work fits the interactive transaction/request budget and the caller needs the immediate result.

### Durable job / bounded batch

Fits work that:

- may take longer than an interactive request;
- must survive process restart;
- can process a finite set/chunks;
- benefits from explicit progress/retry/quarantine;
- does not require continuous event-time semantics.

This aligns with the current PostgreSQL/outbox/job/Worker direction.

### Incremental / scheduled reconciliation

Fits cases where only changes since a checkpoint or periodic authoritative repair are needed and seconds/minutes/hours of delay are acceptable.

### Micro-batch

A candidate when latency tighter than ordinary batch is justified but event-by-event stateful stream semantics are not. It still owns checkpointing, overlap/idempotency, resource bursts and recovery.

### Stream/event-log processing

A stronger candidate only when a real workload requires properties such as:

- continuous low-latency processing at sustained volume;
- independently progressing consumers over retained history;
- replay from durable ordered/partitioned input;
- event-time windows;
- late/out-of-order data handling;
- continuous stateful derivation whose business value justifies checkpoint/rebalance/recovery complexity.

Do not select a stream platform merely because data arrives over time.

Periodic reconciliation can complement a live/stream path as repair evidence where the workload benefits, but it is not mandatory architecture for every stream.

## 10. API and composition strategy selection

Use the same framework rather than a fixed decision tree.

Typical progression for a read/use case:

```text
Can one existing bounded application/query operation serve it?
    -> prefer that

Does one host need a stable cross-capability projection?
    -> in-process application/query composition

Are genuine independent remote calls unavoidable?
    -> bounded aggregation + required/degradable/optional semantics

Do materially different client families create persistent lifecycle/payload divergence?
    -> BFF becomes a candidate

Does a real flexible nested/client-selected read workload cause repeated endpoint or over/under-fetch pain?
    -> GraphQL becomes a candidate

Can Workstation satisfy the view from valid local/read-model state?
    -> compose locally
```

BFF or GraphQL is never selected merely because there are two client types. See `docs/architecture/API_AND_PROTOCOL_COMPOSITION.md` once that owner is merged.

## 11. Protocol strategy selection

Protocol choice is tied to a real boundary and required property.

Current examples remain:

```text
same-process module/application interaction
-> direct in-process call

ordinary Web/external business interface
-> REST/task-oriented HTTP baseline

Workstation sync
-> HTTP baseline; gRPC candidate when measured streaming/binary/generated-contract value earns it

live UX signal
-> SignalR/WebSocket/SSE candidate; durable query/state remains recovery authority

external provider
-> provider-required protocol through owning adapter

future real independent synchronous service boundary
-> workload-driven; gRPC can be a strong candidate, not an automatic rule
```

Do not create a universal protocol selector abstraction around local code.

## 12. Schema / compatibility strategy selection

Not every internal type requires an elaborate version registry/transform system.

Ask first whether the representation crosses an **independent evolution or durable persistence boundary**.

If code and representation upgrade atomically inside one process and no durable old value survives, ordinary compile-time change may be enough.

If old/new clients, durable messages/jobs, stored snapshots, local Workstations, APIs or independently deployed producers/consumers can coexist, explicit compatibility/versioning/normalization is required according to the owning contract family.

This keeps compatibility complexity proportional to the evolution boundary.

The schema/contract model is owned by `docs/architecture/SCHEMA_AND_CONTRACT_EVOLUTION.md` once that owner is merged.

## 13. Persistence/data strategy selection

Data technology is selected from authority, transaction, query, scale, recovery and operations requirements—not from a generic SQL-vs-NoSQL rule.

Current baseline remains:

- PostgreSQL for central relational authoritative transactional state;
- SQLite/WAL for Workstation local/provisional durable state;
- object storage for large unstructured objects with DB-owned metadata;
- derived projections/caches only when a named read/latency workload earns them;
- specialized document/search/vector/time-series/stream stores only when a named workload proves enough value to justify another authority/copy/rebuild/backup/tenant/operations contract.

## 14. Deployment / distribution strategy selection

Distribution is also workload-driven.

A module is not promoted to an independent service simply because it has clear domain ownership. A new process/service/node must protect a real fault, security, deployment, scaling, provider or independent-lifecycle boundary.

Current single-rack profile may legitimately use maintenance downtime instead of pretending to offer zero downtime. Canary/blue-green/multi-node orchestration becomes appropriate only when spare topology, routing, schema compatibility, health evidence, rollback/roll-forward and operator capacity exist.

Kubernetes, service mesh and service extraction are positive future mechanisms for the right topology; they are not maturity milestones.

## 15. Hybrid strategies are allowed, but every mechanism needs a job

Uniformity is not a goal by itself. One workload can legitimately combine several strategies when each owns a distinct requirement.

Example:

```text
Workstation local read model
    + bounded sync transport
    + authoritative PostgreSQL command
    + durable after-commit Worker consequence
    + optional live UI signal
```

Another may be a simple synchronous REST request over one module-owned query.

Hybrid does **not** automatically mean better. Every additional mechanism must have:

- a distinct responsibility;
- an authority/freshness contract;
- failure/recovery behavior;
- ownership;
- evidence that it improves the workload enough to justify its cost.

## 16. Decision record for material choices

Do not create an ADR for every local refactor. For a material architecture choice, preserve the smallest useful record from:

```text
Decision / short name
Status
Workload / boundary
Business consequence
Authority / data/effect ownership
Hard invariants
Operational targets / degraded-mode requirements
Measured/known workload profile
Unknowns / measurement obligations
Current mechanism
Plausible candidates
Selected strategy
Why it fits
Rejected alternatives and why
Complexity / TCO obligations
Primary + degraded/fallback behavior
Recovery / reconciliation
Compatibility / migration / exit implications
Evidence / POC / verification
Falsification / revisit trigger
Owner
Supersedes / superseded-by where material
```

This complements `docs/decisions/MATERIAL_DECISION_HISTORY.md`; it does not require a second bureaucracy.

## 17. Evidence and falsification

A selected strategy is not permanent truth.

Record both:

```text
Adoption evidence
= what proves the mechanism is justified now

Falsification / revisit trigger
= what future measurement, incident, customer need, scale, topology or operator burden would cause re-evaluation
```

Examples:

- gRPC reconsidered if representative sync shows HTTP overhead/streaming limitations materially dominate;
- GraphQL reconsidered if real UI read shapes repeatedly cause endpoint proliferation/over-fetching;
- a broker/stream platform reconsidered when independent replaying consumers and sustained event volume appear;
- Kubernetes reconsidered when multi-node desired-state/rollout/service-discovery/failover pain is real;
- an adopted mechanism can also be simplified/removed if its expected benefit does not appear.

## 18. Verification after adoption

Selection is not finished at merge/design time.

After implementation:

1. verify the hard invariants and failure/recovery contract;
2. measure the target workload on representative hardware/topology;
3. compare actual operational/resource/business cost with the decision assumptions;
4. verify degraded/fallback/reconciliation behavior where applicable;
5. update the material decision if evidence materially changes the fit.

A green CI pipeline proves only the scenarios it actually exercised.

## 19. No runtime Architecture Decision Engine baseline

The phrase `architecture decision engine` may describe this methodology informally, but SquiFlow does **not** introduce an automated runtime or AI architecture selector.

Current implementation is repository governance:

```text
workload/requirement profile
+ focused owner rules
+ decision record when material
+ POC/measurement/verification
+ revisit trigger
```

Future tooling may validate required decision fields, generate comparison matrices, link evidence, or flag stale assumptions. It must not silently choose technology or override product/domain/security authority.

## 20. Architectural invariant

> **SquiFlow optimizes for fit, not uniformity. Architecture is selected per real workload after hard invariants and degraded/recovery obligations are known; plausible strategies are compared by total system and business cost; additional complexity must be earned by a named requirement; material decisions record alternatives, recovery and falsification; and hybrid mechanisms are allowed only when each has a distinct justified responsibility.**
