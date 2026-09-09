# URL 039 — Top 9 Architectural Patterns for Data and Communication Flow

## Review method

This occurrence is reviewed independently from earlier archive/URL material. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: no comparison, pattern catalog, popularity claim, or source diagram selects architecture by itself.

## A. Identification

- **URL occurrence:** `039`
- **PDF page:** `283`
- **Source URL:** `https://blog.bytebytego.com/p/ep107-top-9-architectural-patterns`
- **Source access:** public newsletter section accessible.
- **Related supplied visual:** archive page `305`, design-pattern cheat sheet; the visual is related but not the same nine architecture-flow patterns.
- **Visual inspected:** PDF page `283` at full size.

## B. Core concept

### SOURCE

The public section lists nine patterns: Peer-to-Peer, API Gateway, Pub-Sub, Request-Response, Event Sourcing, ETL, Batching, Stream Processing, and Orchestration. It gives one-sentence descriptions of each.

### INFERENCE

These patterns solve different communication/data-flow problems and are often composable. The correct review is a map from each pattern to a concrete SquiFlow surface or missing requirement, not a ranking or a backlog of nine things to implement.

### EXTERNAL KNOWLEDGE / CAVEAT

The patterns operate at different levels. Event Sourcing changes the authoritative state model; batching is an execution technique; API Gateway is an edge pattern; orchestration is workflow coordination. Treating them as interchangeable architecture choices would be category error.

## C. Important concepts

- direct peer communication;
- north-south gateway entry;
- pub/sub fan-out;
- synchronous request-response;
- event-log authority;
- ETL/analytics integration;
- bounded batching;
- continuous stream processing;
- centralized workflow orchestration;
- authority, durability and failure semantics per pattern.

## D. Diagram / visual explanation

The related supplied visual shows object-oriented design patterns such as Factory, Builder, Singleton, Chain of Responsibility and Iterator. It is not evidence for the nine data/communication-flow patterns. The PDF correctly labels it only as a related ByteByteGo visual. Therefore SquiFlow conclusions come from the accessible newsletter text, not from reading the unrelated image as architecture prescription.

## E. How it works — SquiFlow fit map

```text
Peer-to-Peer
    -> direct independently deployed component communication if such topology exists
    -> ordinary same-host modules do not need network P2P

API Gateway
    -> public edge TLS/routing/request-limit/exposure capability
    -> not business authorization authority

Pub-Sub
    -> multiple genuinely independent consumers of a committed fact
    -> outbox can support initial fan-out without generic broker platform

Request-Response
    -> immediate authoritative answer
    -> in-process for modules; HTTP/gRPC for real process boundaries

Event Sourcing
    -> event sequence itself is source of truth
    -> only for a domain whose replay/history authority justifies it

ETL
    -> migration/analytics/warehouse-style movement/transformation when needed
    -> not online transaction authority

Batching
    -> sync batches, imports, rebuilds, bounded Worker processing where grouping helps

Stream Processing
    -> continuous low-latency/replay/window workload when proven

Orchestration
    -> explicit coordinator/state machine for multi-step workflow where one owner is desirable
    -> can be in-process; does not imply microservices
```

## F. Why it matters

This source is a good test of the user's anti-bias rule: nearly every pattern is valuable somewhere, and several already appear in SquiFlow for different reasons. The architecture remains coherent precisely because we do not force one pattern everywhere.

## G. Trade-offs / limitations

Peer-to-peer can reduce central bottlenecks but increases discovery/security/versioning. Gateways centralize edge concerns but can become bottlenecks/authority mistakes. Pub-sub decouples producers but adds delivery/order/replay semantics. Request-response is simple but can create synchronous failure chains. Event Sourcing adds replay history but major model complexity. ETL introduces lag/duplicate data. Batching increases throughput but latency. Streaming reduces latency but adds continuous operational state. Orchestration clarifies ownership but can centralize workflow coupling.

## H. Alternatives / comparisons — fit, not winner/loser

There is no winner among the nine. SquiFlow can use request-response for interactive commands, batching for sync/import, orchestration for explicit workflows, a gateway at the edge, and later pub-sub/streaming/ETL/Event Sourcing only where their specific properties are required.

## I. Real implementation considerations

Every pattern must declare authority, owner, transaction/durability boundary, timeout/retry/idempotency, ordering, compatibility, observability, recovery and small-team operational cost.

### Implications for the Current Implementation

- **KEEP:** in-process/request-response for immediate modular-monolith work; HTTP/gRPC only across real process boundaries.
- **KEEP:** edge gateway/reverse-proxy capabilities for TLS/routing/exposure/limits, while backend authorization remains authoritative.
- **KEEP:** bounded batching for Workstation sync and suitable imports/jobs, with count + byte limits.
- **KEEP:** explicit workflow/orchestration where one business owner/state machine is clearer than hidden event choreography.
- **LATER / SCALE TRIGGER:** pub/sub when several independent consumers genuinely need the same committed fact.
- **LATER / SCALE TRIGGER:** ETL for real analytics/warehouse/migration requirements; streaming for continuous low-latency/replay/window workloads.
- **LATER / SCALE TRIGGER:** Event Sourcing for a domain where event history must itself become authority.
- **AVOID:** turning the nine-pattern list into an implementation backlog.
- **AVOID:** creating network peer-to-peer calls between modules that can remain in-process.

**What are we actually doing and why?** SquiFlow already mixes patterns deliberately: request-response for immediate answers, batching for sync, orchestration for owned workflows, durable outbox/Worker for after-commit work, and edge gateway capability. Each exists because of its local problem, not because it beat the others.

**What would falsify/change this?** New topology/workload evidence can add a pattern at a specific boundary: multiple consumers can earn pub/sub; replayable continuous data can earn streaming; independent historical authority can earn Event Sourcing.

### Critical interrogation — answers intentionally withheld

**Foundation**
1. Name the nine source patterns and their problem class.
2. Which patterns are communication patterns versus data-authority/processing patterns?
3. Why can orchestration be in-process?

**Critical reasoning**
1. Which of the nine does SquiFlow already use or plan, and exactly where?
2. Why does a gateway not imply microservices?
3. Why is Event Sourcing a much deeper decision than batching?
4. When does pub/sub improve a fact distribution problem rather than hide ownership?
5. Which SquiFlow workflows should have explicit orchestration instead of event choreography?

**Trade-off**
1. Request-response versus durable async for which SquiFlow operations?
2. Batching versus streaming for reconnect/sync or analytics?
3. Orchestration versus choreography for money/stock/permissions?
4. ETL versus live read composition for reporting?

**Failure / edge**
1. Orchestrator crashes mid-workflow.
2. Pub/sub consumer receives duplicate/out-of-order event.
3. Batch contains one poison item.
4. Gateway is unavailable while backend is healthy.

**Implementation**
1. What is the authoritative state for each chosen pattern?
2. What identifiers carry idempotency/order/replay evidence?
3. What retry owner exists at each boundary?
4. What operational dashboards prove pattern health?

**System design interview**
1. Map the nine patterns to a retail/quotation system without using all nine by default.
2. Explain how SquiFlow can combine orchestration, request-response, batching and pub/sub coherently.

**Challenge**
A team proposes Kafka + Event Sourcing + stream processing + pub/sub because all four appear in the article. Identify four separate requirements that would need to be proven before those choices are justified for SquiFlow.
