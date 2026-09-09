# ByteByteGo Exhaustive Sequential Study — Archive Entries 111-120

**Source:** `ByteByteGo_Selected_Archive_plus_Web_Content_Images_URLs(2).pdf`  
**Coverage in this file:** archive entry `118`  
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

# 118 — Evolution of HTTP

## A. Identification

- **Archive entry:** `118`
- **PDF pages:** `231-232`
- **Original archive pages:** `427-428`
- **Multi-page:** yes
- **Visual inspected:** PDF page `231`.

## B. Core concept

### SOURCE

The article summarizes HTTP evolution:

- HTTP/0.9: simple GET/HTML.
- HTTP/1.0: headers/status codes, new connection per request.
- HTTP/1.1: persistent connections and additional methods.
- HTTP/2: multiplexing multiple requests over one connection.
- HTTP/3: QUIC over UDP to reduce latency and improve reliability, especially for mobile/real-time applications.

### INFERENCE

The progression aims to improve protocol efficiency while preserving the higher-level HTTP request/response model.

### EXTERNAL KNOWLEDGE / CAVEAT

- HTTP/1.0 had keep-alive extensions in real deployments; “every request required a new connection” is a useful baseline simplification, not universal history.
- HTTP/1.1 persistent connections are default, but pipelining was not widely successful.
- HTTP/2 multiplexes streams over TCP, so packet loss can still cause connection-level transport head-of-line blocking.
- HTTP/3 uses QUIC over UDP and avoids TCP-level head-of-line blocking between independent streams, but “improves reliability” is not a universal guarantee; networks/middleboxes/implementations can influence outcomes.
- Application semantics, idempotency, authorization, and business consistency do not become correct merely by moving to a newer HTTP version.

## C. Important concepts

- persistent connections;
- request methods/status/headers;
- multiplexed streams;
- HPACK/QPACK header compression (context beyond source);
- TCP vs QUIC transport;
- connection establishment/0-RTT considerations;
- transport head-of-line blocking;
- proxy/middlebox compatibility;
- version negotiation/fallback;
- TLS integration;
- protocol version versus application version.

## D. Diagram / visual explanation

Page `231` shows five rows:

- HTTP/0.9 simple GET;
- HTTP/1.0 repeated open/close connections plus methods/status codes;
- HTTP/1.1 persistent connection;
- HTTP/2 multiple colored logical streams inside one TCP connection;
- HTTP/3 multiple logical flows inside one QUIC connection over UDP.

The diagram makes the transport progression easy to see.

## E. How it works — step by step

1. HTTP application semantics define requests/responses.
2. HTTP/1.x sends them over TCP connections with different reuse behavior.
3. HTTP/2 multiplexes streams over one TCP connection and compresses headers.
4. HTTP/3 maps HTTP semantics onto QUIC streams over UDP with integrated modern TLS behavior.
5. Clients/servers/edges negotiate a supported version; application API semantics should remain stable across the transport choice.

## F. Why it matters

SquiFlow does not need to encode business behavior around HTTP/1.1 vs 2 vs 3. It needs the edge/runtime to negotiate supported protocols while API correctness remains transport-independent.

This is particularly relevant to the gRPC candidate because ordinary gRPC commonly relies on HTTP/2 behavior and therefore must be tested through the real customer/edge network path rather than only localhost.

## G. Trade-offs / limitations

- newer protocols can reduce connection overhead and improve multiplexing;
- they also depend on client/edge/network support;
- connection multiplexing can change failure blast radius and flow-control behavior;
- QUIC/UDP may be blocked or shaped differently in enterprise networks;
- protocol-specific optimization adds test/operational surface;
- application bottlenecks may still be DB, authorization, storage, or CPU rather than transport.

## H. Alternatives / comparisons — fit, not winner/loser

HTTP versions are usually negotiated alternatives, not architecture competitors:

```text
HTTP/1.1
  broad compatibility

HTTP/2
  multiplexed streams; common gRPC transport

HTTP/3
  QUIC-based transport with different loss/connection behavior
```

SquiFlow can support/fallback among them without changing business command semantics.

## I. Real implementation considerations

- edge/runtime protocol support matrix;
- ALPN/TLS negotiation;
- HTTP/2 proxy support for gRPC if selected;
- HTTP/3/UDP network/firewall behavior;
- idle/connection/stream limits;
- upload/body bounds;
- cancellation/deadlines;
- observability by negotiated protocol without high-cardinality misuse;
- compatibility/fallback testing on representative customer networks;
- no semantics tied to transport version.

### Implications for the Current Implementation

- **KEEP:** HTTP/1.1/2/3 negotiation is infrastructure detail, not business semantics.
- **KEEP:** Workstation sync correctness is transport independent.
- **NEEDS MEASUREMENT:** gRPC candidate must be benchmarked through actual edge/proxy/customer-network behavior, not just serialization microbenchmarks.
- **LATER / SCALE TRIGGER:** HTTP/3-specific tuning only if measurement shows material value and supported deployment paths.
- **AVOID:** assuming newest HTTP version automatically fixes application latency.

**Failure cases:** QUIC blocked; HTTP/2 proxy resets long stream; edge falls back to HTTP/1.1; multiplexed connection failure interrupts several operations; client and server disagree on supported protocol; retry creates duplicate mutation because transport reset is mistaken for business failure.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. What key change did HTTP/1.1 make to connection reuse?
2. What does HTTP/2 multiplex?
3. What transport does HTTP/3 use?

**Critical reasoning questions**
1. Why does HTTP/3 not make idempotency unnecessary?
2. Why can HTTP/2 help gRPC without proving gRPC is the right SquiFlow sync transport?
3. Which SquiFlow bottlenecks are completely unaffected by a protocol upgrade?
4. Why should protocol version remain outside business-domain contracts?
5. What customer-network constraints could make HTTP/3 or HTTP/2 less reliable in practice?

**Trade-off questions**
1. What compatibility advantage does HTTP/1.1 retain?
2. What operational testing does HTTP/3 add?
3. When is HTTP/2 multiplexing materially valuable?

**Failure / edge-case questions**
1. HTTP/2 connection resets after server commit. How does client retry safely?
2. Corporate network blocks UDP/443. What should HTTP/3-capable clients do?
3. Long-lived gRPC stream is terminated by reverse-proxy timeout. How is state recovered?

**Implementation questions**
1. How will protocol negotiation be observed/tested?
2. Which proxy settings are required for gRPC streaming if selected?
3. Which timeouts belong at client, edge, and server layers?

**System design interview questions**
1. Explain HTTP/2 versus HTTP/3 head-of-line behavior.
2. Design a protocol fallback strategy that preserves API semantics.

**Challenge**
A Workstation sync POC is 25% faster on direct HTTP/2 gRPC, but a representative customer network resets long HTTP/2 connections every 60 seconds while ordinary HTTPS requests are stable. Decide what evidence and design changes are needed before transport selection.

---
