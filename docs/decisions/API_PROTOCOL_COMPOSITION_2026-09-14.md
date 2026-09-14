# API and Protocol Composition Decision — 2026-09-14

**Status:** Accepted refinement  
**Version:** v0.0.20  
**Base discussion:** API composition, protocol composition, schema resolution/version overlap  
**Current owner:** `docs/architecture/API_AND_PROTOCOL_COMPOSITION.md`

## 1. Audit result

The proposed composition model contains several useful concerns, but parts of its generic service-oriented diagram conflict with SquiFlow's already accepted modular-monolith/runtime decisions.

Already accepted before this refinement:

- ordinary cross-module reads inside Core/Admin modular-monolith hosts should be composed in-process;
- task-oriented HTTP is the ordinary stable bounded client/API baseline;
- GraphQL is a positive measured candidate only for genuinely flexible/nested Web/Admin reads;
- BFF and edge composition are later/requirement-driven options;
- a gateway does not become business authorization/domain authority;
- ordinary modules do not call each other over HTTP/gRPC merely to imitate microservices;
- gRPC is a candidate for real process boundaries such as Workstation sync, not a universal default;
- final Workstation sync transport remains open for Phase 3;
- broker/event-log infrastructure is not baseline;
- Workstation has a local SQLite/WAL model and can satisfy many views locally without remote fan-out;
- schema/API/sync/durable-work versions are separate compatibility domains.

This refinement preserves those decisions while making composition ownership/failure/version behavior explicit.

## 2. Accepted refinements

### 2.1 API composition and protocol selection are separate axes

API composition decides which capability results form a client/use-case projection.

Protocol selection decides how an interaction crosses a **real** process/provider/network boundary.

One must not force the other.

Examples:

```text
same-process composition
-> direct capability application/query calls

Workstation sync
-> HTTP or gRPC according to Phase-3 proof

external provider
-> provider protocol through owning integration adapter
```

There is no requirement to wrap local module calls in REST/gRPC/local protocol adapters just to make composition look uniform.

### 2.2 Aggregation and orchestration are explicitly different

Aggregation combines independent results and may use bounded parallel execution for genuine independent calls.

Orchestration coordinates dependent/stateful work and can require workflow state, durable execution, retries, idempotency, compensation/reconciliation and timeout semantics.

A read aggregator must not silently become a workflow engine.

### 2.3 Same-process application/query composition remains the current default

When Orders, Customers, Inventory, Pricing or another capability already execute in one SquiFlow host, compose them through application/query surfaces in-process.

Do not create service calls simply so a gateway/aggregator can call them.

### 2.4 Remote/derived dependencies gain explicit criticality

When a composed result genuinely depends on independently failing remote/derived sources, classify each contribution as conceptually:

```text
Required
Degradable
Optional
```

For each applicable dependency define timeout/deadline, retry ownership, freshness, fallback/cache behavior, partial-result semantics, authorization scope and resource/concurrency bounds.

This is policy vocabulary, not a mandate for one universal runtime class.

### 2.5 Availability amplification is a design concern

Making every downstream/derived source mandatory can reduce the useful availability of a composite result and increase tail latency.

Approximate multiplication may be used as intuition only when independence assumptions are reasonable. It is not a universal SquiFlow availability formula.

The practical rule is:

> Optional presentation information must not accidentally become a mandatory dependency for otherwise valid business work.

### 2.6 Gateway responsibilities stay transport/edge oriented

The gateway may terminate TLS, route, apply request/WAF/access policy, coarse rate/admission limits, correlation metadata, observability and selected authentication validation/handoff.

It does not replace current backend authorization, TenantContext isolation, domain invariants, field/resource permission, idempotency/concurrency or authoritative limit decisions.

### 2.7 BFF remains requirement-driven

A BFF is allowed only when materially different client families create real payload, latency, release cadence, composition, ownership or failure-policy divergence.

Do not create one BFF project/service per possible client by default.

### 2.8 GraphQL remains a measured read candidate

If a real flexible nested read problem earns GraphQL, its implementation must own bounded pagination, query depth/cost, tenant/resource/field authorization, schema/deprecation policy, cache/freshness and N+1 prevention/batching/DataLoader-like behavior where required.

This refinement does not select GraphQL/Federation as baseline.

### 2.9 Edge composition remains restricted

Edge composition/caching may be useful for explicitly safe read-oriented projections when latency/cache/security/residency evidence supports it.

Transactional domain decisions, current sensitive authorization, payment/stock/credit/hard-limit authority and secret-bearing data do not move to the edge for convenience.

### 2.10 Workstation local-first composition is explicit

When data required by the Workstation UI is validly available in local capability/read-model state, compose it locally rather than fan out to server APIs.

Missing/stale data follows explicit sync/online-query/resnapshot/degraded behavior.

Local composition never makes stale Workstation state authoritative for protected server-owned invariants.

### 2.11 Contract normalization happens at real boundaries before current composition

Where old/new API/sync/durable contracts coexist:

```text
wire/durable version
-> boundary-specific resolver/normalizer
-> current application contract
-> composition/current use-case logic
```

Current composition logic should not carry historical protocol/schema branching deep into capability code when the boundary can normalize safely.

Protocol choice and schema version remain independent decisions.

### 2.12 Composition ownership stays use-case oriented

Do not make Orders or another capability a generic dependency hub merely because a screen needs cross-capability data.

Prefer a client/use-case composite query/application component that calls capability-owned query surfaces while the capabilities retain business ownership.

A universal `CompositionService`/`CompositionEngine` is not required.

### 2.13 Multi-capability mutation is not read composition

Calling several APIs and merging responses does not create a safe cross-capability transaction.

Protected mutations use the real consistency mechanism: one authoritative transaction where appropriate, application coordination, outbox/Worker consequence, workflow/orchestration, or external-effect reconciliation.

Gateway/BFF/GraphQL/client layers do not become distributed transaction coordinators by accident.

### 2.14 Composition has explicit observability/resource gates

Measure/bound query count, fan-out, tail latency, dependency deadlines, retry amplification, payload/query size, cache behavior, partial/degraded result rate and connection/concurrency cost.

Tracing uses the existing correlation model and avoids high-cardinality metric explosion.

## 3. Protocol-direction clarification

The generic proposal included protocol-to-workload mappings that would be too strong for current SquiFlow decisions.

Accepted current direction is:

```text
same-process modules
-> direct in-process calls

Web/external API
-> REST/task-oriented HTTP baseline

GraphQL
-> deferred measured read candidate

Workstation sync
-> HTTP simpler baseline; gRPC preferred candidate to compare

future real synchronous service boundary
-> workload-driven; gRPC preferred candidate, not automatic

durable async/background
-> DB/outbox/job/Worker baseline; broker only when earned

external integrations
-> provider-required protocol through adapter

Workstation local reads
-> in-process + SQLite/read model
```

Therefore `internal high-throughput = gRPC`, `event propagation = broker`, or `server-to-server = gRPC` are not accepted as universal rules.

## 4. What this decision does not introduce

- service-per-module/microservice topology;
- a universal Composition Engine runtime service;
- a universal Protocol Resolver network service;
- generic `CapabilityInvocation` abstraction around every local call;
- HTTP/gRPC between ordinary modules;
- GraphQL/Federation as baseline;
- BFF-per-client by default;
- edge business authority;
- Kafka/broker as baseline event propagation;
- gRPC as automatic server-to-server transport;
- orchestration hidden inside a read aggregator;
- a second business implementation for each protocol/client.

## 5. Relationship to schema/contract evolution

`docs/architecture/SCHEMA_AND_CONTRACT_EVOLUTION.md` remains the owner of version overlap, compatibility matrices, registry metadata and resolver/normalizer semantics.

`docs/architecture/API_AND_PROTOCOL_COMPOSITION.md` owns where/how current capability results are composed and how real protocol boundaries interact with those contracts.

The combined rule is:

> Normalize supported historical contracts at the owning boundary, then compose current application/capability contracts at the narrowest correct boundary.

## 6. Revisit triggers

Reconsider a BFF, GraphQL, edge composition, universal invocation abstraction or additional remote composition boundary only after evidence such as:

- repeated excessive WAN round trips;
- stable client families with materially divergent payload/release/ownership needs;
- repeated endpoint proliferation caused by genuinely dynamic read projections;
- measured tail latency/fan-out problems;
- independent service/process extraction that creates a real protocol-selection problem;
- a real plugin/provider topology where dynamic invocation materially reduces coupling.

## 7. Architectural invariant

> Composition is a placement/ownership problem, not a reason to manufacture services. SquiFlow composes same-process capability data in-process, composes Workstation data locally when valid, uses bounded remote aggregation only across real boundaries, keeps orchestration separate, chooses protocols from workload evidence, and normalizes versioned external/durable contracts before current composition logic.