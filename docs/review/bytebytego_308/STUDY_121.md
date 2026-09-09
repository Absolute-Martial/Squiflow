# ByteByteGo Exhaustive Sequential Study — Archive Entry 121

# 121 — Common Network Protocols Every Engineer Should Know

## A. Identification

- **Archive entry:** `121`
- **PDF pages:** `237-238`
- **Original archive pages:** `437-438`
- **Multi-page:** yes
- **Exact URL overlap later:** URL `022` — must still be reviewed independently on PDF page `266`.
- **Visual inspected:** PDF page `237`; continuation page `238` also rendered/inspected.

## B. Core concept

### SOURCE

The article presents a broad protocol map and says internet/application behavior relies on protocols that define how data moves, who can communicate, and how securely it happens.

The visual groups:

- transport: TCP, UDP, QUIC;
- web/security: HTTP, TLS;
- remote access/files: SSH, SFTP, SMB;
- naming/privacy: DNS, DNS-over-HTTPS;
- messaging/RPC/IoT: gRPC, MQTT;
- real-time: WebSocket, WebRTC;
- authorization/identity: OAuth, OpenID;
- directory/time/VPN/network configuration/IPv6: LDAP, NTP, WireGuard, IPsec, DHCP, ICMPv6;
- email: SMTP, IMAP.

The prose says TCP emphasizes reliable delivery, UDP prioritizes speed, QUIC combines reliability-oriented features with UDP-based transport, HTTP powers the Web, TLS secures traffic, DNS resolves names, SSH provides remote access, SFTP/SMB move files, WebSocket/WebRTC/MQTT support live communication, OAuth/OpenID support access/identity, and infrastructure protocols such as DHCP/NTP/ICMPv6/LDAP support network operation.

### INFERENCE

The main architectural lesson is that “networking” is not one protocol decision. Different layers solve different problems: transport, secure channel, naming, identity, remote administration, application RPC, live delivery, mail, device messaging, and private-network connectivity.

### EXTERNAL KNOWLEDGE / CAVEAT

The infographic intentionally mixes layers and abstractions. That is useful as a map but can mislead if treated as one protocol stack.

Important qualifications:

- OAuth 2.0 is primarily an authorization framework; OpenID Connect is the authentication/identity layer commonly built on OAuth 2.0. The visual's `OpenID` label should not be treated as identical to every historical OpenID protocol.
- gRPC commonly rides HTTP/2 and TLS in current production deployments; `TCP/443` is a common deployment shape, not an intrinsic universal gRPC port/transport rule.
- DNS-over-HTTPS is DNS carried over HTTPS. Depending on HTTP version it may use TCP/TLS or QUIC/HTTP3; `TCP/443` is not universal.
- SFTP is the SSH File Transfer Protocol; it is not simply FTP with TLS.
- QUIC uses UDP as its substrate but provides its own connection, congestion control, reliable streams, encryption handshake integration, and multiplexing semantics.
- WebSocket's familiar HTTP `Upgrade` path describes the common HTTP/1.1 establishment mechanism; newer HTTP versions have different standardized establishment mechanisms.
- A protocol's availability does not establish application authority. TLS, VPNs, or network reachability do not grant tenant/business permission.

## C. Important concepts

### Transport and delivery

- TCP reliability, ordering, congestion/flow control and connection semantics;
- UDP datagrams and application-owned reliability/ordering when needed;
- QUIC connections and multiplexed streams over UDP;
- latency/handshake implications;
- connection loss/reconnect behavior;
- backpressure and flow control;
- NAT/firewall/proxy compatibility.

### Secure transport and naming

- TLS certificate/hostname trust;
- DNS resolution;
- DoH privacy/centralization implications;
- DNS caching/TTL/failover behavior;
- certificate renewal and time synchronization.

### Application communication

- HTTP request/response;
- gRPC typed RPC/streaming;
- WebSocket bidirectional live channel;
- WebRTC media/peer connectivity;
- MQTT publish/subscribe for constrained/device environments;
- SMTP/IMAP mail delivery/access;
- SFTP/SMB file-sharing semantics.

### Identity/access and operations

- OAuth/OIDC;
- SSH operator access;
- LDAP directory integration;
- WireGuard/IPsec private-network tunnels;
- NTP time synchronization;
- DHCP address/configuration;
- ICMPv6 as part of healthy IPv6 operation.

### Cross-cutting design questions

- who initiates the connection;
- whether communication is synchronous, live, durable or replayable;
- authentication/authorization after connection establishment;
- confidentiality/integrity;
- version compatibility;
- timeout/retry/idempotency;
- observability;
- deployment/network support;
- recovery when the protocol path fails.

## D. Diagram / visual explanation

Page `237` is a 4-column protocol matrix. Each tile names a role, protocol, typical port/transport hint, and icon. The visual is useful because it exposes **different responsibilities**, not because it tells an application to install every protocol.

For SquiFlow the visual should be read as:

```text
secure application transport
    -> HTTPS/TLS

identity
    -> OIDC/OAuth through ZITADEL

naming/routing
    -> DNS

operator recovery
    -> SSH/private network where deployment permits

time health
    -> NTP/host time synchronization

real typed RPC candidate
    -> gRPC when a real boundary earns it

live UX candidate
    -> WebSocket/SignalR/SSE when a real screen earns it

other protocol families
    -> only when a concrete integration/device/network requirement exists
```

## E. How it works — step by step

A representative SquiFlow Web/API request might involve several independent protocol layers:

1. The client resolves a SquiFlow/custom-domain hostname using DNS.
2. The client reaches the deployment edge/server over IP networking.
3. HTTPS/TLS establishes an authenticated encrypted transport channel.
4. HTTP carries the application request.
5. ZITADEL OIDC/OAuth establishes/refreshes the user authentication context where applicable.
6. SquiFlow derives TenantContext and performs OpenFGA/domain authorization — this is application authority, not a network-protocol function.
7. The API executes business logic and persistence.
8. Observability records protocol/dependency/business evidence without turning network metadata into authority.
9. If a live UI channel exists, WebSocket/SignalR can notify the client that state changed; durable state remains queryable if the live message is lost.
10. Operators use a separate private recovery path such as SSH/private networking when required; that path is not a tenant business API.

A Workstation sync path can use HTTPS ordinary HTTP or a future selected gRPC transport, but the authorization/idempotency/concurrency/local-authority model remains unchanged.

## F. Why it matters

SquiFlow already depends on several protocol families for **different reasons**. Making those reasons explicit prevents two opposite errors:

- treating all networking as “just HTTPS”; or
- installing every protocol shown in a systems diagram.

The source strengthens the need for a protocol/dependency inventory with owner, audience, trust, failure, recovery and compatibility rules.

## G. Trade-offs / limitations

### Benefits of explicit protocol choice

- each protocol can fit its workload rather than forcing one universal transport;
- security boundaries become clearer;
- network/customer-environment failures can be diagnosed separately from business failures;
- protocol-specific optimization can happen without rewriting business semantics.

### Costs/risks

- every added protocol expands deployment, firewall, certificate, compatibility, monitoring and troubleshooting surface;
- proxies/customer networks may support protocols differently;
- retry/connection behavior can amplify load;
- live channels can create false assumptions of durability;
- private protocols can accidentally become hidden authorization bypasses;
- credentials/tunnels can become long-lived high-impact secrets;
- protocol version skew can break old Workstations/integrations.

## H. Alternatives / comparisons — fit, not winner/loser

There is no global `HTTP vs gRPC vs MQTT vs WebSocket` decision.

```text
ordinary Web/business request-response
    -> HTTPS + HTTP/task API

Workstation sync / typed streaming RPC candidate
    -> gRPC if measured properties help
    -> ordinary HTTP remains the simpler baseline comparison

live UI notification
    -> polling / SSE / WebSocket/SignalR by UX directionality/freshness need

future device telemetry/control
    -> MQTT may become useful if constrained/device/pub-sub semantics appear

file exchange
    -> HTTPS/object storage API / SFTP / SMB depending exact partner/environment need

operator private access
    -> SSH over private network / VPN if needed

enterprise directory federation
    -> prefer OIDC/SAML through identity provider when appropriate;
       direct LDAP only if a concrete integration requires it
```

Several can coexist because they solve different boundaries.

## I. Real implementation considerations

For every actually used protocol, record:

- initiator and listener;
- public/private exposure;
- DNS name/port;
- TLS/authentication mechanism;
- application authorization that follows transport authentication;
- request/message size limits;
- timeout/deadline/retry owner;
- reconnect/backoff behavior;
- version compatibility;
- proxy/firewall/customer-network requirements;
- credential/certificate rotation;
- observability;
- degradation/fail-closed behavior;
- private recovery path.

### Implications for the Current Implementation

- **KEEP:** HTTPS/TLS is the external Web/Core/Admin/Workstation secure-transport envelope.
- **KEEP:** ZITADEL OIDC/OAuth is used for human authentication; it does not replace TenantContext/OpenFGA/domain authorization.
- **KEEP:** DNS supports routing/custom domains but never proves tenant authority.
- **KEEP:** time synchronization is operationally required for TLS/tokens/leases/schedules/diagnostics, while explicit versions/IDs remain correctness tools where wall-clock ambiguity is unsafe.
- **KEEP:** SSH/private-network access belongs to infrastructure recovery, not ordinary tenant functionality.
- **NEEDS MEASUREMENT:** gRPC remains a preferred Workstation/real-service-boundary candidate only where streaming/binary/generated-contract/high-frequency properties materially help.
- **LATER / REQUIREMENT TRIGGER:** WebSocket/SignalR/SSE only for real live UX needs; MQTT only for a real device/pub-sub workload; SFTP/SMB only for concrete file-exchange integrations; WireGuard/IPsec only for deployment/private-network requirements; direct LDAP only for a demonstrated enterprise-directory requirement; SMTP/IMAP only where mail delivery/access ownership requires them.
- **AVOID:** raw custom TCP/UDP application protocols without a requirement that higher-level protocols cannot satisfy economically.
- **AVOID:** treating VPN/TLS/firewall reachability as business authorization.

**What are we actually doing and why?** We currently specify a small protocol set because each one has a concrete responsibility: HTTPS for secure application transport, OIDC/OAuth for identity, DNS for routing, time/private access for operations, and possibly gRPC/live signaling at narrowly justified boundaries. The rest remain candidates, not rejected technologies.

**What would falsify/change this?** A concrete customer/device/integration or measured workload that requires another protocol property, or evidence that the selected protocol cannot operate reliably through the target edge/customer network.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. Which protocols in the source operate at transport, application, identity, naming, and operations layers?
2. What problem does QUIC solve differently from raw UDP?
3. Why are OAuth/OIDC conceptually different from TCP/TLS?

**Critical reasoning questions**
1. Which protocol families does SquiFlow actually require today, and what exact requirement justifies each?
2. Why would adding MQTT because it appears in the diagram be architecturally weak?
3. Why does a successful TLS handshake not prove a user may access a tenant resource?
4. What correctness properties of Workstation sync remain unchanged if HTTP is replaced by gRPC?
5. Which protocols are likely to encounter enterprise firewall/proxy restrictions and how would that affect a paying-customer deployment?

**Trade-off questions**
1. When can gRPC be materially better than ordinary HTTP for SquiFlow, and when is it unnecessary complexity?
2. When is WebSocket/SignalR better than polling or SSE, and when is it worse?
3. When would SFTP be a better integration than an HTTPS/object-storage workflow?
4. When does a VPN improve private operations, and what operational/secrets burden does it add?

**Failure / edge-case questions**
1. DNS resolves but TLS certificate validation fails. What should users and operators observe?
2. A customer proxy accepts HTTPS but breaks long-lived HTTP/2/gRPC streams. What fallback or support contract exists?
3. NTP drift invalidates tokens/certificates while business data is healthy. How is this diagnosed?
4. A live WebSocket is lost after a business commit. How does the client recover without losing truth?

**Implementation questions**
1. What ports/protocols are publicly reachable on the first production rack?
2. Which protocols need certificate/credential rotation automation?
3. How are protocol failures represented in stable telemetry/failure codes?
4. Which tests prove that direct/private protocol paths cannot bypass Core/Admin authorization?

**System design interview questions**
1. Design SquiFlow's protocol inventory for Web, Workstation sync, identity, live UX, operations and future device integrations.
2. Explain why one product can legitimately use HTTP, gRPC, WebSocket and async jobs without architecture inconsistency.

**Challenge**
A paying customer permits outbound HTTPS/443 but blocks arbitrary UDP and long-lived connections through a proxy. Design SquiFlow Web, Workstation sync, OIDC, live updates and operator support so correctness survives those constraints. Identify what would have to change if gRPC/HTTP3/WebSocket behavior is degraded.

---
