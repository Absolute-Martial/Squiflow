# Transport Selection — HTTP, gRPC, In-Process, and Durable Async

**Version:** v0.0.15

## 1. Decision

SquiFlow does not force one communication protocol across every boundary.

The baseline selection rule is:

```text
same runtime host/module composition
→ in-process call

real process/service boundary + immediate response needed
→ synchronous transport selected from workload evidence

long-running or after-commit consequence
→ durable outbox/job/Worker path

multiple independent consumers of a committed fact
→ durable fan-out/pub-sub only when the workload exists
```

gRPC is a **preferred candidate, not a pre-decided universal transport**, for real process/service boundaries where its properties materially fit the implemented workload.

The first candidate areas are:
- Workstation ↔ server synchronization when streaming/binary/generated-contract behavior is valuable;
- future independently deployed backend/service communication where a synchronous RPC boundary is genuinely required.

REST/task-oriented HTTP remains the current ordinary application API baseline for Web/external/business API surfaces unless a concrete workload proves another transport is better.

## 2. What this does not mean

Do not introduce gRPC:
- between ordinary modules inside the same modular-monolith host;
- merely because SquiFlow may later extract services;
- merely because Protobuf is binary;
- merely because generic benchmarks claim gRPC is faster than JSON;
- to replace durable outbox/idempotency/conflict/retry semantics;
- for long-running consequences that belong in durable asynchronous work.

A REST call before a gRPC call is **not inherent to gRPC**. That is only one possible gateway/service architecture.

Likewise, a claim such as “gRPC is 5x faster than JSON” is not a SquiFlow architectural guarantee. SquiFlow measures representative payloads, serialization cost, bandwidth, latency, CPU, memory, proxy/edge behavior, and end-to-end business/database time before using performance as the reason to select gRPC.

## 3. Workstation synchronization

The Workstation synchronization architecture remains transport-independent at the correctness layer:

```text
local durable business transaction + outbox
→ authentication/device/session
→ tenant authorization
→ idempotency
→ version/concurrency/conflict handling
→ server business/rule validation
→ authoritative central transaction
→ durable local acknowledgement
```

Changing HTTP/JSON to gRPC/Protobuf does not solve those correctness requirements.

However, gRPC is a preferred candidate for the Workstation transport when an implemented sync workload benefits materially from:
- client/server or bidirectional streaming;
- many small repeated sync messages where framing/serialization overhead is significant;
- large enough payload volume that Protobuf bandwidth/CPU savings are measurable;
- generated strongly versioned contracts that simplify Workstation/server compatibility;
- long-lived efficient connections whose operational behavior is proven through the selected edge/deployment topology.

Phase 3 should keep the first sync transport choice open. If the representative sync POC shows a material streaming/binary/generated-contract opportunity, compare gRPC against the simpler HTTP baseline before finalizing the transport.

The user-visible authority states (`LocalCommitted`, `PendingRemote`, `Authoritative`, `Conflict`, `Rejected`, `AuthorizationChanged`, `UpgradeRequired`) remain the same regardless of transport.

## 4. Backend/service communication

Inside Core API, Admin API, Worker, or another single runtime host, modules remain in-process.

When a capability becomes a **real separately deployed service/process** and requires an immediate synchronous answer, gRPC should be considered early as a preferred candidate alongside HTTP.

Adoption still requires evidence that at least one gRPC property matters enough to justify the extra protocol/runtime/version/operations surface, such as:
- streaming;
- high-frequency low-latency RPC;
- meaningful binary payload efficiency;
- generated contract value across independently versioned processes.

If the interaction is a long-running or after-commit consequence, durable asynchronous execution remains preferred even if gRPC exists elsewhere.

## 5. Admin API and Core API

The separate Admin API and Core API are independent security/availability planes.

Their current normal architecture is **not**:

```text
Admin Web → Admin API → Core API
```

and the existence of gRPC must not recreate that dependency merely because both processes exist.

If a future explicitly justified operation requires direct synchronous Admin API ↔ Core API/service communication, gRPC is a preferred candidate to evaluate. The decision remains workload-specific and must preserve:
- Admin/Core failure independence;
- least-privilege authentication/authorization;
- timeout/retry/idempotency behavior;
- version compatibility;
- observability;
- no bypass of shared domain/data invariants.

Where shared reviewed modules/database ownership or a durable Worker/control command is the correct architecture, use that instead of adding a network RPC.

## 6. Browser and external clients

Browser-facing SquiFlow Web remains HTTP-oriented by default. Native gRPC is not assumed to be the universal browser API.

If a future browser workload considers gRPC-Web or transcoding, it must justify the additional proxy/client/tooling/compatibility surface rather than forcing browser APIs to follow the Workstation/internal-service choice.

External/partner APIs likewise keep stable HTTP semantics unless a real integration contract explicitly requires gRPC.

## 7. Adoption gate

A gRPC boundary may be selected only when all applicable items are answered:

1. Is this a real process/network boundary rather than an in-process module call?
2. Does the caller actually need synchronous RPC/streaming rather than durable async work?
3. Which gRPC property materially helps this workload?
4. What representative benchmark/POC demonstrates the benefit?
5. How are authentication, authorization, tenant context, deadlines, retries and idempotency handled?
6. How are `.proto` contracts versioned across skipped Workstation/backend releases?
7. Does the selected edge/proxy/deployment support the required HTTP/2/streaming behavior reliably?
8. What happens when the peer is slow, unavailable, times out, or returns after the caller gives up?
9. How is the traffic traced/debugged/operated in production?
10. Is the added complexity still justified versus HTTP or durable async execution?

If these questions do not have a concrete answer, gRPC remains a candidate rather than an implementation dependency.

## 8. Revisit triggers

Revisit or strengthen gRPC when one of these becomes real:
- Workstation reconnect/sync traffic becomes chatty or bandwidth/serialization heavy;
- bidirectional or server-streaming sync materially simplifies the implementation;
- a separately deployed service has sustained high-frequency synchronous calls;
- generated contracts solve a demonstrated cross-process compatibility problem;
- an existing HTTP boundary becomes a measured latency/CPU/bandwidth bottleneck.

Do not revisit merely because SquiFlow adds another module or because a generic architecture diagram uses gRPC.
