# ByteByteGo Exhaustive Sequential Study — Archive Entry 123

# 123 — A picture is worth a thousand words: 9 best practices for developing microservices

## A. Identification

- **Archive entry:** `123`
- **PDF page:** `241`
- **Original archive page:** `441`
- **Multi-page:** no
- **Visual inspected:** PDF page `241` in full.
- **Archive significance:** final archive occurrence before the URL section.

## B. Core concept

### SOURCE

The visual lists nine microservice practices:

1. separate data store for each microservice;
2. keep code at a similar level of maturity;
3. separate build for each microservice;
4. single responsibility;
5. deploy into containers;
6. treat servers as stateless;
7. domain-driven design;
8. micro frontend;
9. orchestrating microservices.

The visible prose on the page explicitly begins enumerating items `1-5`; items `6-9` are present in the infographic and are therefore part of the supplied source visual.

### INFERENCE

The source is trying to describe properties that make **genuinely independent services** independently owned, built, deployed, scaled and operated.

### EXTERNAL KNOWLEDGE / CAVEAT

The infographic overstates several practices if read as universal rules:

- “separate data store” should primarily mean **exclusive authoritative ownership** for a truly independent service; it does not always require a separate physical database server/product/cluster.
- a microservice can be built from a monorepo while still having an independently releasable artifact; repository layout and deployment independence are different concerns.
- “single responsibility” should usually mean a cohesive bounded business capability, not one tiny function/entity per service.
- containers are an optional packaging/isolation mechanism; microservices can run on VMs, processes, serverless platforms or other environments.
- “stateless server” should mean process memory is not the sole durable authority; a service still owns durable state.
- DDD can help discover boundaries, but one bounded context does not mechanically equal one microservice.
- micro frontends are useful only when frontend ownership/deployment boundaries justify their runtime/UX/consistency cost.
- Kubernetes/orchestration is not inherent to microservices and does not create service boundaries by itself.

## C. Important concepts

### Service boundary

- independent business capability;
- independent deployment/failure/scaling reason;
- clear API/event contract;
- team/code ownership;
- explicit data authority;
- no direct private-table modification by another true service.

### Data ownership

- service-owned authoritative writes;
- cross-service reads through API/events/projections when extracted;
- distributed transaction implications;
- consistency and stale-read contracts;
- migration/exit path.

### Build/deployment ownership

- independent artifact/release cadence;
- backward/forward compatibility;
- rollout/rollback;
- observability and SLO ownership;
- config/secrets;
- dependency versioning.

### Runtime/deployment

- process isolation;
- containers optional;
- stateless process semantics;
- service discovery/routing;
- orchestration only when topology requires it;
- resource limits;
- scaling independent bottlenecks.

### Domain/frontend boundaries

- DDD/bounded-context reasoning;
- micro frontend only for real independent UI ownership;
- UX consistency/shared design concerns;
- avoiding distributed monoliths.

## D. Diagram / visual explanation

Page `241` is a 3x3 matrix of “Microservice Best Practices.” It visually associates:

```text
service A -> DB A
service B -> DB B

separate CI/build pipelines

one service responsibility

containers

stateless request/response service

DDD layered/domain model

payments frontend + orders frontend
    -> API gateway
    -> payments/orders services

API/UI/CLI
    -> Kubernetes master
    -> nodes
```

The diagram is best read as a **possible mature microservice operating model**, not as a migration checklist for SquiFlow.

## E. How it works — step by step

If SquiFlow someday extracts a capability into a true independent service, the correct sequence should be requirement-led:

1. Identify the concrete reason the capability should no longer remain in-process: independent scaling, fault isolation, security boundary, deployment cadence, ownership, or specialized runtime.
2. Define the service's business responsibility and invariants.
3. Define authoritative data ownership. Other services must not casually write its private data.
4. Define synchronous/asynchronous contracts and compatibility/versioning.
5. Define what happens to cross-boundary transactions: idempotency, outbox/events, reconciliation, `OutcomeUnknown` where relevant.
6. Give it an independently deployable artifact if deployment independence is actually part of the reason for extraction.
7. Select packaging (process/VM/container) from deployment evidence.
8. Add service discovery/load balancing/orchestration only if the deployed topology requires them.
9. Define SLO/telemetry/backup/restore/on-call/recovery ownership.
10. Extract only after proving the new boundary reduces or contains a real problem more than it adds network/distributed-systems cost.

## F. Why it matters

This is highly relevant precisely because SquiFlow has already chosen a **modular monolith first**. The article gives a useful test of whether that choice is dogma or fit-for-purpose.

SquiFlow's current answer is positive and requirement-specific:

```text
small team
+ early product
+ business modules with strong transactional interactions
+ owned lower-spec rack
+ need for simple debugging/recovery

-> in-process modular monolith is currently the lower-risk boundary
```

That does not mean microservices are rejected. It means a service must earn a network/deployment boundary from a concrete problem.

## G. Trade-offs / limitations

### When true microservices can help

- independent scaling of a sharply different workload;
- fault containment;
- independent release cadence;
- stronger security/process isolation;
- specialized runtime/technology where justified;
- independent team ownership;
- regional/residency isolation.

### Costs introduced

- network timeout/partial failure;
- retry/idempotency complexity;
- distributed tracing/diagnostics;
- version compatibility;
- deployment orchestration;
- per-service secrets/config/health;
- cross-service authorization;
- data ownership and eventual-consistency consequences;
- backup/restore coordination;
- greater baseline CPU/RAM/process/network consumption;
- more complex local development and incident response.

### Distributed-monolith risk

Many small services with synchronous chains, shared tables, synchronized deployments and one team can create **more coupling**, not less.

## H. Alternatives / comparisons — fit, not winner/loser

```text
modular monolith
    -> strong fit when one team owns tightly related business capabilities
       and in-process transaction/debug simplicity matters

separate process in same product
    -> fit for a real supervision/security/failure boundary
       without pretending full service independence
    -> current examples: Guard; Admin API plane; future Worker

independent microservice
    -> fit when scaling/failure/security/team/deployment/data-ownership reason
       materially exceeds distributed-systems cost

serverless / managed job / external provider
    -> can fit narrow specialized capabilities without building a permanent service
```

These are not maturity stages where microservices are automatically “later and better.”

## I. Real implementation considerations

Before extracting any SquiFlow module/service, require answers to:

- What exact current pain/failure/bottleneck does extraction solve?
- Is it measured or hypothetical?
- What capability/invariant moves?
- Who owns its authoritative data?
- What happens to transactions that currently span modules?
- What API/event contract replaces in-process calls?
- Does it need synchronous response or durable async?
- What retry/idempotency/reconciliation semantics appear?
- How is authorization/tenant context propagated and revalidated?
- How is version skew supported?
- How is it deployed, discovered, observed, backed up and restored?
- How does the small team operate/on-call it?
- What resource cost does it add to the actual rack?
- What is the rollback/reintegration strategy if the boundary is wrong?

### Implications for the Current Implementation

- **KEEP:** modular-monolith business core because current SquiFlow requirements favor in-process transactions, simpler debugging, lower operational load and one-team ownership.
- **KEEP:** Core API, Admin API, Worker and Guard process boundaries only for their concrete runtime/security/supervision responsibilities; process separation does not automatically mean microservices.
- **KEEP:** current hosts may share the central DB while preserving explicit module/data ownership because they are hosts of the same modular-monolith core, not independent services.
- **LATER / SCALE TRIGGER:** if a capability becomes truly independent, give it explicit authoritative data ownership and prevent other services from directly modifying its private tables.
- **LATER / SCALE TRIGGER:** independent build/deploy, containerization, discovery, load balancing, orchestration and micro frontend only when the extracted boundary/topology specifically needs them.
- **AVOID:** microservice-per-module/entity, database-per-module physical deployment, HTTP/gRPC between ordinary modules, Kubernetes/micro-frontends as architecture fashion.
- **NEEDS MEASUREMENT:** likely extraction candidates such as heavy document/image processing should first prove a distinct CPU/memory/scaling/failure profile that cannot be owned economically by the Worker/current host.

**What are we actually doing and why?** SquiFlow is deliberately using a modular monolith because the current team, transactional domain, hardware and pre-Phase-0 state make network boundaries costlier than the problems they would solve. We already use separate processes where an external failure/security/lifecycle boundary is real. That is a positive design rationale, not an anti-microservice ideology.

**What would justify changing it?** Sustained evidence of independent scale, release cadence, fault/security isolation, technology/runtime need, customer residency, or team ownership where extraction materially reduces risk/cost.

**What would falsify an extraction?** The service still requires synchronized deployment, shared-table writes, long synchronous chains, distributed transactions for ordinary operations, or more operational cost than the isolated problem warrants.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. What are the nine practices shown in the microservice visual?
2. What is the difference between exclusive data ownership and a physically separate database server?
3. What does stateless service process mean in a system that still owns persistent data?

**Critical reasoning questions**
1. Why is SquiFlow currently a modular monolith rather than microservices?
2. Which current SquiFlow process boundaries are real, and why do they not automatically imply microservice architecture?
3. If a module needs independent scaling, what other evidence is required before extraction?
4. Why can “database per service” be harmful if applied mechanically to the current modular monolith?
5. How can a system with many services still be a distributed monolith?

**Trade-off questions**
1. When does independent deployment become worth API/version/data-consistency cost?
2. When can a separate Worker process solve isolation without a full microservice extraction?
3. When is a micro frontend justified, and what UX/platform cost does it add?
4. When does container orchestration become a separate infrastructure decision rather than a microservice rule?

**Failure / edge-case questions**
1. An extracted Payment service commits but Core API times out. How is duplicate/ambiguous outcome handled?
2. Two services share the same DB tables and release together. What independence was actually gained?
3. A service is stateless in memory but its only database is unavailable. What availability claim remains?
4. Kubernetes restarts a failed pod, but the downstream DB is saturated. Why might orchestration worsen the incident?

**Implementation questions**
1. What extraction gate should appear in architecture review before creating a new service repository/project?
2. How are tenant identity and OpenFGA/resource authorization propagated across a real service boundary?
3. What service-level backup/restore/reconciliation evidence is required after data ownership splits?
4. How is old/new service version coexistence proven?

**System design interview questions**
1. Choose whether to keep or extract SquiFlow document rendering when CPU demand grows 20x while order/payment traffic remains low.
2. Design an extraction from modular monolith to an independent service without shared-table writes or a synchronous call chain.

**Challenge**
Assume PDF/image processing starts consuming 80% CPU and causing Core API tail-latency spikes, but it still reads document/order data and writes generated-artifact metadata. Design three possible responses — stronger Worker isolation on the same host, a separate process/node, and a true service extraction — and state the evidence that would make each one the correct boundary.

---
