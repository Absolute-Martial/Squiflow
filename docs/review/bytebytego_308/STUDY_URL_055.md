# URL 055 — API Protocols 101: A Guide to Choose the Right One

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `055`
- **PDF page:** `299`
- **Source URL:** `https://blog.bytebytego.com/p/api-protocols-101-a-guide-to-choose`
- **Source access:** paid article with a public preview; no subscription controls bypassed.
- **Related supplied visual:** archive page 118, network protocol dependencies visual.
- **Visual inspected:** PDF page `299` at full size.

## B. Core concept

### SOURCE

The preview defines an API protocol as rules/standards for network communication and compares REST, GraphQL, gRPC, WebSockets, SSE, webhooks, and SOAP across performance, security, implementation complexity, scalability, and directionality. It says REST is familiar/simple, GraphQL gives clients more control over fetched data, gRPC can provide efficient low-latency service communication with more setup, WebSockets support bidirectional real-time interaction, SSE can be simpler for one-way updates, and webhooks need their own authentication/signature validation.

### INFERENCE

The source itself is strongest when read as a fit matrix. SquiFlow should first identify the boundary and interaction semantics, then choose the protocol whose properties solve that specific problem. The same product can use several protocols without inconsistency.

### EXTERNAL KNOWLEDGE / CAVEAT

Statements such as ‘gRPC is faster than REST’ or ‘gRPC is more efficient for microservices’ are workload-dependent generalizations. Serialization, payload shape, server/database time, HTTP version, proxy support, connection reuse, streaming, client ecosystem, observability and deployment topology all matter. REST and GraphQL are API/interface styles that usually ride over HTTP, while WebSocket/SSE/webhooks differ in direction and delivery model; they are not interchangeable slots in one strict taxonomy.

## C. Important concepts

- boundary-first protocol selection;
- request/response versus streaming versus push/callback;
- client-driven read composition;
- binary/generated contracts;
- browser/external interoperability;
- durability versus live signaling;
- authentication/authorization independent of transport;
- deadlines/retries/idempotency;
- edge/proxy/network compatibility;
- measurement before performance claims;

## D. Diagram / visual explanation

The related network-dependency visual shows lower-layer protocols such as IP, TCP, UDP, TLS, HTTP and QUIC beneath higher-level protocols. This reinforces that application protocol choice and transport/network choice are layered. A gRPC, REST, GraphQL, SSE or WebSocket decision still relies on lower-level networking and still does not decide SquiFlow tenant/business authority.

## E. How it works — step by step

1. Ask whether the interaction is even a real network/process boundary; otherwise use an in-process call.
2. Ask whether the caller needs an immediate authoritative response.
3. If yes, compare task HTTP/REST-style, GraphQL, and gRPC from the actual request/read/RPC shape.
4. If the need is live UI notification, compare polling, SSE, or WebSocket/SignalR without making them durable truth.
5. If the need is after-commit/long-running work, use durable Worker/outbox regardless of live protocol.
6. For outbound callbacks, define webhook authentication/signatures, idempotency, retry and delivery status.
7. Benchmark representative payloads through the real edge/network topology before selecting a protocol for performance.
8. Preserve authentication, TenantContext, OpenFGA, concurrency, idempotency and compatibility independently of transport.

## F. Why it matters

SquiFlow has genuinely different communication surfaces: browser business commands/resources, complex read composition, Workstation sync, possible future synchronous service RPC, live UI updates, and durable background consequences. A single universal protocol would force the wrong properties onto several of those boundaries.

## G. Trade-offs / limitations

Using several protocols increases tooling, contracts, observability and operational surface. Forcing one protocol reduces variety but can create poor UX, inefficient streaming, awkward commands, or fragile async behavior. The correct balance is the smallest set of protocols that each earn a concrete boundary.

## H. Alternatives / comparisons — fit, not winner/loser

```text
in-process
    -> ordinary modular-monolith modules

task-oriented HTTP / REST-style
    -> explicit commands/resources and broad interoperability

GraphQL
    -> flexible nested/client-selected read composition when measured useful

gRPC
    -> real typed/high-frequency/streaming RPC boundary when its properties matter

SSE
    -> simple one-way server-to-browser updates

WebSocket / SignalR
    -> bidirectional/live interaction or wake-up

webhook
    -> outbound callback to another system

Worker + outbox
    -> durable long-running / after-commit work
```

These can coexist; none replaces application authority.

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** in-process calls inside the modular monolith.
- **KEEP:** task-oriented HTTP as the ordinary Web/external/business API fit because explicit commands/resources, HTTP semantics, interoperability and endpoint inventory suit those surfaces.
- **LATER / SCALE TRIGGER:** GraphQL for concrete Web/Admin nested/client-selected read-composition pain with query-cost/auth/N+1/schema controls.
- **NEEDS MEASUREMENT:** gRPC for Workstation sync or future real service RPC; benchmark streaming, payload/CPU/bandwidth, generated-contract and edge support against simpler HTTP.
- **LATER / SCALE TRIGGER:** SSE or SignalR/WebSocket for a concrete live-UX requirement; durable state remains authority.
- **KEEP:** Worker/outbox for durable long-running/after-commit work regardless of synchronous/live protocol choices.
- **AVOID:** using a protocol comparison as a global winner/loser architecture decision.

**What are we actually doing and why?** SquiFlow intentionally uses different mechanisms at different boundaries: HTTP where explicit business commands/resources fit, in-process calls where there is no network boundary, durable async where work must survive process/dependency failure, and gRPC/GraphQL/live protocols only when their specific properties solve a measured surface.

**What would falsify/change this?** If a browser read screen repeatedly needs nested projections and task HTTP causes costly client composition, GraphQL may be the better read surface. If Workstation sync is not chatty/streaming enough to justify gRPC complexity, simple HTTP can remain the better fit. The current choice changes from evidence, not labels.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What communication properties distinguish REST/task HTTP, GraphQL, gRPC, SSE, WebSocket, and webhooks?
2. Why is a live WebSocket message different from durable asynchronous work?
3. Why does protocol choice not decide authorization?

**Critical reasoning**

1. Which SquiFlow surfaces currently have a real network boundary and which are in-process?
2. What exact property makes task HTTP fit ordinary commands/resources?
3. What exact Workstation-sync evidence would make gRPC better than HTTP?
4. Which Web/Admin read shape would justify GraphQL?
5. Why should a notification channel not become the sole proof that a business event happened?

**Trade-off**

1. What complexity is introduced by supporting multiple protocols?
2. When is SSE simpler than WebSocket/SignalR?
3. When can generated gRPC contracts be worth the browser/external interoperability cost?

**Failure / edge**

1. gRPC stream drops after the server committed some items. Which correctness layer recovers?
2. WebSocket message is lost. How does the UI recover authoritative state?
3. Webhook receiver times out after accepting the callback. How is retry made safe?
4. GraphQL query is valid but extremely expensive. Which controls bound it?

**Implementation**

1. What representative benchmark is required before selecting gRPC for sync?
2. How are deadlines, retries and idempotency surfaced consistently across HTTP/gRPC?
3. What edge/proxy behavior must be proven for streaming?
4. How are protocol versions kept compatible with skipped Workstation releases?

**System design interview**

1. Choose communication mechanisms for Workstation sync, dashboard reads, live job progress, and after-commit email—and justify each boundary.
2. Explain how REST, GraphQL, gRPC, WebSocket/SSE and durable async can coexist in one coherent architecture.

**Challenge**

1. A benchmark says gRPC serialization is 5x faster, but 95% of request time is PostgreSQL lock wait and the customer proxy intermittently breaks long-lived HTTP/2 streams. Should SquiFlow switch? Explain the evidence hierarchy.
