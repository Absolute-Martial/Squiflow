# ByteByteGo Exhaustive Sequential Study — URL Entry 026

# URL 026 — A Guide to Async Patterns in API Design

## A. Identification

- **URL entry:** `026`
- **PDF page:** `270`
- **Source URL:** `https://blog.bytebytego.com/p/a-guide-to-async-patterns-in-api`
- **Public source access:** paid post; public preview inspected. The inaccessible pattern-by-pattern continuation is not reconstructed.
- **Related visual:** archive page `425`, short polling / long polling / WebSocket / SSE visual.
- **Visual inspection:** PDF page `270` rendered and inspected in full.

## B. Core concept

### SOURCE

The public preview says request-response handles most Web/API work but not every interaction. It identifies cases where:

- work takes too long for one request;
- events happen on the server’s schedule;
- interaction is continuous rather than one-shot;
- messages must outlive the original connection.

The source lists short polling, long polling, server-sent events (SSE), WebSockets, webhooks, asynchronous API/status polling, message queues, and GraphQL subscriptions as patterns that extend beyond a single HTTP request/response.

### INFERENCE

“Async API” is not one architecture. The correct SquiFlow choice depends on **what must outlive what**: the business operation, client connection, user session, or notification channel. Durable work, live signaling, callbacks, and polling have different authority and recovery semantics.

### EXTERNAL KNOWLEDGE / CAVEAT

- `202 Accepted` means work was accepted for processing, not that the business outcome succeeded.
- SSE is primarily server-to-client; WebSocket is bidirectional; polling is request/response and can be the most compatible fallback.
- Long-lived connections need reconnect/backpressure/connection-capacity/proxy behavior.
- Webhooks require endpoint authenticity, replay/idempotency, bounded retries, and delivery observability.
- Message queues are usually an internal durable-work mechanism, not a direct browser API.
- GraphQL subscriptions do not create durable truth and should not be adopted merely because GraphQL supports them.
- Cancellation of an async operation cannot be assumed to undo an external effect that may already have happened.

## C. Important concepts

- synchronous request/response;
- durable asynchronous operation resource;
- `202 Accepted` and status polling;
- short polling;
- long polling;
- SSE;
- WebSocket/SignalR;
- webhooks;
- queue-backed work;
- GraphQL subscriptions;
- reconnect/backoff;
- operation idempotency;
- cancellation/OutcomeUnknown;
- durable truth versus live signal.

## D. Diagram / visual explanation

The related visual compares four connection patterns over time:

- **short polling:** repeated requests with idle gaps;
- **long polling:** one request waits until data/update is available, then repeats;
- **WebSocket:** persistent bidirectional/full-duplex channel;
- **SSE:** persistent server-to-client event stream.

It is a directionality/connection-lifetime diagram, not a durability diagram. A WebSocket message can be lost; an SSE connection can drop; polling can miss transient signals unless the underlying state is queryable. Durable business state must live elsewhere.

## E. How it works — step by step

SquiFlow pattern selection should start from the operation:

1. If a short authoritative transaction can finish in the bounded interactive budget, keep it synchronous.
2. If work is long-running/resource-heavy, validate/authorize/idempotently persist a durable operation and return `202` + operation location/status.
3. Worker claims and executes the durable operation under the correct authorization semantics.
4. Client polls status initially unless a real need justifies a live push channel.
5. If live server-driven UX is needed, evaluate SSE versus SignalR/WebSocket by directionality, browser/proxy support, connection count, and reconnect behavior.
6. External callbacks use webhooks only with authenticity/idempotency/retry/reconciliation contracts.
7. Any live notification is a hint/wakeup; client can recover authoritative state after reconnect.
8. Operation states include `Pending/Running/Succeeded/Failed/Cancelled/OutcomeUnknown` or domain-appropriate equivalents.

## F. Why it matters

SquiFlow has clear long-running candidates such as document/report generation, image processing, some imports/integrations, reconciliation, and diagnostics. Keeping those as durable Worker operations avoids tying business completion to an HTTP connection. At the same time, queuing every ordinary command would add needless eventual outcomes and support complexity.

## G. Trade-offs / limitations

Polling:
- simple and compatible;
- can waste requests and add update latency.

Long polling:
- reduces repeated empty responses;
- holds connections and complicates timeout/proxy behavior.

SSE:
- simple server-driven event stream;
- one-way and still needs reconnect/state recovery.

WebSocket/SignalR:
- low-latency bidirectional interaction;
- more connection state, scaling/proxy/backpressure/diagnostic complexity.

Async job/status API:
- durable and explicit;
- adds operation lifecycle, polling, cancellation, retention, and operator tooling.

Webhooks:
- decouple external consumers;
- add delivery/retry/signature/endpoint lifecycle ambiguity.

## H. Alternatives / comparisons — fit, not winner/loser

```text
short authoritative business command
    -> synchronous HTTP

long-running/resource-heavy work
    -> durable operation + Worker + status API

simple live update with compatibility priority
    -> polling may be enough

server-to-client live feed
    -> SSE candidate

bidirectional interactive channel
    -> SignalR/WebSocket candidate

external system notification/callback
    -> webhook candidate

internal durable work
    -> queue/job/outbox; not a browser transport
```

Several patterns can coexist because they solve different timing/direction/durability requirements.

## I. Real implementation considerations / SquiFlow implications

- **KEEP:** ordinary short authoritative business transactions remain synchronous because users need a definitive result and there is no reason to create eventual operation state.
- **KEEP:** long-running/resource-heavy work uses durable operation + Worker semantics when the work truly outlives the request.
- **KEEP:** duplicate async requests reuse the same semantic operation/status via idempotency rather than enqueueing duplicate work.
- **KEEP:** live channels never become durable business/sync truth.
- **LATER / SCALE TRIGGER:** SSE/SignalR-WebSocket/GraphQL subscriptions only when an implemented UX requires lower-latency server-driven updates than polling provides.
- **LATER / SCALE TRIGGER:** webhooks for real external integration callbacks/delivery.
- **NEEDS MEASUREMENT:** connection counts, proxy behavior, reconnect rate, memory/backpressure, polling traffic, and user-perceived update latency before selecting a live pattern.
- **AVOID:** queuing every command because “async scales better.”
- **AVOID:** interpreting `202 Accepted` or a delivered live message as business success.

**What are we doing and why?** We keep short business commands synchronous because immediate authoritative feedback is simplest and clearest. We use durable Worker/status semantics only when work genuinely outlives the request because durability, retry, recovery, and explicit state then matter more than keeping the connection open. We would add a live push mechanism only when real UX latency/traffic proves polling insufficient.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. What four limitations of one-shot request/response does the preview identify?
2. Which async API patterns does the source list?
3. What directionality difference is visible between SSE and WebSocket?

**Critical reasoning questions**
1. Which first SquiFlow operations truly need a durable async status resource and why?
2. Which operations should stay synchronous even though a Worker exists?
3. What user-visible meaning distinguishes `Accepted`, `Running`, `Succeeded`, `Failed`, and `OutcomeUnknown`?
4. Why is a WebSocket notification never sufficient proof that a business transaction exists?
5. What actual UI requirement would justify live push instead of polling?

**Trade-off questions**
1. When is polling the best choice despite inefficiency?
2. When is SSE preferable to WebSocket?
3. When does a durable async operation justify its lifecycle/retention complexity?
4. When is a webhook preferable to an external consumer polling SquiFlow?

**Failure / edge-case questions**
1. Client receives `202` then loses network for a day. How does it recover operation state?
2. User cancels after the Worker has sent an external provider request. What does cancellation mean?
3. WebSocket reconnects after missing updates. How does it catch up from authoritative state?
4. Webhook receiver succeeds but response is lost, causing redelivery. What makes the receiver safe?

**Implementation questions**
1. What operation states/transitions are persisted?
2. How are semantic idempotency and duplicate status lookup implemented?
3. What connection/backpressure/reconnect metrics are required for live channels?
4. How are webhook authenticity, retry, dead-letter/reconciliation, and endpoint disablement represented?
5. How is an async operation authorized when the original actor’s permissions later change?

**System design interview questions**
1. Design asynchronous PDF generation with `202`, durable status, idempotency, cancellation, and Worker recovery.
2. Choose polling, SSE, or SignalR/WebSocket for a live order dashboard and justify it from actual update/directionality requirements.

**Challenge**
A proposal replaces every Core API mutation with `202 + queue` and opens a WebSocket to report completion. Identify which commands gain nothing from this design, which operations genuinely benefit, and what new failure/recovery/user-understanding burden the proposal creates.

---
