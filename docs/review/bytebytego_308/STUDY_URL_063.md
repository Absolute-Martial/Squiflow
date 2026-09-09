# URL 063 — Synchronous vs Asynchronous Communication: When to Use What?

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `063`
- **PDF page:** `307`
- **Source URL:** `https://blog.bytebytego.com/p/synchronous-vs-asynchronous-communication`
- **Source access:** paid article with a public preview; no subscription controls bypassed.
- **Related supplied visual:** archive page 425, polling/SSE/WebSocket timing visual.
- **Visual inspected:** PDF page `307` at full size.

## B. Core concept

### SOURCE

The preview explicitly says neither synchronous nor asynchronous communication is objectively better. Synchronous calls are direct, predictable and easier to trace but couple caller latency/availability to the callee. Asynchronous communication publishes a message, queues a job, or fires an event so the sender can continue; it improves decoupling/elasticity but makes debugging, consistency and control more complex. The trade-offs named are latency versus throughput, simplicity versus resilience, and immediate response versus eventual progress.

### INFERENCE

This fits the SquiFlow review method exactly: choose from the semantic timing/reliability requirement, not from a comparison. The first question is whether the caller actually needs the authoritative result before continuing. The second is whether the work must survive process/dependency failure after the caller is released.

### EXTERNAL KNOWLEDGE / CAVEAT

The related visual shows short polling, long polling, WebSocket and SSE—client update mechanisms—not durable backend asynchronous processing. WebSocket/SSE can be live but lossy, while a database-backed job/outbox can be asynchronous and durable without any live connection. Likewise, asynchronous messaging introduces duplicate delivery, ordering, poison work, backlog, idempotency and reconciliation obligations.

## C. Important concepts

- time coupling and immediate authoritative response;
- availability/latency coupling of synchronous dependencies;
- durable queue/job versus fire-and-forget;
- after-commit consequences;
- at-least-once redelivery and idempotency;
- backlog/age/backpressure;
- polling/SSE/WebSocket as live-update channels;
- orchestration/observability of eventual progress;
- in-process versus real network boundary;

## D. Diagram / visual explanation

The visual compares browser update mechanisms over time: periodic short polling, request-held long polling, full-duplex WebSocket, and one-way SSE. That visual should not be conflated with the article’s broader service-communication comparison. For SquiFlow, a Worker job may be durable asynchronous execution, while SignalR/SSE merely tells the UI that the durable status changed. Losing the live signal must never lose the business truth.

## E. How it works — step by step

1. Ask whether the interaction is inside one runtime; if so, use an in-process call unless another real boundary exists.
2. For a real boundary, decide whether the caller needs a result before its own operation can complete.
3. Use synchronous HTTP/gRPC only when immediate answer is part of the semantics.
4. Use durable outbox/job/Worker for long-running or after-commit consequences.
5. Define job/message idempotency, ordering, retries, poison/quarantine and backlog visibility.
6. If users need live progress, layer polling/SSE/WebSocket/SignalR over durable status rather than replacing it.
7. Propagate correlation/causation across sync and async boundaries.
8. Measure throughput, latency, queue age and failure amplification before changing the communication model.

## F. Why it matters

SquiFlow has both immediate authority decisions—such as validation/approval/refund acceptance—and delayed consequences such as document generation, notifications, provider delivery, reconciliation and future background work. Treating all work synchronously risks cascades and long request occupancy; treating all work asynchronously makes user semantics and consistency unnecessarily complex.

## G. Trade-offs / limitations

Synchronous communication is easier to reason about but can amplify downstream latency/outages and tie resource lifetimes together. Durable async improves temporal decoupling and throughput shaping but adds persistence, duplicate handling, eventual-state UX, queue operations, recovery and observability. Live push improves UX but adds connection/proxy/resource complexity and does not make work durable.

## H. Alternatives / comparisons — fit, not winner/loser

```text
in-process call
    -> ordinary modular-monolith collaboration

synchronous task HTTP / gRPC
    -> real boundary + immediate result required

durable outbox + Worker/job
    -> long-running / after-commit consequence

polling
    -> low-frequency/simple status refresh

SSE
    -> one-way live update

WebSocket / SignalR
    -> bidirectional/live interaction

webhook
    -> outbound callback to another system
```

Several may participate in one user journey.

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** in-process module calls within Core/Admin/Worker hosts rather than fake network boundaries.
- **KEEP:** short authoritative business commands synchronous when the caller needs the immediate decision.
- **KEEP:** durable outbox/job/Worker path for true long-running or after-commit consequences.
- **KEEP:** semantic idempotency and bounded retries because durable async assumes redelivery/ambiguity.
- **LATER / SCALE TRIGGER:** SignalR/WebSocket/SSE only for a concrete live-UX requirement; durable status remains authority.
- **NEEDS MEASUREMENT:** gRPC versus HTTP at a real synchronous boundary, and polling versus push based on frequency/latency/resource/network evidence.
- **AVOID:** fire-and-forget in-memory work for consequences that must survive crash or provider outage.
- **AVOID:** describing async as universally more resilient without accounting for queue/backlog/duplicate/recovery complexity.

**What are we actually doing and why?** SquiFlow uses synchronous communication for decisions the caller must know now and durable asynchronous work for consequences that can progress after commit and must survive failure. Live-update protocols are separate UX mechanisms. This is a semantic fit decision, not a sync-versus-async winner.

**What would falsify/change this?** If a supposedly synchronous operation routinely waits on slow non-authoritative work, it should be split so the authoritative part finishes and the consequence becomes durable async. If an async workflow cannot explain partial progress/recovery or adds more complexity than a short reliable call, synchronous execution may be better.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What trade-offs does the source name between synchronous and asynchronous communication?
2. Why does synchronous communication couple availability and latency?
3. Why is an asynchronous message/job not automatically durable?

**Critical reasoning**

1. Which SquiFlow operations require an immediate authoritative response?
2. Which consequences should happen only after the authoritative transaction commits?
3. Why is WebSocket/SSE not the same thing as durable asynchronous processing?
4. How do correlation and causation cross an async boundary?
5. What user state is shown while durable work is pending or ambiguous?

**Trade-off**

1. When is synchronous simplicity worth availability coupling?
2. When does durable async improve throughput but worsen completion latency?
3. When is polling cheaper/more reliable than a push connection?

**Failure / edge**

1. Worker receives the same job twice. What prevents duplicate effects?
2. Queue backlog grows for hours while API remains healthy. What user/operator evidence is required?
3. Live progress connection drops. How does the client recover status?
4. A synchronous downstream call times out after the downstream committed. Which idempotency/reconciliation rules apply?

**Implementation**

1. Which business transaction writes the outbox/job?
2. How are lease/fencing, retry, poison/quarantine and graceful drain implemented?
3. Which metrics expose oldest-item age and no-progress work?
4. How is live UI notification rebuilt from durable state after reconnect?

**System design interview**

1. Design quotation approval followed by document generation and customer notification using appropriate sync/async boundaries.
2. Compare HTTP/gRPC synchronous RPC with durable Worker execution without declaring one universally better.

**Challenge**

1. An engineer proposes making every command asynchronous for ‘resilience.’ Identify which SquiFlow operations would become harder or less correct and design the minimal mixed model.
