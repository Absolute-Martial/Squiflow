# URL 033 — A Detailed Guide to API Composition Techniques

## Review method

This occurrence is reviewed independently from earlier archive/URL material. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: no comparison, pattern catalog, popularity claim, or source diagram selects architecture by itself.

## A. Identification

- **URL occurrence:** `033`
- **PDF page:** `277`
- **Source URL:** `https://blog.bytebytego.com/p/a-detailed-guide-to-api-composition`
- **Source access:** paid article with public preview; no paywall bypass.
- **Related supplied visual:** archive page `30`, API learning roadmap.
- **Visual inspected:** PDF page `277` at full size.

## B. Core concept

### SOURCE

The preview defines API composition as combining data from several services into one client-facing result. It says composition can happen in the client, server/aggregator, API gateway, backend-for-frontend, GraphQL, edge, or a service. Server-side composition can trade several expensive client round trips for one expensive client trip plus cheaper internal calls. Placement also changes partial-failure behavior, caching, versioning and team ownership. The outline distinguishes composition, aggregation and orchestration.

### INFERENCE

Composition is a placement/ownership decision, not a protocol winner. If the underlying data is already in one modular-monolith process/database boundary, forcing network service calls just to resemble the source architecture would manufacture the problem the pattern is meant to solve.

### EXTERNAL KNOWLEDGE / CAVEAT

A composition layer can become a distributed-monolith hotspot if it fans out synchronously to many fragile services. GraphQL can act as a composition interface but does not eliminate N+1, authorization, query-cost, partial-failure or data-ownership problems. A gateway can expose composition features, but edge routing/security and business read composition should not be conflated automatically.

## C. Important concepts

- client-side composition;
- backend aggregation;
- BFF per client family;
- GraphQL query composition;
- edge composition;
- partial failure and timeout budget;
- cacheability/freshness;
- over-fetching/under-fetching;
- ownership/versioning;
- composition versus orchestration;
- fan-out amplification and tail latency.

## D. Diagram / visual explanation

The related visual is an API learning roadmap listing REST, GraphQL, gRPC, WebSocket, gateways, caching, pagination and integration patterns. It is not evidence that SquiFlow needs each layer. For this article it mainly reinforces that API style, gateway placement and integration pattern are separate decisions.

## E. How it works — step by step

For an implemented SquiFlow screen requiring several data areas:

1. identify the actual UI projection and freshness/authorization requirements;
2. ask whether the data lives in one modular-monolith process or genuinely separate services;
3. if same-process, prefer an application/query composition that calls modules/data access in-process rather than network fan-out;
4. expose a bounded task/query HTTP shape where stable and simple;
5. if the client genuinely needs flexible nested/client-selected projections, evaluate GraphQL on that read surface;
6. if several materially different frontends need different composition/ownership, evaluate a BFF only when duplication/coupling is real;
7. define timeout/partial-result/freshness/cache/authorization behavior;
8. measure round trips, query count, DB plan, payload and tail latency before choosing a new layer.

## F. Why it matters

Tenant Web and Platform Admin will likely have overview/dashboard reads crossing several domain areas. The project needs a conscious composition strategy so UI convenience does not create accidental N+1 queries, cross-tenant field leakage, long synchronous service chains, or a gateway that becomes business authority.

## G. Trade-offs / limitations

Client composition keeps server layers simple but can multiply WAN round trips and duplicate logic. Server aggregation reduces client chatter but centralizes partial-failure and version coupling. BFFs can fit different client needs but add deployments/ownership. GraphQL gives flexible reads but adds query-cost/schema/field-authorization complexity. Edge composition can reduce latency but complicates security/data residency/cache control.

## H. Alternatives / comparisons — fit, not winner/loser

```text
same-process query composition
    -> strongest current fit for modular-monolith data spanning modules

task-oriented HTTP composite endpoint
    -> stable bounded screen/use-case projection

GraphQL
    -> positive candidate for genuinely flexible nested/client-selected Web/Admin reads

BFF
    -> candidate when different client families have materially different composition/lifecycle needs

client-side composition
    -> reasonable for a few independent low-latency calls

edge composition
    -> candidate only when edge latency/cache/security model proves useful

orchestration
    -> coordinates multi-step work; not merely merging a read response
```

## I. Real implementation considerations

Define authorization per field/resource, tenant scope, freshness, timeout budget, partial failure contract, cancellation, fan-out/concurrency bounds, DB query count, caching, version compatibility and observability. Composition should not mutate authoritative state as a side effect of a read.

### Implications for the Current Implementation

- **KEEP:** inside Core/Admin modular-monolith hosts, ordinary cross-module read composition stays in-process when the data/logic is already local.
- **KEEP:** task-oriented HTTP is useful for stable bounded composite reads and explicit commands/resources.
- **NEEDS MEASUREMENT:** GraphQL remains a positive candidate for tenant dashboard, Platform Admin dashboard, and configurable read/report screens if real UI data proves repeated over/under-fetching or composition pain.
- **LATER / SCALE TRIGGER:** a BFF is justified only when actual client families diverge enough that one composition surface becomes a release/ownership bottleneck.
- **LATER / SCALE TRIGGER:** edge composition requires a concrete latency/cache/security case; it is not implied by custom domains/CDN use.
- **AVOID:** adding network service calls between modules simply so an aggregator/gateway can compose them.
- **AVOID:** treating the edge API gateway as the only place business/resource authorization occurs.

**What are we actually doing and why?** We currently compose business reads in the same application boundary because the modular monolith already owns the relevant modules/data without remote-call failure. We would introduce GraphQL/BFF/edge composition only when a specific client/read workload earns the extra layer.

**What would falsify/change this?** Real screens showing high client round-trip count, rapidly varying projections, repeated endpoint proliferation, or independent client-team ownership can justify a different composition layer, provided its query/security/failure cost is measured.

### Critical interrogation — answers intentionally withheld

**Foundation**
1. What is API composition?
2. How is composition different from orchestration?
3. Why can server-side composition reduce total WAN latency despite adding one server layer?

**Critical reasoning**
1. Which SquiFlow data is genuinely split across processes today versus only modules?
2. Why would introducing service calls before composition be backwards?
3. Which tenant/Admin screens are plausible GraphQL candidates and why?
4. What authorization work remains if GraphQL performs composition?
5. When does a composite endpoint become a brittle screen-specific API?

**Trade-off**
1. When is BFF better than one shared API surface?
2. When is client composition simpler than server aggregation?
3. When would edge composition be too risky for tenant-sensitive data?

**Failure / edge**
1. One of four composed sources times out. Is partial data allowed?
2. A slow source consumes the whole request deadline. How are budgets partitioned?
3. One field is forbidden while the rest of the object is allowed. What does the response do?
4. A composed cache is fresher for one module than another. How is freshness expressed?

**Implementation**
1. How many DB queries and internal calls may a dashboard request produce?
2. How are composition traces correlated without high-cardinality metric explosion?
3. How are old clients supported when the composition shape evolves?
4. Which layer owns the DTO/schema?

**System design interview**
1. Design a tenant dashboard spanning customer, quote, order, payment and inventory summaries.
2. Explain when task HTTP, GraphQL and BFF can coexist in one product.

**Challenge**
A mobile client is later added with a very different dashboard from Blazor Web. Decide whether to add endpoints, GraphQL, a BFF, or client composition and identify the evidence required before changing the current architecture.
