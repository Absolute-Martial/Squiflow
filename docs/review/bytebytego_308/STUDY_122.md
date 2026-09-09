# ByteByteGo Exhaustive Sequential Study — Archive Entry 122

# 122 — 8 Popular Network Protocols

## A. Identification

- **Archive entry:** `122`
- **PDF pages:** `239-240`
- **Original archive pages:** `439-440`
- **Multi-page:** yes
- **Visual inspected:** PDF page `239`; continuation page `240` also rendered/inspected.

## B. Core concept

### SOURCE

The article lists eight widely used protocols and explains a basic mechanism/use case for each:

1. FTP — separate control/data channels for file upload/download.
2. TCP — reliable connection established using a three-way handshake.
3. UDP — lightweight connectionless datagrams for low-latency transfer.
4. HTTP — requests/responses for Web resources.
5. HTTP/3 (QUIC) — UDP-based multiplexed HTTP transport intended to reduce latency.
6. HTTPS — HTTP protected by encryption/key exchange.
7. SMTP — email submission/delivery through mail servers.
8. WebSocket — full-duplex real-time bidirectional communication established from an HTTP context.

### INFERENCE

The article is a useful compact reminder that a system can use multiple protocol families at once: transport, Web/API, secure Web, live communication, email and file transfer.

### EXTERNAL KNOWLEDGE / CAVEAT

Several statements are deliberately introductory and should not become architecture facts without qualification:

- HTTP is not universally TCP-based because HTTP/3 uses QUIC over UDP.
- HTTPS is not one fixed “public key + session key over TCP” mechanism. HTTP/1.1/2 usually use TLS over TCP, while HTTP/3 integrates TLS 1.3 with QUIC over UDP; modern TLS commonly derives traffic secrets using ephemeral key agreement.
- UDP does not inherently implement request/response. Applications define their own message semantics.
- HTTP/3 is not simply “more reliable than TCP”; QUIC provides reliable multiplexed streams with different loss/handshake/head-of-line characteristics.
- FTP is unencrypted by default and has separate control/data channel/NAT/firewall complexity. SFTP, FTPS and HTTPS/object-storage workflows are different secure alternatives.
- WebSocket should normally be protected with TLS (`wss`) on untrusted networks and remains a live transport, not durable storage.

## C. Important concepts

- TCP handshake/reliable byte stream;
- UDP datagrams;
- QUIC/HTTP3 multiplexed streams;
- HTTP request/response semantics;
- TLS/HTTPS;
- WebSocket full-duplex sessions;
- SMTP server-to-server/submission semantics;
- FTP active/passive/control/data paths;
- firewall/NAT/proxy traversal;
- secure versus insecure defaults;
- application retry/idempotency above transport;
- protocol negotiation;
- connection longevity and resource cost;
- compatibility/fallback.

## D. Diagram / visual explanation

Page `239` is a table with columns **Protocol**, **How does it work?**, and **Use Cases**. It visually places HTTP, HTTP/3, HTTPS, WebSocket, TCP, UDP, SMTP and FTP beside each other.

The table should not be read as peers competing for one SquiFlow slot. For example, HTTPS is HTTP protected by TLS; HTTP/3 is an HTTP transport version over QUIC; TCP/UDP are underlying transport primitives; SMTP and FTP solve different application domains.

## E. How it works — step by step

For SquiFlow's current ordinary API surface:

1. DNS resolution determines the server address.
2. A supported secure HTTP transport is negotiated through the runtime/edge.
3. HTTPS protects the connection.
4. HTTP request/response carries the SquiFlow API contract.
5. Application authentication/authorization/idempotency/domain correctness runs above transport.
6. HTTP/1.1/2/3 differences may change connection/latency behavior but do not change command semantics.
7. If a live channel is later needed, WebSocket/SignalR may coexist with HTTP for notifications while durable state remains in the DB/outbox.
8. If email delivery is implemented, SMTP may be a provider-facing transport — or an email API/provider may hide SMTP from SquiFlow entirely.
9. File exchange uses the mechanism that matches the partner/security requirement; FTP is not assumed merely because it is common.

## F. Why it matters

The article reinforces an important design discipline: protocol selection should be **layer-aware**. Saying “we use HTTPS” does not answer every communication question, and saying “we use HTTP/3” does not change the business API contract.

## G. Trade-offs / limitations

### TCP / HTTP/1.1/2

- broad compatibility and mature tooling;
- TCP head-of-line behavior can affect multiplexed application streams;
- long-lived connection handling/resource limits matter.

### QUIC / HTTP3

- faster connection establishment in some scenarios;
- independent stream loss behavior can improve Web performance;
- UDP may be blocked/degraded by enterprise networks/NAT/security devices;
- support/observability differs across edge/server/client stacks.

### WebSocket

- efficient bidirectional live channel;
- long-lived connection state and reconnect/backpressure complexity;
- not durable/replayable by itself.

### SMTP

- standard interoperable mail transport;
- deliverability, reputation, bounce handling, authentication and provider policy are substantial operational concerns.

### FTP

- widespread legacy support;
- plaintext default and awkward firewall/NAT model;
- often inferior to SFTP/HTTPS/object storage for new sensitive integrations.

## H. Alternatives / comparisons — fit, not winner/loser

```text
HTTP/1.1 / HTTP/2 / HTTP/3
    -> same broad Web/API family with different transport properties
    -> negotiate/use what deployment proves reliable

WebSocket / SSE / polling
    -> choose by UX directionality/frequency/reconnect need

SMTP / mail-provider HTTPS API
    -> choose by provider, delivery ownership, observability and operational burden

FTP / FTPS / SFTP / HTTPS upload / object-storage signed flow
    -> choose by partner compatibility and security/recovery contract

raw TCP/UDP
    -> only when a concrete protocol/device requirement justifies custom application framing
```

## I. Real implementation considerations

- HTTP transport-version negotiation must not alter API semantics;
- proxy/edge fallback and customer-network behavior must be tested;
- long-lived WebSocket/gRPC connections need limits, drain/reconnect and telemetry;
- SMTP/email path needs delivery/bounce/duplicate/OutcomeUnknown semantics if SquiFlow owns sending;
- file-transfer integration must define encryption, credentials, malware/content validation, object identity, retry and duplicate behavior;
- direct TCP/UDP exposure expands attack and operations surface.

### Implications for the Current Implementation

- **KEEP:** HTTPS + HTTP remains the ordinary external application transport because it fits Web/business API needs and ecosystem compatibility.
- **KEEP:** HTTP/1.1/2/3 selection is runtime/edge evidence, not business semantics.
- **NEEDS MEASUREMENT:** HTTP/3 only if actual edge/client/customer-network measurements show worthwhile benefit; no app logic should require it.
- **LATER / REQUIREMENT TRIGGER:** WebSocket/SignalR for live UX, SMTP/email-provider transport for actual notifications, SFTP/other file protocols for actual partner file exchange.
- **AVOID at new SquiFlow-controlled sensitive boundaries:** plaintext FTP when a secure, supportable alternative exists; this is a boundary-specific security judgment, not a claim that legacy FTP integrations can never exist.
- **AVOID:** custom raw TCP/UDP APIs merely for perceived speed.

**What are we doing and why?** We use Web-standard HTTPS because the current clients and API semantics fit it. We keep transport versions negotiable because they can improve performance without changing application correctness. Other protocols remain candidate integrations when their domain appears.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. How do TCP and UDP differ at the transport level?
2. How does HTTP/3 relate to QUIC and UDP?
3. What does WebSocket add beyond ordinary HTTP request/response?

**Critical reasoning questions**
1. Why is “HTTP uses TCP” an incomplete architecture statement in 2026?
2. Which SquiFlow decisions would remain identical if the edge negotiated HTTP/3 tomorrow?
3. Why should FTP popularity not make it a default SquiFlow file-transfer choice?
4. If email is needed, what decides between direct SMTP and an HTTPS mail provider API?
5. Why does reliable TCP transport not make a payment POST exactly-once?

**Trade-off questions**
1. What customer-network conditions can make HTTP/3 worse than HTTP/2?
2. When is a WebSocket worth the long-lived connection/reconnect cost?
3. When can a standardized file-transfer protocol be better than a custom upload API?

**Failure / edge-case questions**
1. UDP/443 is blocked but TCP/443 works. What must SquiFlow do?
2. WebSocket reconnects after ten minutes offline. How does the UI reconstruct missed business changes?
3. SMTP accepts a message but response is lost. What evidence prevents duplicate notification behavior if duplicates matter?
4. FTP/SFTP partner uploads the same file twice under different names. What is the semantic dedupe strategy?

**Implementation questions**
1. What integration tests prove HTTP-version changes preserve idempotency/error semantics?
2. How will edge observability distinguish QUIC negotiation failure from application 5xx?
3. What limits apply to long-lived live connections?
4. Which file-transfer credentials are rotated and where are they stored?

**System design interview questions**
1. Design protocol fallback for a desktop client across restrictive enterprise networks.
2. Design a secure external file-ingestion integration and explain when SFTP beats HTTPS/object storage.

**Challenge**
A customer requires nightly inbound files from a legacy FTP-capable machine, while SquiFlow policy prefers encrypted transfer. Design a migration/containment solution that respects the legacy constraint without making plaintext FTP a general platform baseline.

---
