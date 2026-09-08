# ByteByteGo API Gateway, Service Communication, Data Sharing, REST, and Protocol Source Review — v0.0.15

**Status:** Source-backed follow-up review. Accepted architecture remains owned by the focused current documents.

## Reading limitation

The API Gateway, service-to-service communication, and service-data-sharing posts expose their introduction/article scope publicly and place the detailed body behind the continuation/paywall. The API-design, REST, and network-protocol newsletter sections expose enough text to review directly. This document records only what the accessible source supports. SquiFlow-specific conclusions beyond that text are architecture synthesis and are identified as such.

Sources reviewed sequentially:

1. API Gateways 101 — https://blog.bytebytego.com/p/api-gateways-101-the-core-of-modern
2. Service-to-Service Communication Patterns — https://blog.bytebytego.com/p/top-service-to-service-communication
3. Strategies to Share Data Between Services — https://blog.bytebytego.com/p/top-strategies-to-share-data-between
4. How to Design Good APIs — https://blog.bytebytego.com/p/ep189-how-to-design-good-apis
5. What is a REST API? — https://blog.bytebytego.com/p/ep192-what-is-a-rest-api
6. Common Network Protocols Every Engineer Should Know — https://blog.bytebytego.com/p/ep195-common-network-protocols-every

---

## 1. API Gateway

The source describes an API gateway as the client-facing entry point in a service-based system so clients do not need to know every internal service location/authentication/request format. The public introduction also lists gateway capabilities such as routing, authentication/authorization, API version management, protocol/payload transformation, and analytics.

### SquiFlow decision

**ADAPT the edge-gateway role; do not turn the gateway into SquiFlow business authority.**

SquiFlow currently has only a few deliberate server executables. The deployment edge may provide:
- TLS termination;
- hostname/custom-domain routing;
- route separation between tenant/business and platform/admin surfaces;
- request-size and coarse abuse/WAF controls where the selected edge supports them;
- coarse rate limiting;
- transport/protocol negotiation and observability metadata where useful.

It must not own:
- TenantContext derivation as final authority;
- OpenFGA business/platform authorization;
- workflow/domain decisions;
- payment/stock/credit authority;
- idempotency/concurrency semantics;
- Admin API business/control logic.

The edge may route both backends but must preserve direct ownership:

```text
edge
├── tenant/business → Core API
└── platform/admin  → Admin API
```

not:

```text
edge → Core API → Admin API
```

A heavyweight API-management product is not required merely because the pattern exists. Start with the edge/reverse-proxy capability that the deployment actually needs and measure its resource/operational cost on the owned rack.

**Audit result:** KEEP an edge-gateway/reverse-proxy capability as a deployment concern; reject gateway-as-business-authority and reject a gateway product chosen before requirements.

---

## 2. Service-to-service communication

The source states that synchronous/blocking and asynchronous/non-blocking communication have different effects on latency, scaling, recovery, and consistency.

### SquiFlow decision

**Use network communication only across real runtime boundaries. Keep module-to-module work in-process inside the modular monolith.**

Current rule:

```text
inside one application host/module composition
→ direct in-process call

client needs immediate authoritative result
→ synchronous HTTP to owning backend

long-running/after-commit consequence
→ durable Worker/job/outbox path

committed fact with multiple real consumers
→ durable fan-out/pub-sub only when proven
```

Do not create HTTP/gRPC calls between modules merely to imitate microservices.

Avoid synchronous internal call chains such as:

```text
client → Core API → Service A → Service B → Service C
```

when the same business operation can remain one in-process/transactional application flow. Every additional network hop adds timeout, retry, partial-failure, tracing, and versioning obligations.

`Admin API` remains independently callable and does not normally call `Core API` for platform commands. `Worker` consumes durable work; it is not a generic RPC hop inserted between ordinary commands and the database.

If a future component is extracted into a true independent service, select synchronous HTTP/gRPC or asynchronous messaging from the required semantics, latency, failure, ordering, and consistency behavior rather than by fashion.

**Audit result:** KEEP direct in-process modular-monolith communication; use synchronous/asynchronous network patterns only at justified process/service boundaries.

---

## 3. Sharing data between services

The source introduces service data ownership and asks whether services should share a data source or exchange data through APIs/messages. Its public scope emphasizes the distinction between sharing one database and sharing data while preserving service independence.

### SquiFlow decision

SquiFlow must not import a database-per-service rule into a system that is intentionally a modular monolith with several deployment hosts.

Core API, Admin API, and Worker can legitimately use the same authoritative central database when they are hosting/composing the same SquiFlow business modules and preserving the same transaction/invariant rules.

But shared database access must not become unstructured cross-host table ownership:
- domain/application ownership remains by module/capability;
- hosts reuse reviewed shared module/persistence code where practical;
- Core API and Admin API must enforce the same invariant when both legitimately touch the same state;
- one host must not bypass another module's rules with ad-hoc SQL merely because the table is reachable;
- read-only/reporting access still respects tenant/platform authorization boundaries.

If a future capability becomes a genuinely independent service with its own deployment/data lifecycle, its authoritative data ownership must become explicit and other services should exchange data through a stable contract/event/read model rather than directly modifying its internal tables.

**Audit result:** KEEP shared authoritative DB for the modular monolith; make data ownership explicit; database-per-service applies only after a real service extraction earns it.

---

## 4. Designing good APIs

The visible ByteByteGo section emphasizes idempotency, versioning, noun-oriented resource naming, authentication/HTTPS, and pagination.

### SquiFlow decision

Most of this matches the current API contract, with two important refinements.

First, the source presents POST/PATCH as non-idempotent in the ordinary HTTP-method sense. SquiFlow intentionally adds **semantic idempotency** to retryable POST business commands using caller-provided idempotency keys. Therefore:

```text
POST
≠ automatically safe to retry

POST + SquiFlow semantic idempotency contract
→ retry-safe for the same intended business operation
```

Second, noun-oriented resources are the default for ordinary resource APIs, but material domain transitions may use explicit command/action subresources because business intent matters more than REST cosmetics:

```text
POST /api/orders
POST /api/quotes/{id}/approval
POST /api/payments/{id}/refunds
```

Do not replace `approval` or `refund` with a vague generic entity update merely to avoid a semantic command endpoint.

Versioning remains mandatory because Workstations can skip releases and backends can roll independently. Exact URI/header/media-type version mechanics remain an implementation decision until Phase work proves the needed compatibility model.

All external surfaces use TLS/HTTPS in production. Pagination is server-bounded; offset is not mandatory when cursor/keyset pagination is more correct or efficient.

**Audit result:** KEEP predictable/versioned/resource-oriented APIs, but preserve semantic command endpoints and SquiFlow idempotency rather than following simplistic REST rules mechanically.

---

## 5. REST constraints

The source lists the classic REST constraints: client-server, stateless, uniform interface, cacheable responses, layered system, and optional code-on-demand.

### SquiFlow decision

SquiFlow uses **REST/task-oriented HTTP as a pragmatic application API style**. It does not claim every SquiFlow surface is a strict REST implementation.

Reasons:
- tenant Web may use a server-backed browser session and potentially Blazor Interactive Server circuit state;
- SquiFlow uses explicit semantic command subresources for material business transitions;
- code-on-demand is not part of the business API contract;
- some responses are intentionally non-cacheable because authority/freshness matters.

The useful REST properties still apply where they help:
- client/server separation;
- predictable resources and HTTP semantics;
- explicit cacheability;
- layered edge/backend topology;
- stateless **business correctness** in backend process memory: committed truth is not stored only in one request-process memory instance.

Do not chase formal REST purity if it weakens domain clarity, security, idempotency, or client compatibility.

**Audit result:** KEEP REST-inspired/task-oriented HTTP; explicitly reject false claims of strict REST compliance where the real session/runtime model differs.

---

## 6. Network protocols

The visible ByteByteGo section identifies TCP/UDP/QUIC, HTTP/TLS/DNS, SSH/file-transfer protocols, WebSocket/WebRTC/MQTT, OAuth/OpenID, backend infrastructure protocols, email protocols, and VPN protocols as different tools for different network responsibilities.

### SquiFlow decision

Protocol choice follows the responsibility instead of creating one universal transport layer.

Current protocol boundaries:
- **HTTPS/TLS:** all normal external Web/API/Admin/Workstation sync traffic in production;
- **OIDC/OAuth over HTTPS:** ZITADEL authentication/session flows;
- **HTTP version (1.1/2/3):** negotiated by client/edge/server where supported; application semantics must not depend on a particular transport version;
- **DNS:** routing dependency, especially for custom domains and provider endpoints; DNS/hostname input is never itself tenant authority;
- **WebSocket/SignalR where used:** live UI/signal/wakeup only; durable business/sync truth remains DB/outbox/state records;
- **SSH/private network access:** infrastructure recovery/operations only, never a normal tenant business channel;
- **time synchronization:** operationally important because TLS/token validity, leases, schedules, and diagnostics depend on reasonable clocks; correctness must still avoid unsafe wall-clock assumptions where monotonic/version evidence is required.

Do not add MQTT, WebRTC, FTP/SFTP, raw TCP/UDP, or gRPC merely because they are common protocols. A future requirement must identify the actual latency, streaming, device, transport, compatibility, or deployment problem they solve.

**Audit result:** KEEP HTTPS/OIDC and narrow protocol use; protocol diversity is requirement-driven, not a feature checklist.

---

## Questions applied to SquiFlow

This review now follows the project-wide question rule in `docs/review/ARCHITECTURE_QUESTION_LEDGER.md`.

### Source-explicit questions

The public **data-sharing** article asks, in substance:
- should multiple services connect to the same database;
- should they exchange data through APIs/messages instead;
- how should consistency, performance, fault tolerance and loose coupling be balanced?

Applied answer for SquiFlow: Core API/Admin API/Worker may share the modular-monolith authoritative DB, but module/domain ownership still controls mutation rules. API/event-mediated ownership becomes the default only after a capability is genuinely extracted as an independent service. Consistency is selected per invariant, and Phase 6/10 verify host-failure behavior.

The public **API-design** newsletter asks readers to identify the most common API design mistake and how to fix it.

Applied answer for SquiFlow: the highest-risk mistake is a generic CRUD/transport endpoint that hides business intent, authorization, tenant scope, idempotency, concurrency, resource limits, and version compatibility. The fix is explicit task/resource contracts plus the API implementation gate.

The public **REST** newsletter asks which REST constraint is most often overlooked.

Applied answer for SquiFlow: do not choose one slogan. Ask all six constraints against the actual surface. Two especially easy SquiFlow mistakes are (1) confusing process statelessness with a system that has no sessions/circuits, and (2) failing to classify cacheability so stale protected state becomes authority.

The public **network-protocol** newsletter asks which protocol failure would be most disruptive.

Applied answer for SquiFlow: convert that into a failure matrix. DNS/TLS failures block online connectivity; ZITADEL failure affects login/step-up; OpenFGA failure must never become allow; time skew affects tokens/TLS/leases/schedules; WebSocket failure must lose no business truth; SSH/private-access failure affects recovery rather than tenant business correctness. The exact outage/degraded behaviors promoted from this question now live in `OPEN_DECISIONS.md`.

### SquiFlow-derived questions from the paid/public-introduction articles

For **API Gateway**:
- Do we need a gateway product or just a simple reverse proxy/edge?
- Which checks belong at the edge versus Core/Admin API?
- Is the edge a single point of failure?
- Can platform recovery still happen if the public edge is unavailable?
- Does routing preserve Admin API independence from Core API?

For **service-to-service communication**:
- Is a network boundary actually required, or are both capabilities in-process modules?
- Does the caller require an immediate authoritative result?
- Can the consequence run after commit through durable work?
- What happens when the peer times out after possibly performing an effect?
- Who owns retries, and how many synchronous hops are on the user path?
- Is ordering/fan-out/replay actually required?

These are synthesis questions derived from the source trade-offs, not quoted source questions.

### Needs discovered by asking the questions

This question pass uncovered or promoted several concrete decisions that were easy to miss in a pattern-only review:
- exact API/sync version-negotiation mechanism before skipped Workstation releases exist;
- explicit edge failure/SPOF and private recovery-path test;
- exact ZITADEL-outage behavior for existing sessions and step-up;
- exact OpenFGA-outage/fail-closed or narrowly degraded behavior by operation risk;
- endpoint-level cacheability classification rather than a vague `we use CDN/cache` statement;
- DNS/TLS renewal/failure recovery;
- acceptable clock-skew monitoring/tolerance;
- WebSocket/SignalR, if ever used, remaining non-durable signal only.

These are now promoted into `docs/decisions/OPEN_DECISIONS.md` and the question ledger rather than remaining hidden in this source review.

---

## Combined result for v0.0.15

KEEP/STRENGTHEN:
- edge reverse proxy/API-gateway capability as north-south routing/security infrastructure;
- direct Core API and direct Admin API ownership behind the edge;
- in-process communication inside the modular monolith;
- explicit synchronous versus durable asynchronous semantics at real process boundaries;
- shared central DB for SquiFlow hosts with explicit module/data ownership and shared invariants;
- semantic idempotency for retryable POST commands;
- REST/task-oriented HTTP without false strict-REST claims;
- HTTPS/TLS/OIDC as current external protocol baseline;
- durable truth outside transient WebSocket/circuit/process memory;
- question-driven source review that promotes applicable unknowns into owned OPEN decisions/gates.

DEFER/REJECT AS BASELINE:
- gateway as authorization/domain authority;
- API-management platform selected before need;
- HTTP/gRPC between ordinary modules;
- microservice/database-per-service rules applied to the modular monolith;
- long synchronous service-call chains;
- strict REST purity;
- MQTT/WebRTC/FTP/SFTP/raw TCP/UDP/gRPC without a concrete workload.

The governing rule remains disciplined completeness: a new network/service/data-sharing boundary is justified only when its responsibility and failure model are concrete, and once accepted it must be implemented fully rather than weakened for minimalism.
