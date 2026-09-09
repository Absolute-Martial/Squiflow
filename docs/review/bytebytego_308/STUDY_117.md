# ByteByteGo Exhaustive Sequential Study — Archive Entries 111-120

**Source:** `ByteByteGo_Selected_Archive_plus_Web_Content_Images_URLs(2).pdf`  
**Coverage in this file:** archive entry `117`  
**Review method:** source first; diagrams visually inspected; `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE` separated; SquiFlow implications follow both `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`.

The standing rule for this batch is stronger than “compare technologies.” For every material exposure the review asks:

```text
What is SquiFlow actually doing at this boundary?
What real user/business/operational problem does that solve?
Why does the current mechanism fit that problem?
What alternative could be better for a different surface?
Could several mechanisms coexist?
What authority does the mechanism own — and what does it NOT own?
What failure/recovery burden does it introduce?
What evidence would justify adoption?
What evidence would falsify/change the current choice?
What is documented versus actually implemented today?
```

A source comparison, popularity claim, or product catalog is never sufficient by itself to choose SquiFlow architecture.

---

# 117 — Can a web server provide real-time updates?

## A. Identification

- **Archive entry:** `117`
- **PDF pages:** `229-230`
- **Original archive pages:** `425-426`
- **Multi-page:** yes
- **Visual inspected:** PDF page `229`.

## B. Core concept

### SOURCE

The article explains four browser update models:

- short polling;
- long polling;
- WebSocket;
- SSE (Server-Sent Events).

It says browsers initiate the communication; WebSocket and SSE then allow the server to send updates after a connection has been established. SSE is described as unidirectional and WebSocket as full-duplex.

### INFERENCE

The real design axis is not “can the server push?” but the required **directionality, freshness, connection lifetime, fallback behavior, and durability** of updates.

### EXTERNAL KNOWLEDGE / CAVEAT

The statement that with SSE “the browser cannot send a new request to the server” is too broad. The **SSE stream itself** is server→client, but the browser can still issue ordinary HTTP requests independently.

WebSocket is full-duplex but does not by itself provide durable delivery, replay, exactly-once semantics, or business-state authority. SSE supports reconnection and event IDs in standard usage, but application recovery still must be designed.

Frameworks such as SignalR can negotiate/fallback among WebSockets, SSE, and long polling depending on environment and server/client support.

## C. Important concepts

- polling interval/freshness;
- request amplification;
- long-held HTTP responses;
- SSE event stream;
- WebSocket full-duplex channel;
- reconnect/backoff;
- load balancer/proxy idle timeouts;
- session affinity when runtime requires it;
- backpressure;
- message ordering/sequence;
- missed-update recovery;
- durable state vs transient signal;
- authorization changes during long-lived connections;
- multi-node fan-out only when topology requires it.

## D. Diagram / visual explanation

Page `229` shows four time diagrams:

- **short polling:** repeated request/response spikes separated by waits;
- **long polling:** request waits open until new data, then reconnects;
- **WebSocket:** criss-cross bidirectional messages over a persistent connection;
- **SSE:** persistent connection with server-to-browser arrows only.

The visual is effective at showing communication shape, but not durability/recovery semantics.

## E. How it works — step by step

### Polling

1. Browser periodically asks for new state.
2. Server responds immediately (short polling) or waits until data/change/timeout (long polling).
3. Browser repeats.

### SSE

1. Browser opens an HTTP event-stream request.
2. Server keeps the response stream open.
3. Server emits events over time.
4. Browser reconnects on interruption; application reconciles missed/current state.

### WebSocket

1. Browser establishes a WebSocket-compatible connection.
2. Connection remains long-lived.
3. Either side can send frames.
4. Application handles interruption/reconnect/authorization/state recovery.

## F. Why it matters

SquiFlow may need live UI updates for operation status, notifications, admin dashboards, or “something changed” hints. But live signaling must never become the only copy of business progress.

## G. Trade-offs / limitations

### Short polling

- simplest;
- resilient to connection loss by nature;
- wastes requests / increases stale window at longer intervals.

### Long polling

- reduces empty polls;
- more server/proxy connection management;
- reconnect cycle remains.

### SSE

- simple server→browser stream;
- HTTP-friendly and suitable for one-way updates;
- not full duplex on the same stream;
- browser/support/proxy behavior must be tested.

### WebSocket

- efficient full-duplex low-latency channel;
- greater connection lifecycle/backpressure/multi-node complexity;
- infrastructure/proxy/firewall compatibility issues;
- no built-in durable state guarantee.

## H. Alternatives / comparisons — fit, not winner/loser

```text
ordinary polling
  good enough for low-frequency status/read updates

SSE
  strong fit for simple server->browser event stream

WebSocket / SignalR
  strong fit for interactive/full-duplex or framework-managed live updates

gRPC streaming
  candidate for non-browser typed process/client boundaries where transport fit exists

durable DB/outbox/status resource
  authoritative recovery source regardless of live transport
```

The browser UI may use SignalR while Workstation sync uses HTTP/gRPC and Worker uses durable jobs. These are not competing system-wide decisions.

## I. Real implementation considerations

- identify exact screen/journey needing freshness;
- define acceptable update delay;
- keep durable status/query fallback;
- reconnect with backoff/jitter;
- reauthorize/revalidate current session on reconnect and for sensitive operations;
- handle token/session expiry during connection;
- bound per-connection memory and outbound queue;
- drop/coalesce low-value signals under pressure rather than business truth;
- proxy idle timeouts/keepalive;
- multi-node fan-out only if multiple Web hosts actually exist;
- prevent tenant/cross-user subscription mistakes;
- monitor connection count, reconnect rate, queue/drop behavior.

### Implications for the Current Implementation

- **KEEP:** WebSocket/SignalR, if used, is live signaling only, never durable truth.
- **KEEP:** Web remains online-only for business mutation; live connection loss must not show false success.
- **NEEDS MEASUREMENT / product need:** no live-update technology should be selected until a concrete UX freshness requirement exists.
- **KEEP:** polling may be the simpler correct choice for low-frequency status updates.
- **LATER / SCALE TRIGGER:** shared backplane/distributed live-session infrastructure only after real multi-node Web topology.
- **AVOID:** using live transport as the only operation-progress or sync acknowledgement record.

**Failure cases:** connection silently drops; reverse proxy timeout; client sleeps/resumes; session revoked while stream remains; message bursts exceed client speed; multi-node reconnect lands on another process; live signal arrives before durable transaction is visible; signal never arrives after commit.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. What is the difference between short and long polling?
2. Which direction does SSE carry events on its stream?
3. What extra communication direction does WebSocket provide?

**Critical reasoning questions**
1. What exact SquiFlow screen currently needs real-time updates rather than polling?
2. Why should a WebSocket notification say “refresh durable state” rather than become the business fact itself?
3. Why is “SSE means browser cannot send requests” misleading?
4. What happens to authorization on a connection open for hours?
5. How does SignalR change the WebSocket-vs-SSE decision?

**Trade-off questions**
1. When is polling preferable despite being less fashionable?
2. When is SSE simpler than WebSocket?
3. What operational cost appears with thousands of persistent connections?

**Failure / edge-case questions**
1. Operation commits but live signal is lost. What should the user see after refresh?
2. Client reconnects after 30 minutes and missed 100 events. How is state recovered?
3. A tenant's connection is accidentally subscribed to another tenant's channel. What defense prevents leakage?
4. Browser sleeps and resumes with an expired session. What happens next?

**Implementation questions**
1. What per-connection buffers/limits are required?
2. What proxy timeouts/keepalive behavior must be tested?
3. What correlation/sequence identifiers make reconnect diagnosis possible?
4. What durable endpoint/query is the fallback source?

**System design interview questions**
1. Design live job-progress updates that remain correct if every live message is lost.
2. Compare SSE, WebSocket, and polling for a low-volume admin dashboard.

**Challenge**
An Admin dashboard shows Worker progress. The WebSocket disconnects after the job commits `Succeeded`, then reconnects to another Web process after the user's platform permission was revoked. Design the status recovery and authorization path.

---
