# ByteByteGo Archive Sequential Review 001–015 — v0.0.15

**Status:** Source-backed sequential review of the uploaded ByteByteGo archive. Accepted architecture remains owned by the focused current documents. This review applies each article's explicit question/trade-off to SquiFlow instead of treating article patterns as a feature checklist.

Reviewed in order:

1. How does gRPC work?
2. Docker vs. Kubernetes. Which one should we use?
3. A Cheatsheet on Infrastructure as Code Landscape
4. A Crash Course on Architectural Scalability
5. Cookies Vs Sessions Vs JWT Vs PASETO
6. The Ultimate API Learning Roadmap
7. 10 Essential Components of a Production Web Application
8. Session, Cookie, JWT, Token, SSO, and OAuth 2.0 Explained in One Diagram
9. A Cheatsheet on Database Performance
10. JWT 101: Key to Stateless Authentication
11. 12 Algorithms for System Design Interviews
12. PostgreSQL 101: The Everything Database
13. Top 12 Tips for API Security
14. 24 Good Resources to Learn Software Architecture in 2025
15. SQS vs SNS vs EventBridge vs Kinesis

The source is the uploaded `ByteByteGo_Selected_Archive_plus_Web_Content_Images_URLs.pdf`, archive-generated pages 5–34.

---

## 1. gRPC

The article explains remote procedure calls as calls that can look local while actually crossing a network boundary, with gRPC using generated stubs, binary encoding and HTTP/2. Its closing question asks what limitations matter in real projects.

### Questions applied to SquiFlow

**Do we currently need gRPC?**

**Answer:** No baseline requirement. Ordinary business modules are in-process. Core API/Admin API use task-oriented HTTP, and Workstation sync can begin with an explicit HTTP contract. gRPC is reconsidered only if the real sync/provider workload proves a material need for streaming, binary payload efficiency, strongly generated contracts, or another capability that outweighs deployment/debugging/version-compatibility complexity.

**What failure would a premature gRPC boundary create?**

It would convert local module calls into remote calls with timeouts, retry, partial failure, version skew, authentication and observability obligations without a demonstrated benefit.

**Decision:** DEFER gRPC. Do not infer a protocol choice from generic benchmark claims.

---

## 2. Docker vs Kubernetes

The article distinguishes single-host container execution from cluster orchestration and explicitly asks what challenges justify switching to Kubernetes.

### Questions applied to SquiFlow

**What challenge currently requires Kubernetes?**

**Answer:** None. SquiFlow starts on a small owned rack with only a few intentional runtime executables. Kubernetes would add a control plane, scheduling/network/storage abstractions and operational overhead before the deployment has a cluster-orchestration problem.

**What would justify revisiting Kubernetes?**

Repeated multi-node orchestration pain such as manual placement/reconciliation of replicas, rollout/rollback coordination, service discovery, automated failover/replacement, or scaling operations becoming an operational bottleneck that simpler deployment automation cannot handle reliably.

**Decision:** Containerization may be used for packaging/isolation when useful; Kubernetes is not baseline and is trigger-driven.

---

## 3. Infrastructure as Code

The article describes containerization, orchestration, IaC and GitOps as ways to make infrastructure repeatable/versioned/testable. It asks whether infrastructure is managed as code.

### Questions applied to SquiFlow

**Which SquiFlow infrastructure must be reproducible?**

At minimum, the paying-customer single-node deployment should be rebuildable from version-controlled deployment definitions/runbooks rather than undocumented shell history. This includes the intended service layout, edge routing, service start/restart behavior, database provisioning/migration procedure, backup scheduling/target configuration, observability exporters and resource limits that belong to deployment infrastructure.

**Does IaC mean administrators should edit YAML for normal product settings?**

No. Application/tenant/platform configuration remains a first-class Web/Admin API experience where applicable. IaC is for infrastructure/deployment reproduction, not the normal business administration interface.

**Decision:** KEEP reproducible deployment/infrastructure-as-code as a production requirement. Exact tooling remains OPEN; do not force Terraform/Kubernetes/Flux merely because the category exists.

---

## 4. Architectural scalability

The article emphasizes that no system is infinitely scalable and identifies centralized components, high-latency components and tight coupling as bottlenecks. It lists load balancing, caching, event-driven processing and sharding as possible techniques.

### Questions applied to SquiFlow

**What does “scalable” mean for SquiFlow?**

Not infinite scale. Each deployment profile needs a measured workload/capacity envelope and a known next move when a specific bottleneck is reached.

**What is SquiFlow's first likely bottleneck?**

Unknown until measurement. Candidates include DB connection/query/WAL pressure, CPU-heavy documents, object-transfer bandwidth, Worker backlog, external identity/authorization latency, or one physical node. The architecture must diagnose the actual bottleneck rather than pre-install every scaling pattern.

**Do centralized components automatically need removal?**

No. The central authoritative database is intentionally centralized for correctness in the baseline. It becomes a scaling problem only when measured capacity/recovery/SLO evidence says so.

**Decision:** KEEP finite capacity profiles and evidence-triggered scaling. Sharding, replicas, extra nodes or event-driven decomposition are not baseline merely because they are standard scaling techniques.

---

## 5. Cookies, Sessions, JWT and PASETO

The article compares server-side session state, JWT-style self-contained tokens and PASETO-style safer cryptographic defaults.

### Questions applied to SquiFlow

**Which authentication approach should SquiFlow implement itself?**

None as a custom identity platform. ZITADEL is selected. SquiFlow consumes standards-based OIDC/OAuth and chooses a suitable Web session pattern around that provider; Workstation uses system-browser Authorization Code + PKCE.

**Does a JWT/PASETO/session establish SquiFlow business authority?**

No. Authentication identifies the actor/session. Current application authorization is OpenFGA + server policy/resource/domain checks, and tenant isolation is separate again.

**Do we need PASETO because the article lists it?**

No current requirement. Token format/crypto choices at the identity layer should follow ZITADEL/OIDC capabilities rather than creating a second token system.

**Decision:** KEEP ZITADEL/OIDC boundary; exact Web session/cookie topology remains Phase-1 OPEN.

---

## 6. Ultimate API learning roadmap

The article covers API styles, authentication, documentation, pagination, idempotency, versioning, performance, gateways and integration patterns, then asks what is missing.

### Questions applied to SquiFlow

For SquiFlow, the roadmap is incomplete unless every production API also answers:

- tenant/resource authorization;
- property allowlists;
- semantic idempotency scope;
- optimistic concurrency/expected version;
- retry/failure classification;
- compatibility with older Workstation protocols;
- long-running operation status;
- request/page/resource/rate limits;
- audit/correlation;
- consistency/freshness of returned derived data;
- owning backend: Core API or Admin API.

**Decision:** Existing SquiFlow API gate remains stronger than a technology checklist. No new API framework is required.

---

## 7. Production Web application components

The article lists CI/CD, DNS, load balancer/reverse proxy, CDN, APIs, DB/cache, workers, search, monitoring and alerts and asks what else a production Web application needs.

### Questions applied to SquiFlow

**Which listed components are actually baseline?**

CI/CD, DNS/TLS/edge routing, Web/API hosts, central DB, Worker when durable async work exists, observability and alerting are real. CDN/static caching is useful where safe. A generic distributed cache or separate full-text search service is not baseline without measured need.

**What SquiFlow-specific production components are missing from the generic list?**

Recovery/backup restore, ZITADEL identity, OpenFGA authorization, tenant isolation, object storage, separate Admin API control plane, secrets/configuration, rate/admission controls, certificate/time dependency health and private break-glass recovery.

**Decision:** Treat production-component diagrams as completeness prompts, not a mandatory shopping list.

---

## 8. Session, Cookie, JWT, Token, SSO and OAuth 2.0

The article summarizes several identity/session approaches and asks about QR-code login.

### Questions applied to SquiFlow

**Does SquiFlow need QR-code login now?**

No current user requirement. Do not add another login flow merely because it is popular.

**What matters for SSO?**

ZITADEL owns SSO/federation capability. SquiFlow must correctly map external `(issuer, subject)` identity to SquiFlow membership/tenant context and must not treat an identity-provider session as application authorization.

**Decision:** No QR-login work. Keep SSO/federation inside the selected identity boundary.

---

## 9. Database performance

The article says performance depends on workload type, item size/type, dataset size, concurrency, consistency, HA and geographic distribution before listing indexing, sharding/partitioning, denormalization, replication and locking.

### Questions applied to SquiFlow

**What workload are we optimizing before selecting/tuning the DB?**

Phase 3 must characterize the actual slice instead of testing only a query count. Record at least:

- read/write/delete mix;
- representative row/document sizes;
- tenant count/data skew;
- normal and reconnect-burst concurrency;
- sync/import write bursts;
- consistency requirements;
- query cardinalities and hot paths;
- initial HA/geographic assumptions.

**Which performance technique should be used first?**

Whichever measured bottleneck justifies it. Indexing/locking/concurrency are likely early tools. Replication, denormalization and sharding require separate evidence and correctness/freshness contracts.

**Decision:** STRENGTHEN Phase-3 DB POC with explicit workload characterization before tuning.

---

## 10. JWT 101

The article explains JWT header/payload/signature and symmetric/asymmetric signing and asks whether JWTs are used for authentication.

### Questions applied to SquiFlow

**Does SquiFlow validate arbitrary JWTs from clients?**

No. It validates tokens/sessions only according to the configured trusted ZITADEL issuer/application contract. Issuer, audience, signature, time claims and OIDC semantics are not replaced by “it looks like a signed JWT.”

**Does the JWT payload become fresh authorization?**

No. OpenFGA/current server state remains authorization authority.

**Decision:** No custom JWT authentication subsystem.

---

## 11. Algorithms for system design

The article lists Bloom filters, consistent hashing, Merkle trees, Raft, operational transformation, leaky bucket, rsync and others and asks what algorithms belong in a design toolkit.

### Questions applied to SquiFlow

**Which of these algorithms are current product requirements?**

None should be adopted merely because they are useful at scale.

- Raft/consistent hashing/Merkle-tree replica repair are not baseline because SquiFlow is not building its own distributed database/consensus layer.
- Operational Transformation is not baseline because SquiFlow has no real-time multi-author collaborative editor requirement.
- `rsync` is not the Workstation business-sync model; sync is semantic, authenticated, idempotent and conflict-aware.
- Leaky/token-bucket style algorithms may be implementation choices inside framework/runtime rate limiting, but SquiFlow does not need a custom rate-limiter service now.
- Bloom filters become relevant only if an actual high-volume negative-lookup problem proves the memory/false-positive trade-off worthwhile.

**Decision:** Algorithm selection remains problem-driven.

---

## 12. PostgreSQL architecture

The article describes PostgreSQL's per-client processes, Postmaster, shared memory, WAL/background writer/checkpoint/autovacuum/archiver/replication processes and physical file classes, then asks what else should be understood.

### Questions applied to SquiFlow

**What PostgreSQL internals matter on the low-resource rack if PostgreSQL wins Phase 3?**

The POC must not stop at SQL correctness. Measure/observe:

- connection-pool size versus PostgreSQL backend process/memory cost;
- WAL growth during reconnect/import bursts;
- checkpoint latency/I/O spikes;
- autovacuum behavior under update/delete churn;
- temp/sort spill behavior;
- restart/crash recovery time;
- archive/log growth and disk-full behavior;
- migration/backup interaction with the finite disk envelope.

**Decision:** STRENGTHEN PostgreSQL proof; this still does not preselect PostgreSQL before Phase 3.

---

## 13. API security tips

The article lists HTTPS, OAuth2, WebAuthn, API keys, authorization, rate limiting, versioning, allowlisting, OWASP API risks, gateway, error handling and input validation.

### Questions applied to SquiFlow

**Which identity mechanisms belong in the application?**

ZITADEL owns primary authentication/MFA/passkey/WebAuthn capability when enabled. SquiFlow should not independently implement WebAuthn or password authentication.

**Do API keys belong to normal tenant users?**

No baseline requirement. If a future partner/integration API needs machine credentials, that becomes its own scoped authentication/rotation/revocation/rate-limit design; it is not a substitute for user OIDC sessions.

**Does an API gateway satisfy authorization?**

No. Core/Admin API repeat their own authorization/resource/domain checks.

**Decision:** Existing OWASP/API security model remains; do not duplicate identity/security features already owned by ZITADEL/OpenFGA/backend policies.

---

## 14. Architecture learning resources

The article recommends books, engineering blogs, architecture resources and foundational whitepapers and asks what other resources to use.

### Questions applied to SquiFlow

**What evidence quality should be required before a source changes architecture?**

ByteByteGo diagrams/newsletters are useful for surfacing patterns and questions. When a decision depends on exact security/database/protocol/provider semantics, prefer current primary specifications, official provider documentation, standards, and foundational papers. Cross-check secondary summaries before locking a product-level invariant.

**Decision:** KEEP source hierarchy as an architecture-review discipline: secondary sources generate questions; primary evidence closes high-impact technical claims when available.

---

## 15. SQS vs SNS vs EventBridge vs Kinesis

The article distinguishes queue, publish/subscribe, event-bus routing and streaming workloads.

### Questions applied to SquiFlow

**Which semantics does SquiFlow need today?**

- one durable work item → DB-backed job/queue semantics;
- several independent consequences of one committed fact → multiple durable outbox deliveries/pub-sub semantics when actually needed;
- event routing between many independent producers/consumers → event bus only when topology earns it;
- high-volume replayable stream/independent offsets → stream infrastructure only when a real workload requires it.

**Do AWS product names determine the architecture?**

No. SquiFlow currently runs on owned infrastructure and provider choices remain independent. The article is useful because it distinguishes semantics, not because SquiFlow should adopt SQS/SNS/EventBridge/Kinesis.

**Decision:** KEEP existing queue/pub-sub/event-stream selection matrix; no Kafka/RabbitMQ/cloud event-bus baseline.

---

# New/strengthened requirements from articles 001–015

1. **Reproducible deployment-as-code is a production requirement**, while normal application administration remains Web/Admin-API driven.
2. **Kubernetes has explicit revisit triggers rather than remaining a vague future possibility.**
3. **Scalability is capacity-profile/bottleneck driven**, not “add distributed patterns now.”
4. **Phase-3 DB selection starts with workload characterization**, including reconnect-burst behavior and data-size/concurrency/consistency assumptions.
5. **If PostgreSQL wins, its operational proof includes connection-process cost, WAL/checkpoints/autovacuum/temp/disk behavior**, not only SQL/RLS correctness.
6. **gRPC remains non-baseline** and can only be introduced for a measured transport/contract need.
7. **Architecture review uses a source hierarchy**: secondary articles surface questions; exact high-impact claims are closed against primary docs/specifications when available.
8. **Generic production-component and algorithm lists are not backlogs.** Every component/algorithm must answer a concrete SquiFlow failure/performance/operational need.

# Phase mapping

- **Phase 0:** keep `deploy/` real and begin a reproducible deployment profile; do not add Kubernetes/gRPC infrastructure.
- **Phase 1:** close the Web session model around ZITADEL; no custom JWT/PASETO/WebAuthn subsystem.
- **Phase 3:** record real workload profile and prove DB operational behavior under current + projected/burst workload.
- **Phase 6:** preserve message semantics; choose the simplest durable mechanism for the first real Worker workload.
- **Phase 8:** classify endpoint cache/rate/security behavior and verify edge/backend security remains layered.
- **Phase 10:** prove the deployment can be recreated from versioned deployment definitions/runbooks and document the measured next scaling move for each first-order bottleneck.
