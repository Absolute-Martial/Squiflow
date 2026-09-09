# ByteByteGo Exhaustive Sequential Study — URL Entry 022

# URL 022 — Common Network Protocols Every Engineer Should Know

## A. Identification

- **URL entry:** `022`
- **PDF page:** `266`
- **Source URL:** `https://blog.bytebytego.com/p/ep195-common-network-protocols-every`
- **Public source access:** the relevant protocol section is publicly accessible and was inspected directly.
- **Exact archive-title overlap:** archive `121`; this URL occurrence is independently reviewed.
- **Related visual:** archive page `437`, closely matching the source section.
- **Visual inspection:** PDF page `266` rendered and inspected in full.

## B. Core concept

### SOURCE

The source presents a broad protocol map. It says:

- TCP emphasizes reliable transport;
- UDP prioritizes low overhead/speed;
- QUIC provides modern transport behavior over UDP;
- HTTP powers Web communication;
- TLS secures traffic;
- DNS maps names to addresses;
- SSH supports remote access;
- SFTP/SMB support file access;
- WebSocket, WebRTC, and MQTT support live/messaging use cases;
- OAuth/OpenID support identity/access flows;
- DHCP, NTP, ICMPv6, LDAP, SMTP, IMAP, WireGuard/IPsec and related protocols support infrastructure, directory, email, and VPN functions.

### INFERENCE

The useful architecture lesson is not “pick the best protocol.” It is that protocol selection is layered. Secure transport, request/response, identity, naming, live signaling, file exchange, remote operations, device messaging, and time synchronization solve different problems and can legitimately coexist.

### EXTERNAL KNOWLEDGE / CAVEAT

The infographic mixes protocol layers and uses typical transport/port labels as a compact teaching device. Those labels are not universal deployment rules.

Important qualifications:

- HTTP does not universally mean TCP: HTTP/3 runs over QUIC/UDP.
- QUIC uses UDP as its substrate while implementing its own connection, reliable stream, congestion-control, multiplexing, and integrated TLS 1.3 handshake semantics.
- OAuth 2.0 is primarily an authorization framework; OpenID Connect is commonly the authentication/identity layer built on OAuth 2.0.
- SFTP is the SSH File Transfer Protocol, not “FTP over TLS.”
- gRPC commonly uses HTTP/2 or other supported transports; a single port/transport label is not the architecture.
- WebSocket establishment differs across HTTP versions; the familiar HTTP/1.1 Upgrade path is not the only standardized mechanism.
- Network reachability, TLS, VPN, or service identity never by themselves grant tenant/business permission.

## C. Important concepts

- transport reliability/ordering/flow control;
- latency and connection setup;
- HTTP versions and QUIC;
- TLS trust/certificate lifecycle;
- DNS routing/caching/TTL;
- OIDC/OAuth identity flow;
- gRPC typed RPC/streaming;
- WebSocket/SSE/SignalR live updates;
- MQTT device/pub-sub workloads;
- SFTP/SMB file exchange;
- SSH/VPN private operations;
- NTP/time synchronization;
- SMTP/IMAP email paths;
- proxy/firewall/customer-network constraints;
- retry/idempotency above the transport layer.

## D. Diagram / visual explanation

The visual is a grid of protocol families and typical use cases. It should be read as a **responsibility inventory**, not as one stack SquiFlow needs to deploy.

Current SquiFlow mapping:

```text
public Web/Core/Admin traffic
    -> HTTPS/TLS + HTTP

human identity
    -> OIDC/OAuth through ZITADEL

routing/custom domains
    -> DNS

private infrastructure recovery
    -> SSH/private-network/VPN capability as deployment requires

time health
    -> host/NTP synchronization

Workstation/real service RPC candidate
    -> gRPC only if representative workload earns it

live UX candidate
    -> polling/SSE/SignalR-WebSocket when a real screen requires it

other protocols
    -> only from concrete partner/device/operations requirements
```

## E. How it works — step by step

A normal Web/API interaction can involve several independent layers:

1. DNS resolves a SquiFlow/custom-domain hostname.
2. IP transport reaches the edge/server.
3. TLS authenticates/encrypts the transport channel.
4. HTTP carries the application request.
5. ZITADEL OIDC/OAuth establishes user identity/session evidence.
6. SquiFlow derives TenantContext and applies OpenFGA/domain authorization.
7. The authoritative transaction executes.
8. Observability records protocol/dependency/business evidence.
9. A live channel, if used, only signals change; durable state remains recoverable by normal API/sync reads.
10. Private SSH/VPN recovery remains an operator channel, never tenant business authority.

## F. Why it matters

SquiFlow already depends on several protocols for different reasons. Explicitly documenting **what each protocol does and does not own** prevents both protocol sprawl and false consolidation. HTTPS cannot replace OIDC; OIDC cannot replace OpenFGA; gRPC cannot replace semantic idempotency; WebSocket cannot replace durable state.

## G. Trade-offs / limitations

Every extra protocol adds:
- firewall/proxy/customer-network compatibility work;
- certificate/credential rotation;
- different timeout/reconnect/backpressure behavior;
- deployment and monitoring surface;
- version compatibility obligations;
- operator troubleshooting burden.

At the same time, forcing one protocol onto every workload can also be costly: typed streaming RPC, live browser signaling, file exchange, and private recovery have different properties.

## H. Alternatives / comparisons — fit, not winner/loser

```text
ordinary Web/external request-response
    -> HTTPS + HTTP

Workstation sync / high-frequency typed stream
    -> HTTP or gRPC by measured need

live server-to-browser update
    -> polling / SSE / SignalR-WebSocket by directionality/frequency

future device pub-sub
    -> MQTT only when device/network semantics justify it

partner file exchange
    -> HTTPS/object-storage API or SFTP/SMB by partner environment

private operator access
    -> SSH/VPN/private network as deployment requires
```

The correct answer can include several protocols because the boundaries differ.

## I. Real implementation considerations / SquiFlow implications

- **KEEP:** HTTPS/TLS for public application traffic because encrypted authenticated transport is required.
- **KEEP:** OIDC/OAuth through ZITADEL for human identity; business authorization remains TenantContext + OpenFGA + domain state.
- **KEEP:** DNS for routing/custom domains but never as tenant authority.
- **KEEP:** time synchronization as an operational dependency while sequence/version/idempotency evidence owns correctness where wall-clock ambiguity is unsafe.
- **KEEP:** private SSH/network recovery as infrastructure operations, not a business API.
- **NEEDS MEASUREMENT:** gRPC at Workstation/real process boundaries when streaming/binary/generated-contract/high-frequency properties materially help.
- **LATER / SCALE TRIGGER:** WebSocket/SSE/SignalR, MQTT, SFTP/SMB, LDAP, VPN, SMTP/IMAP only when the exact live/device/file/directory/network/mail requirement exists.
- **AVOID:** raw custom TCP/UDP application protocols without a requirement higher-level protocols cannot satisfy economically.
- **AVOID:** selecting protocols from a matrix based on popularity or apparent performance.
- **Duplicate traceability:** URL `022` is independently complete despite archive `121`.

**What are we doing and why?** We keep a small protocol set because each selected protocol solves a concrete boundary: HTTPS secures application transport, OIDC/OAuth supplies identity, DNS routes names, private network protocols support recovery, and future gRPC/live protocols remain narrowly scoped candidates. We would add another protocol only when a customer/device/workload requirement demonstrates a property the existing path cannot provide reliably or economically.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. Which source protocols belong mainly to transport, Web/security, identity, live communication, files, and operations?
2. Why is QUIC not just raw UDP?
3. Why do OAuth/OIDC solve a different problem from TLS?

**Critical reasoning questions**
1. Which protocol families does SquiFlow actually need in the first paying deployment and what requirement justifies each?
2. Why would adding MQTT merely because it appears in the infographic be weak architecture reasoning?
3. What remains unchanged in Workstation sync correctness if HTTP is replaced with gRPC?
4. Why can a successful TLS handshake or VPN connection never be enough to authorize a tenant object?
5. What paying-customer proxy/firewall constraints could falsify a preferred long-lived streaming transport?

**Trade-off questions**
1. When can gRPC materially outperform/simplify the actual Workstation boundary enough to justify it?
2. When is polling more robust than WebSocket/SSE despite higher latency/traffic?
3. When is SFTP preferable to an HTTPS/object-storage integration?
4. When is a VPN useful for operator recovery and what secrets/operations cost does it add?

**Failure / edge-case questions**
1. DNS resolves correctly but certificate validation fails. What should user/operator diagnosis show?
2. A customer proxy allows HTTPS but breaks HTTP/2 long-lived streams. How does correctness survive?
3. NTP/clock drift causes token/TLS failures while DB state is healthy. How is that distinguished?
4. A WebSocket notification is lost after a committed business transaction. How does the client recover truth?

**Implementation questions**
1. Which public/private ports and listeners exist in the production profile?
2. What credential/certificate rotation is required for each used protocol?
3. How are transport failures mapped to stable FailureCodes without leaking secrets?
4. Which tests prove private/alternate protocol paths cannot bypass Core/Admin authorization?

**System design interview questions**
1. Design SquiFlow’s protocol inventory for Web, Workstation, identity, live UX, private recovery, and future integrations.
2. Explain why HTTP, gRPC, WebSocket, and durable jobs can all be correct in one system.

**Challenge**
A customer permits outbound HTTPS/443 but blocks arbitrary UDP and unreliable long-lived proxy connections. Redesign Workstation sync and live UI behavior so business correctness remains intact, and state what evidence would change the transport choice.

---
