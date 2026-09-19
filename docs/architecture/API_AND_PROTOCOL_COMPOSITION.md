# API and Protocol Composition

**Status:** Accepted architecture refinement  
**Version:** v0.1.0
**Scope:** cross-capability reads, client-facing composition, remote dependency aggregation, protocol placement, local-first Workstation composition, and interaction with schema/contract compatibility

## 1. Decision

SquiFlow treats **API composition** and **protocol selection/composition** as related but separate concerns.

API composition answers:

> Which capability-owned data/results are combined into one client/use-case result, and which application layer owns that combination?

Protocol selection answers:

> When a real process/external boundary exists, which transport/protocol carries the interaction?

Neither concern changes the core modular-monolith rule:

> If the participating capabilities already execute inside one SquiFlow process, compose them in-process. Do not manufacture HTTP/gRPC service calls merely so an aggregator/gateway can call them.

This document refines the existing API-composition review direction. It does not select GraphQL, a BFF tier, edge composition, a generic composition engine, a protocol-resolver service, or service-per-module topology as baseline.

Detailed related owners remain:

- `docs/architecture/AUTHORITATIVE_CAPABILITY_MODULES.md` for authoritative capability ownership;
- `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md` for module/query/persistence boundaries;
- `docs/architecture/SCHEMA_AND_CONTRACT_EVOLUTION.md` for version overlap, resolver/normalizer and compatibility governance;
- `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md` for HTTP/API semantics;
- `docs/api/TRANSPORT_SELECTION.md` for HTTP/gRPC and other transport selection;
- `docs/sync/SYNC_AND_AUTHORITY.md` for Workstation synchronization authority;
- `docs/server/WORKER_RUNTIME_AND_SCHEDULING.md` for durable background execution.

## 2. Composition vocabulary

Use these terms deliberately.

| Concept | SquiFlow meaning |
|---|---|
| **Client-side composition** | Client obtains multiple independent results and combines them locally. |
| **Application/query composition** | One SquiFlow host/application query composes several capability-owned read results in-process. This is the current server-side default when data/logic is already local. |
| **Aggregation** | Independent component results are combined, commonly with bounded concurrent execution when genuine independent calls exist. |
| **Orchestration** | Later work depends on earlier outcomes or coordinates stateful multi-step execution. It is not merely a composite read. |
| **BFF** | Client-family-specific backend shape/lifecycle, introduced only when client divergence creates a real ownership/release problem. |
| **GraphQL composition** | Client-selected/nested read composition through GraphQL if a real flexible-query problem earns it. |
| **Edge composition** | Safe read-oriented composition/caching close to clients where latency/cache/security constraints justify it. |
| **Protocol selection** | Choosing HTTP, gRPC, provider protocol, in-process call, durable job, etc. for an actual boundary. |
| **Contract resolution** | Normalizing a supported older wire/durable contract into the current application contract at the boundary. |
| **Local Workstation composition** | Combining capability-owned local/read-model state inside the Workstation without remote fan-out. |

Do not use `composition`, `aggregation`, and `orchestration` interchangeably.

## 3. Current default: same-process composition

The normal SquiFlow business core is a modular monolith.

For a screen/read use case that needs Orders, Customers, Inventory and Pricing data already available inside the same runtime boundary:

```text
Web/Admin request
      │
      ▼
composite application/query use case
      │
      ├── Orders query surface
      ├── Customers query surface
      ├── Inventory query surface
      └── Pricing query surface
      │
      ▼
client-facing projection
```

The implementation may use capability-owned query surfaces, optimized projections, joins/read models where ownership permits, or other concrete data-access techniques. It does **not** need four network calls simply because the result spans four capabilities.

Rules:

- capability modules retain ownership of their business meaning and protected data semantics;
- the composite use case owns the cross-capability client/read projection when that projection has genuine use-case value;
- no generic gateway/controller may bypass module ownership with arbitrary business SQL;
- protected mutations still execute through the owning authoritative application path;
- a composite read must not mutate authoritative state as a hidden side effect;
- query composition must preserve TenantContext, authorization/field rules, freshness and bounded result size.

## 4. Client-side composition

Client-side composition is allowed when it is genuinely simpler and bounded.

Good candidates include a few independent low-latency reads where:

- partial results are understandable;
- round-trip cost is acceptable;
- duplicate composition logic across clients is small;
- the client does not need to understand internal capability/service topology;
- authorization and field scope remain enforced by each server operation.

Do not make it the default for complex views when it causes:

- many WAN round trips;
- duplicated timeout/retry/merge logic;
- inconsistent composition across Web/Workstation/future clients;
- fragile cross-version coordination;
- partial-failure behavior that every client must reinvent.

The existence of client-side composition does not permit the client to become business authority.

## 5. Aggregation versus orchestration

### Aggregation

Aggregation combines independent results.

For genuine independent remote/provider calls, bounded concurrency can reduce critical-path latency:

```text
request
  ├── dependency A ──┐
  ├── dependency B ──┼──> combine result
  └── dependency C ──┘
```

Do not interpret this diagram as permission to split in-process modules into remote services.

When parallel work exists, define:

- one overall request deadline/budget;
- per-dependency timeout budget;
- bounded concurrency;
- cancellation propagation where supported;
- retry ownership to avoid amplification;
- required/degradable/optional failure behavior;
- cache/freshness semantics;
- observability without unbounded metric cardinality.

### Orchestration

Orchestration coordinates dependent or stateful work:

```text
step A
  │ result/state
  ▼
step B
  │
  ▼
step C
```

That may involve domain/application coordination, workflow state, durable jobs, retries, compensation/reconciliation, idempotency, timeouts or external effects.

Therefore:

> A read-composition layer must not silently become SquiFlow's workflow/orchestration engine.

Long-running or crash-recoverable orchestration remains owned by the appropriate application/workflow/Worker mechanisms rather than an HTTP response aggregator.

## 6. Dependency criticality and partial results

Where a composed result depends on independently failing remote/derived sources, classify each dependency/use-case contribution explicitly.

Conceptual classes:

```text
Required
Degradable
Optional
```

These are policy concepts first; they do not require a universal `CompositionDependency` class.

### Required

Failure means the composite result cannot honestly satisfy its contract.

### Degradable

A bounded fallback/stale projection/partial representation is acceptable and clearly represented.

### Optional

The result remains useful without the contribution.

Each composed dependency defines as applicable:

```text
criticality
freshness expectation
source/authority
request/dependency timeout
retry ownership
fallback/cache policy
partial-result representation
security/field scope
resource/concurrency bound
```

If five genuinely independent dependencies are all mandatory, end-to-end availability can be lower than each dependency's availability. Approximate multiplication can be a useful intuition for independent failures, but it is **not** an SLO formula: real failures may be correlated and dependencies may have different availability models.

The architectural requirement is simpler:

> Do not accidentally make optional presentation data a mandatory dependency for an otherwise valid business result.

## 7. API gateway boundary

The edge/reverse proxy/API-gateway capability may own or assist with:

- TLS termination;
- host/path routing;
- request-size limits;
- WAF/access policy;
- coarse rate/admission limits;
- protocol/HTTP negotiation;
- correlation/request metadata;
- edge observability;
- authentication handoff/validation where the selected topology safely supports it.

But the gateway is not the authoritative business layer.

It must not replace:

- current application/resource authorization;
- TenantContext resolution/isolation;
- field-level permission;
- domain invariants;
- idempotency/concurrency;
- authoritative usage/limit decisions;
- capability-owned data/business semantics.

Business read composition stays in an application/query owner unless a measured BFF/edge composition boundary is deliberately introduced.

## 8. Backend-for-Frontend

A BFF is allowed, not baseline.

Introduce one only when materially different client families create a real problem such as:

- incompatible payload/latency requirements;
- different release cadence/ownership;
- persistent endpoint proliferation to satisfy one client without harming another;
- materially different composition/failure/cache behavior;
- independently evolving client teams that justify a separate backend lifecycle.

Do not create `WebBff`, `MobileBff`, `WorkstationBff` projects/services merely because those names appear in architecture patterns.

A BFF remains an adapter/composition boundary. It does not duplicate capability business rules or become a second authority.

## 9. GraphQL

GraphQL remains deferred until a real client-driven query-composition requirement justifies it.

A valid candidate is a flexible/nested Web/Admin read surface where task HTTP produces repeated over-fetching/under-fetching or endpoint proliferation that is demonstrated rather than assumed.

If introduced, the GraphQL surface must explicitly own:

- tenant/resource/field authorization;
- query depth/complexity/cost limits;
- bounded pagination;
- N+1 avoidance/batching/data-loader behavior where needed;
- cache/freshness semantics;
- schema/deprecation/version-overlap policy;
- introspection/persisted-query policy appropriate to the audience;
- tracing/query-count/dependency evidence.

N+1 prevention is an implementation responsibility of the selected GraphQL/query layer. It does not justify a universal platform service before GraphQL exists.

GraphQL changes a client/query interface. It does not change capability authority, persistence ownership, or the requirement for current authorization and domain invariants.

## 10. Edge composition

Edge composition is a later, requirement-driven option for suitable read-oriented data such as cacheable/public-like metadata or other explicitly safe projections.

Before introducing it, define:

- whether the data can safely be cached/composed outside the authoritative application process;
- tenant/user/permission dimensions of the cache key;
- freshness and invalidation;
- residency/privacy constraints;
- behavior when the edge is stale/unavailable;
- which origin checks must still run;
- whether the latency benefit is measured.

Do not put transactional domain decisions, current sensitive authorization, payment/stock/credit/hard-limit authority, or secret-bearing data at the edge merely for lower latency.

## 11. Protocol selection is a separate axis

SquiFlow does not force every composed dependency through one protocol.

Current direction is:

| Boundary/workload | Current direction |
|---|---|
| Same-process capability composition | direct in-process application/query calls |
| Tenant Web/external business API | REST/task-oriented HTTP baseline |
| Flexible nested Web/Admin reads | GraphQL only if a measured need earns it |
| Workstation synchronization | ordinary HTTP is simpler baseline; gRPC remains a preferred candidate to compare in Phase 3 |
| Future real synchronous service/process boundary | choose from workload evidence; gRPC is a preferred candidate, not an automatic default |
| Durable after-commit/background execution | PostgreSQL/outbox/job/Worker baseline; broker only if a real workload earns one |
| External provider/integration | provider-required protocol behind the owning integration adapter |
| Workstation local data access | in-process capability/read model + SQLite; no network protocol required |
| Live UI signal | SignalR/WebSocket/SSE only when needed; never durable authority |

Do not turn this table into a generic runtime protocol switch.

## 12. No universal protocol resolver service

A composition use case should know the concrete application boundary it calls.

Inside one process:

```text
composite query
  -> capability application/query surface
```

At an actual remote boundary:

```text
owning adapter
  -> HTTP/gRPC/provider protocol
```

SquiFlow does **not** require:

```text
composition engine
  -> generic CapabilityInvocation
  -> dynamic REST/gRPC/local protocol resolver
  -> every capability
```

unless a future plugin/process topology proves that dynamic protocol polymorphism solves a real replacement/deployment problem.

Protocol neutrality is achieved first through correct dependency direction and capability-owned application contracts, not by wrapping every call in a universal invocation framework.

## 13. Composition and schema/contract resolution

Composition must interact cleanly with version compatibility, but it must not become the place where all historical schemas live forever.

At a long-lived external/process boundary:

```text
old/new wire contract
        │
        ▼
boundary-specific contract resolver/normalizer
        │
        ▼
current application command/query/value model
        │
        ▼
composition/use-case logic
```

This ordering keeps compatibility translation near the boundary.

Rules:

- old API/sync/durable contracts are normalized before current business composition where practical;
- composed application logic operates on current internal contracts rather than branching on many historical client versions;
- capability/public contract ownership remains explicit in the source-controlled compatibility registry;
- GraphQL/REST/gRPC/Sync compatibility rules remain technology/contract-family specific;
- protocol choice does not define schema compatibility;
- schema version does not define protocol choice.

See `docs/architecture/SCHEMA_AND_CONTRACT_EVOLUTION.md`.

## 14. Workstation local-first composition

The Workstation changes the usual API-composition trade-off because it has a local SQLite/WAL store and capability-owned local/provisional/read state.

Prefer local composition when the required data is validly available locally:

```text
Workstation UI
      │
      ▼
local application/read composition
      │
      ├── Orders local/read state
      ├── Customers local/read state
      ├── Inventory local/read state
      └── other allowed local projections
      │
      ▼
local UI projection
```

This avoids unnecessary remote round trips and preserves local-first UX.

However:

- local composition does not turn stale local state into authoritative payment/stock/credit/permission/hard-limit truth;
- the UI must preserve local/pending/authoritative distinctions where material;
- missing/stale data can trigger explicit synchronization, online query, resnapshot, or unavailable/degraded UX according to the capability contract;
- do not perform hidden remote fan-out from every local view simply because one field is absent;
- remote fallback must obey current authentication, authorization, version compatibility and resource limits.

## 15. Composition ownership

Do not let one domain capability become a general dependency hub for unrelated presentation composition.

Avoid this as a default:

```text
Orders
  -> Customers
  -> Inventory
  -> Pricing
  -> Recommendations
  -> every other module
```

Instead:

```text
client/use-case composition
  ├── Orders-owned query
  ├── Customers-owned query
  ├── Inventory-owned query
  └── Pricing-owned query
```

The owning composition use case may live in the host/application area that owns the screen/journey, or in a deliberately named cross-capability read/application component when the use case is stable enough to deserve one.

This is not a universal `CompositionService` requirement.

Capability modules still own:

- business invariants;
- state transitions;
- authoritative mutations;
- capability-owned permission/feature/settings semantics;
- capability public query/command/event contracts.

Composition owns the client/use-case projection and dependency/failure semantics, not the underlying business meaning.

## 16. Mutations are not read composition

A command that changes multiple authoritative facts is not made safe merely by calling several APIs and combining their responses.

Protected multi-capability mutations must use the real consistency mechanism appropriate to the invariant, such as:

- one application transaction when state is co-owned in one authoritative store and ownership permits it;
- explicit capability/application coordination;
- outbox + durable Worker consequence;
- workflow/orchestration with idempotency and reconciliation;
- external-provider `OutcomeUnknown` handling.

The API gateway/BFF/GraphQL resolver/client must not become a distributed transaction coordinator by accident.

## 17. Caching and freshness

Composition can mix sources with different freshness, but the result must not hide that difference where correctness matters.

Every cached/derived contribution identifies as applicable:

- authoritative source;
- freshness/version evidence;
- cache scope/key dimensions;
- invalidation/TTL;
- stale/unavailable behavior;
- tenant/permission scope;
- rebuild/reconciliation path.

A stale optional recommendation/summary may be acceptable. Stale current authorization, payment, stock, credit or hard-limit authority is not.

## 18. Observability and resource bounds

A composed request can amplify load even when the client sent only one request.

Measure/bound as applicable:

- number of DB queries;
- remote/provider fan-out count;
- request/dependency latency and tail latency;
- cancellation/timeouts;
- retry amplification;
- query/payload size;
- partial/degraded-result rate;
- cache hit/miss and stale-result behavior;
- connection/concurrency usage;
- GraphQL resolver/batch counts if GraphQL exists.

Trace composition across meaningful boundaries with existing `TraceId`, `CorrelationId`, and causation semantics. Do not put tenant/entity IDs into unbounded metric dimensions merely to diagnose composition.

## 19. Implementation gate

Before introducing or changing a composition boundary, answer:

1. What exact client/use-case result is being composed?
2. Which data/capabilities are already in the same process?
3. Why is in-process composition insufficient, if a new network boundary is proposed?
4. Is this aggregation or orchestration?
5. Which dependencies are required, degradable, or optional?
6. What freshness/authority does each contribution have?
7. What is the total deadline and per-dependency budget?
8. What owns retries and how is amplification bounded?
9. What happens on one dependency failure or stale cache?
10. How are TenantContext/resource/field authorization preserved?
11. How many DB queries/remote calls can one client request create?
12. What version/compatibility contract applies at each real boundary?
13. Does normalization happen before current composition logic?
14. Could Workstation satisfy this locally without a remote call?
15. Why is BFF/GraphQL/edge composition necessary, if proposed?
16. Does any proposed composition mutate authoritative state as a hidden side effect?
17. What observability proves the layer helps rather than adding tail-latency/failure coupling?
18. What would allow removal/simplification later?

## 20. Explicitly not baseline

This decision does not introduce:

- a universal Composition Engine service;
- a universal Protocol Resolver service;
- HTTP/gRPC between ordinary modules;
- service-per-capability topology;
- BFF-per-client projects by default;
- GraphQL/Federation without a measured read-composition problem;
- edge business authority;
- Kafka/message broker merely for event propagation vocabulary;
- gRPC as the automatic server-to-server protocol;
- a generic dynamic adapter around every local/in-process call;
- orchestration hidden inside read aggregation;
- a composition layer that bypasses capability ownership or authorization.

## 21. Architectural invariant

> SquiFlow composes business data at the narrowest correct boundary. Same-process capabilities are composed in-process; Workstation data is composed locally when validly available; remote aggregation is bounded and explicitly handles partial failure; orchestration remains a separate stateful concern; gateway/BFF/GraphQL/edge layers are requirement-driven adapters; protocol choice applies only to real boundaries; and contract/schema normalization occurs at those boundaries before current application composition.