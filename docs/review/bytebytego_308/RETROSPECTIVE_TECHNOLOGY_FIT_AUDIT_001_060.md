# Retrospective Technology-Fit Audit — Archive Entries 001-060

**Status:** authoritative retrospective interpretation for the exhaustive ByteByteGo study through archive entry `060`.

**Reason for this audit:** the earlier studies were technically cautious, but some shorthand such as `AVOID`, `DEFER`, `not baseline`, or `X remains baseline` can be read as a winner/loser technology judgment. The user has explicitly required a different architectural standard: understand **what each technology is good at, where SquiFlow uses it or could use it, and why**, then decide per boundary. A comparison article must not by itself become an adoption/rejection decision.

This file therefore retroactively reviews entries `001-060` under `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md`.

## Interpretation rule for older study wording

For entries `001-060`, earlier labels mean only the following unless a concrete security/correctness reason says otherwise:

```text
KEEP
    -> current SquiFlow mechanism remains suitable for the named surface

LATER / DEFER / NOT BASELINE
    -> no current requirement at the named surface; technology remains available for another surface or future trigger

AVOID / REJECT AS BASELINE
    -> do not apply this technology/pattern universally or at the stated current boundary
    -> NOT a statement that the technology is bad in general

NEEDS MEASUREMENT
    -> candidate whose value depends on representative SquiFlow evidence
```

No earlier comparison article is allowed to mean `A wins globally, B loses globally`.

The retrospective audit intentionally does **not** rewrite source-derived sections or pretend the archive said something it did not say. It changes only the **SquiFlow architectural interpretation** of those exposures. Where the archive itself is comparative, both sides remain preserved. Where our older study wording was too categorical, this file narrows that wording to the actual SquiFlow boundary/use case.

## Current-use rule added retroactively

For every technology/pattern below, the architectural record must answer both questions:

1. **What/where are we using now?**
2. **Why does it fit that exact SquiFlow surface?**

Examples of current/accepted reasoning:

```text
in-process calls
    -> used inside the modular monolith
    -> because there is no network boundary and direct calls avoid artificial distributed failure/versioning cost

REST/task-oriented HTTP
    -> used/planned for many explicit command/resource and external-facing surfaces
    -> because explicit server-owned business intent, status/error semantics, idempotency contracts and broad HTTP tooling fit those operations

ZITADEL/OIDC
    -> used for interactive identity across Web/Admin/Workstation
    -> because standards-based SSO/MFA/account/session capability avoids SquiFlow owning password/identity infrastructure

OpenFGA
    -> used for current application/resource authorization
    -> because tenant-defined relationships/permissions require current server-side authorization separate from identity token claims

command/query responsibility separation
    -> used in the modular monolith
    -> because mutation intent and read shaping have different responsibilities without requiring separate databases/services

normalized relational authority
    -> current core data direction
    -> because business identities/constraints/transactions need explicit authoritative integrity

transactional outbox + Worker direction
    -> used/planned for after-commit durable consequences
    -> because work must survive caller disconnect/restart without extending synchronous failure chains
```

This “what + where + why” requirement applies equally to technologies we may add later: GraphQL, gRPC, RabbitMQ, Kafka, Redis/Memcached, Kubernetes, Event Sourcing, data-lake tooling, etc. A future adoption is incomplete unless the specific SquiFlow surface and benefit are named.

---

## 001-010 retrospective fit audit

| Entry | Topic | Corrected SquiFlow fit interpretation |
|---|---|---|
| 001 | gRPC | **PLANNED CANDIDATE for specific boundaries.** In-process calls remain best inside one host because there is no network boundary. gRPC is valuable for Workstation sync or future service RPC when streaming, generated contracts, compact payloads, or high-frequency RPC materially help. HTTP remains useful for browser/external/resource APIs; durable async remains better for long-running consequences. |
| 002 | Docker vs Kubernetes | **COMPLEMENTARY, not competitors.** Containers can package/isolate SquiFlow even on one host. Kubernetes becomes useful for recurring multi-node placement, reconciliation, service discovery, rollout/failover, or scaling operations. SquiFlow may use containers without Kubernetes, and may later use both. |
| 003 | IaC landscape | **MULTIPLE TOOLS FOR DIFFERENT CONTROL LAYERS.** Versioned deployment/rebuild is required. Direct scripts/system services, containers, Ansible, Terraform, and GitOps solve different provisioning/configuration/reconciliation problems. Exact tooling should follow the actual deployment topology. |
| 004 | Architectural scalability | **TECHNIQUE SELECTION BY BOTTLENECK.** Load balancing, caching, async processing, scale-up, partitioning, replicas, and sharding can all be correct in different places. SquiFlow does not prefer one globally; it measures the first-order constraint and applies the narrowest effective technique. |
| 005 | Cookies / sessions / JWT / PASETO | **LAYERED OPTIONS, not one-of-four winner.** Web may use secure cookies plus server-managed session state; ZITADEL/OIDC may use JWT-like tokens internally; PASETO is a valid token design for systems that own an independent token protocol but is not automatically a better fit for SquiFlow's OIDC ecosystem. OpenFGA remains current application authorization regardless of token/session form. |
| 006 | API learning roadmap | **POLYGLOT API STYLE IS ALLOWED.** REST/task HTTP, GraphQL, gRPC, WebSocket/SignalR, webhooks, polling, and durable async may coexist because they solve different client/transport/timeliness problems. Selection is per surface. |
| 007 | Production Web components | **CAPABILITY MAP, not deployment shopping list.** Edge, DB, Worker, cache, search, CDN, observability, etc. are introduced only where their capability is actually required. One physical/runtime component may own several roles early. |
| 008 | Session / Cookie / JWT / Token / SSO / OAuth | **STACKED IDENTITY MECHANISMS.** ZITADEL/OIDC/SSO provides identity; Web can establish an application session via cookie; Workstation uses Authorization Code + PKCE; tokens carry protocol state/claims; OpenFGA separately decides application authorization. These mechanisms are complementary layers. |
| 009 | Database performance | **MIXED OPTIMIZATION TOOLBOX.** Indexes, normalization, denormalized projections, partitioning, replication, connection pooling, and lock/concurrency strategies solve different bottlenecks. SquiFlow should use several if measurements justify them rather than treating one as the preferred answer. |
| 010 | JWT | **USED AS PROVIDER/PROTOCOL MECHANISM WHERE APPLICABLE, not application authority.** JWT is valuable for portable signed claims in OAuth/OIDC ecosystems. SquiFlow can consume provider-issued tokens while still using sessions, OpenFGA, and server state. The rejected item is only a redundant custom SquiFlow JWT authority, not JWT technology itself. |

## 011-020 retrospective fit audit

| Entry | Topic | Corrected SquiFlow fit interpretation |
|---|---|---|
| 011 | System-design algorithms/data structures | **PROBLEM-SPECIFIC TOOLBOX.** Bloom Filters, HyperLogLog, consistent hashing, Merkle trees, Raft, etc. are not rejected; they become valuable only for matching problems. Approximate structures are safe for hints/analytics but not exact money/stock/permission authority. |
| 012 | PostgreSQL | **STRONG CENTRAL RELATIONAL CANDIDATE, not universal database.** PostgreSQL fits transactional multi-tenant central authority well if Phase-3 operational proof succeeds. SQLite/libSQL can fit Workstation local state better; specialized search/KV/analytics stores may fit separate workloads later. |
| 013 | API security | **DEFENSES ARE COMPLEMENTARY.** TLS, OIDC, WebAuthn/passkeys, API keys, authorization, rate limiting, validation, gateway controls, and safe errors address different threats. SquiFlow uses the subset appropriate to each user/service/integration surface. |
| 014 | Architecture resources | **EVIDENCE METHOD, not technology choice.** Use secondary sources for discovery, primary sources for exact semantics, and SquiFlow POCs for workload-specific claims. |
| 015 | SQS / SNS / EventBridge / Kinesis | **SEMANTIC FIT MATRIX.** Queue, pub/sub, event bus, and stream are all valid but solve different delivery models. SquiFlow's first durable Worker task likely fits a queue/job model; several independent reactions may fit fan-out; replay/offset-heavy workloads may fit a stream later. AWS branding is incidental to the semantics. |
| 016 | API performance techniques | **COMBINABLE OPTIMIZATIONS.** Pagination, bounded async telemetry, caching, compression, and pooling can coexist. Each is adopted only when its resource trade-off improves a measured SquiFlow path without weakening correctness. |
| 017 | HTTP/1.1 / HTTP/2 / HTTP/3 | **NEGOTIABLE TRANSPORT CAPABILITIES.** SquiFlow need not choose a single version as architecture. Edge/client/runtime can negotiate supported versions; HTTP/3 may help suitable networks while fallback preserves the same API semantics. |
| 018 | URL structure | **NO COMPETING TECHNOLOGY DECISION.** The important fit issue is how route/host/query/fragment data is trusted and validated. |
| 019 | Key-value store comparison | **SPECIALIZED STORE OPTIONS REMAIN OPEN.** Redis, document stores, distributed KV, graph stores, coordination stores, etc. should be selected only for a workload whose data/consistency/operations model fits them. Relational authority today does not ban polyglot persistence later. |
| 020 | AWS database selection | **WORKLOAD CATEGORY, not provider winner.** Managed relational, document, key-value, wide-column, and cache services can all be useful under matching workloads. Current owned-rack deployment means AWS products are not selected now, but a future managed-cloud profile may legitimately use one or several. |

## 021-030 retrospective fit audit

| Entry | Topic | Corrected SquiFlow fit interpretation |
|---|---|---|
| 021 | Frontend loading performance | **COMBINABLE CLIENT OPTIMIZATIONS.** Compression, code splitting, selective rendering/windowing, browser caching, preload/prefetch, CDN/origin choices, etc. are selected from measured browser bottlenecks. Prefetch is not inherently good or bad; it is useful only when it does not compete with critical work. |
| 022 | SSO | **USED for identity convenience/centralization, not business authorization.** ZITADEL SSO is a strong fit for Web/Admin/Workstation identity. SquiFlow still owns tenant selection, membership, OpenFGA authorization, and domain rules. |
| 023 | Redis vs Memcached | **BOTH ARE VALID CACHE PRODUCTS FOR DIFFERENT NEEDS.** Memcached can be attractive for simple ephemeral key/value caching; Redis can be attractive when richer structures/coordination/persistence-like features are genuinely useful. SquiFlow currently has no mandatory shared-cache requirement; in-process caching may be simpler for some early paths. |
| 024 | Backend development roadmap | **LEARNING MAP, not stack lock-in.** Languages, databases, APIs, cloud, containers, CI/CD and observability are capability areas. Existing .NET choices remain because they fit current project constraints, not because alternatives are inferior. |
| 025 | Virtualization vs containerization | **CAN COEXIST.** VMs provide stronger OS/kernel isolation and infrastructure boundaries; containers provide lighter packaging/isolation. A cloud VM running containers is normal. SquiFlow should choose the layer mix that best fits isolation, operations, cost, and recovery. |
| 026 | API development roadmap | **MULTIPLE API STYLES MAY COEXIST.** REST/task HTTP, GraphQL, gRPC, WebSockets, webhooks, polling, and async patterns are selected per consumer and interaction semantics. |
| 027 | Network protocols | **PROTOCOL PER REQUIREMENT.** HTTP/TLS/DNS/OIDC fit current Web/API/identity needs; gRPC may fit sync/RPC; SignalR/WebSocket may fit live signaling; MQTT could fit future device integration; SSH fits private operations. No protocol is globally preferred. |
| 028 | Design patterns | **PATTERN PER DESIGN PRESSURE.** Factory, Strategy, Adapter, Observer, Command, etc. are useful when their structural problem exists. SquiFlow avoids pattern ceremony, not the patterns themselves. |
| 029 | Data engineering roadmap | **FUTURE ANALYTICS TOOLBOX.** Kafka, Spark/Flink, Airflow, warehouses/lakes, and notebooks can become useful when SquiFlow has real volume, replay, transformation, governance, or analytical-latency requirements. Their absence from the current baseline is not rejection of their strengths. |
| 030 | Version numbers / SemVer | **ONE VERSIONING SIGNAL AMONG SEVERAL.** SemVer is useful for product/package release communication. API, sync protocol, DB schema, durable message, and configuration compatibility still need their own mechanisms; these are complementary, not competing. |

## 031-040 retrospective fit audit

| Entry | Topic | Corrected SquiFlow fit interpretation |
|---|---|---|
| 031 | Top system-design concepts | **QUESTION MAP, not technology ranking.** Caching, sharding, queues, service discovery, CDN, WebSockets, etc. remain legitimate tools when matching requirements appear. |
| 032 | OOP design patterns | **USE SELECTIVELY.** Patterns are not rejected; they are applied when they simplify a real dependency/lifecycle/behavior problem. |
| 033 | REST API design practices | **REST IS A USEFUL STYLE FOR SOME SURFACES, not the universal API.** SquiFlow benefits from explicit resource/command HTTP contracts for many mutations/resources. GraphQL can fit read composition; gRPC can fit typed streaming/RPC; SignalR can fit live updates. |
| 034 | AWS services | **CAPABILITY CATALOG, not provider commitment.** Managed compute/storage/DB/queue/monitoring services can be excellent in a future cloud profile. Current owned-rack constraints explain why they are not selected today. |
| 035 | Clean code | **HEURISTICS, not winner/loser technologies.** Apply readability/cohesion/DRY principles according to business ownership; do not force abstractions where duplication is safer. |
| 036 | SQL joins | **ALL JOIN TYPES HAVE VALID USE.** INNER/LEFT/RIGHT/FULL are query semantics chosen by the result meaning. None is 'better' universally. |
| 037 | Cloud computing | **DEPLOYMENT OPTION, not future mandate or rejection.** Owned infrastructure is current, while VMs/containers/managed cloud can be future deployment profiles if cost, reliability, support, or customer requirements make them better. |
| 038 | SQL query execution | **MECHANISM STUDY, not selection.** Physical plans are provider/workload-specific and may choose different access paths for different data distributions. |
| 039 | JWT explanation | **TOKEN FIT, not auth architecture by itself.** Provider-issued JWTs can be useful; sessions and current server authorization remain complementary. |
| 040 | Deployment strategies | **STRATEGIES CAN BE COMPOSED OVER MATURITY.** Initial single-node deployment may use maintenance-aware immutable releases. With spare topology, blue-green can enable fast switchback; canary can reduce blast radius; A/B can support experimentation. One does not permanently exclude the others. |

## 041-050 retrospective fit audit

| Entry | Topic | Corrected SquiFlow fit interpretation |
|---|---|---|
| 041 | System design topic map | **COVERAGE MAP, not architecture prescription.** It identifies concerns and mechanisms; SquiFlow selects only those that solve current/future requirements. |
| 042 | Transformers | **FUTURE CAPABILITY CANDIDATE.** Transformer/LLM technology may be valuable for OCR assistance, extraction, classification, search, support, or drafting. It is not current business authority, and any adoption needs privacy/cost/error/human-confirmation contracts. |
| 043 | JWT | **SAME FIT AS 010/039.** Useful provider/protocol token mechanism; not a replacement for current application authorization. |
| 044 | API design pillars | **SURFACE-SPECIFIC API DESIGN.** Interface, paradigm, relationships, versioning, and rate limits combine with SquiFlow's security/idempotency/concurrency needs. REST, GraphQL, gRPC, and live protocols may all be appropriate on different surfaces. |
| 045 | HTTPS/TLS | **PRODUCTION TRANSPORT REQUIREMENT, not competitor choice.** TLS secures transport for HTTP/gRPC/OIDC and can coexist underneath different application protocols. SquiFlow delegates crypto implementation to maintained runtimes/edges. |
| 046 | Server types | **ROLES, not one-server-per-role rule.** Reverse proxy, web, DNS, mail, origin, etc. can be hosted/managed externally or combined depending topology. SquiFlow only operates roles it actually needs. |
| 047 | Amazon Key architecture | **CASE-STUDY PATTERNS MAY TRANSFER WITHOUT COPYING TOPOLOGY.** Device identity, partner isolation, command expiry, OTA, telemetry, and physical OutcomeUnknown can be useful if SquiFlow later controls networked printers/cutters/devices. AWS microservices are not inherently required for those properties. |
| 048 | Shipping code to production | **CONTROL OBJECTIVES, not mandatory toolchain.** GitLab CI or another CI, artifact storage, tests, scans, deployment and observability tools can satisfy the same release controls. Tool choice should fit the team/environment. |
| 049 | Event Sourcing vs CRUD | **BOTH CAN BE VALID IN DIFFERENT DOMAINS.** Current SquiFlow authoritative state is better served by normalized transactional state + explicit history/outbox. Event Sourcing becomes a positive candidate for a domain that truly needs replay-derived authority, temporal reconstruction, or event-first integration enough to justify event-schema/projection/rebuild obligations. A product can mix models by domain. |
| 050 | Data Lake | **FUTURE ANALYTICS/ML CAPABILITY, not current backup/storage architecture.** A governed lake may be valuable when heterogeneous raw history, large-scale analytics, backfill/reprocessing, or ML workloads exist. Object storage and encrypted backup are separate capabilities and can coexist with a future lake. |

## 051-060 retrospective fit audit

| Entry | Topic | Corrected SquiFlow fit interpretation |
|---|---|---|
| 051 | SQL query execution | **MECHANISM/DIAGNOSTIC MODEL.** Different providers/plans can be appropriate; no single access path is globally best. |
| 052 | RabbitMQ | **POSITIVE BROKER CANDIDATE FOR REAL QUEUE/ROUTING NEEDS.** A DB-backed job table is likely simpler for the first Worker workload. RabbitMQ becomes attractive when broker-managed routing, competing consumers, independent queue lifecycles, or operational decoupling outweigh the extra broker/ack/redelivery burden. Kafka/streams may be better for replay/offset-heavy workloads. |
| 053 | Kubernetes | **POSITIVE ORCHESTRATION CANDIDATE AFTER CLUSTER NEED EXISTS.** It is not appropriate merely because SquiFlow has several processes. It can become the right tool for multi-node desired-state reconciliation, rollout, discovery, replacement/failover, and autoscaling if simpler deployment management becomes the bottleneck. |
| 054 | Storage-saving data structures | **USE WHERE APPROXIMATION/STRUCTURE FITS.** Bloom/Cuckoo filters, HyperLogLog, Count-Min, MinHash, SkipLists, etc. can be very valuable for large-scale hints/analytics/indexing. Only exact-authority paths are off limits to approximate answers. |
| 055 | Database normal forms | **NORMALIZED AUTHORITY + DENORMALIZED READ MODELS CAN COEXIST.** SquiFlow starts normalized for invariants and may add materialized/denormalized projections when read workload benefits, with explicit source/freshness/rebuild rules. |
| 056 | CQRS | **ALREADY USED AT THE RESPONSIBILITY LEVEL; FULL TOPOLOGY IS OPTIONAL.** Command/query separation is useful now. Separate read/write stores/services and async projections may become valuable for heavy read/report/search workloads. Event Sourcing is independent and may or may not be combined. |
| 057 | Architecture resources duplicate | **NO TECHNOLOGY SELECTION.** Duplicate preserved for traceability; evidence hierarchy remains the lesson. |
| 058 | Database indexes | **MULTIPLE INDEX TYPES CAN COEXIST.** Provider-specific primary/unique/composite/partial/covering/specialized indexes should be chosen for actual invariants and hot query plans. |
| 059 | API performance duplicate | **SAME FIT AS 016.** The techniques are complementary and measurement-driven. |
| 060 | REST vs GraphQL | **PER-SURFACE FIT, NOT WINNER/LOSER.** REST/task HTTP is useful for explicit business commands/resources, status/error/idempotency semantics, OpenAPI-style contracts, and external integration. GraphQL is a positive candidate for tenant Web/Admin/read-heavy composition where client-selected nested fields materially reduce awkward aggregation/over-fetch. gRPC, SignalR and durable async remain complementary for other boundaries. |

---

## Cross-entry architecture map after bias correction

The retrospective result is intentionally **heterogeneous**:

```text
same-host business calls
    -> in-process

explicit business commands/resources
    -> REST/task-oriented HTTP where it gives the clearest contract

complex nested/client-selected Web/Admin reads
    -> GraphQL candidate when the implemented UI proves the benefit

Workstation sync / high-frequency typed streaming RPC
    -> HTTP or gRPC; gRPC is a preferred evidence-driven candidate

live UI wakeup/update
    -> SignalR/WebSocket-style signaling

long-running / after-commit work
    -> durable outbox + Worker / queue semantics

broker-managed queue/routing need
    -> RabbitMQ candidate

replayable high-volume event history/independent offsets
    -> Kafka/event-stream candidate

transactional central authority
    -> PostgreSQL strongest current reference candidate

local offline Workstation state
    -> SQLite/libSQL candidate family

read acceleration
    -> in-process / Redis / Memcached / DB projection / CDN according to scope and freshness

single/few-host packaging
    -> host services and/or containers

multi-node desired-state orchestration
    -> Kubernetes candidate

relational authority
    -> normalized state

read-heavy derived views
    -> denormalized/materialized projections when useful

special domain needing replay-derived authority
    -> Event Sourcing candidate for that domain only

large heterogeneous analytical/ML history
    -> governed data lake/warehouse pipeline candidate
```

This is the intended architecture-review posture: **use the best-fitting mechanism per real boundary, and allow complementary mechanisms when their semantics differ.**

## Material owner-document impact

This retrospective audit changes the **interpretation method**, not an implementation technology commitment by itself.

It does **not** silently decide to add GraphQL, RabbitMQ, Kafka, Kubernetes, Redis, PASETO, Event Sourcing, a data lake, or another technology. Instead, it removes any implication that those technologies were globally rejected from a comparison article. Each remains available when a real SquiFlow requirement gives it a better fit than the current mechanism.

A concrete adoption/removal that changes the accepted runtime/data/security/deployment architecture still follows the agreed decision process: surface the specific SquiFlow use case, explain why the technology fits that surface, ask for approval when material, then update the Master Implementation Plan and owner documents consistently.

## Retrospective result

**Entries `001-060` have now been re-audited for technology-selection bias.** Any older shorthand in the detailed studies must be interpreted through this file and `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md`.