# ByteByteGo 308-Page Concept Dependency Map

**Status:** Grows sequentially with the exhaustive study.  
**Last mapped article:** `040 — How to Deploy Services`

The map records concepts only after their source occurrence is reached, while allowing architect-derived links to already accepted SquiFlow design. It is a dependency/reasoning map, not a mandatory technology stack.

## 1. Communication, HTTP, API, and network

```text
same process
    → in-process call

real process/network boundary
    → RPC / HTTP API
        → contract
        → serialization
        → transport
        → timeout/deadline/cancellation
        → retry ownership
        → semantic idempotency
        → compatibility/version skew
        → observability
        → backpressure

001 gRPC
    → Protocol Buffers example
    → gRPC runtime
    → HTTP/2 example
    → generated client/server contract
    → streaming candidate when workload earns it

017 HTTP evolution
    → HTTP/1.x over TCP
    → HTTP/2 over TCP + binary framing/multiplexing
    → HTTP/3 over QUIC/UDP

018 URL
    → scheme → host → port → path → query → fragment

027 network dependencies
    → IP / ICMP
    → TCP / UDP / QUIC and specialized transports
    → TLS
    → HTTP/HTTPS, DNS, NTP, SSH and other application protocols

026 + 033 API design
    → audience / protocol choice
    → resource-oriented defaults
    → semantic action endpoints where clearer
    → HTTP methods / status / headers
    → stable Problem Details
    → explicit version compatibility
    → semantic idempotency key for retryable mutation
    → bounded pagination
        → offset / cursor / keyset trade-offs
    → authentication
    → TenantContext
    → resource/OpenFGA authorization
    → domain + concurrency validation
    → HTTPS/TLS

architect rules
    → POST is not automatically retry-safe
    → HTTP method idempotence table ≠ proof of application-level retry safety
    → idempotency protects semantic effect, not necessarily byte-identical response
    → negotiated HTTP version does not change business semantics
    → hostname/domain assists routing but is never tenant authority
    → arbitrary outbound URLs require SSRF-safe handling
    → gRPC is preferred to evaluate for earned synchronous boundaries, not universal
```

## 2. Identity, sessions, SSO, and tokens

```text
authentication
    → server-side session + cookie
    → token model
        → JWT
            → header
            → payload/claims
            → signature
                → symmetric OR asymmetric signing
        → PASETO comparison
    → SSO
        → central identity provider
    → OAuth 2.0 delegation framework
    → OIDC identity/authentication

022 SSO
    → shared IdP authentication/session relationship
    → each application still has its own validation/session path

039 JWT simple explanation
    → signed claims / tamper detection
    → signature ≠ encryption
    → token validity still requires issuer/audience/time/key/token-policy checks

SquiFlow
    → ZITADEL / OIDC
        → Web application session
        → Workstation Authorization Code + PKCE
        → Admin Web identity + MFA/step-up capability
    → stable external subject mapping
    → SquiFlow membership / TenantContext
    → OpenFGA current authorization
    → domain/workflow/current-state validation

architect rules
    → cookie, session, JWT, PASETO are not equivalent categories
    → cryptographically valid token ≠ current business authorization
    → SSO centralizes authentication, not tenant/resource authority
    → SquiFlow does not create a second custom JWT/PASETO/password authority
```

## 3. Deployment, infrastructure, cloud, and release engineering

```text
002 containerization/orchestration
    → host process OR container
    → multiple-host orchestration problem
        → Kubernetes candidate only after real placement/reconciliation/scaling pain

003 reproducible infrastructure
    → version-controlled deployment/infrastructure definitions
    → provisioning tool candidate (Terraform/CloudFormation examples)
    → configuration tool candidate (Ansible example)
    → CI/CD / optional GitOps only when needed

025 runtime/isolation
    → bare host
    → VM / hypervisor / guest OS
    → container / shared kernel isolation
    → container inside VM

034 + 037 cloud/provider taxonomy
    → compute
    → storage
    → relational/NoSQL database
    → networking/security
    → observability/DevOps
    → IaC/automation
    → provider examples (AWS/Azure/GCP/etc.)
    ≠ product architecture decision

040 release strategies
    → multi-service/big coordinated release
        → simple rollout, larger dependency/rollback risk
    → blue-green
        → duplicate production-capable environment
        → fast traffic switch/rollback
        → extra capacity cost
    → canary
        → gradual exposure
        → reduced blast radius
        → requires routing + observability + compatibility
    → A/B exposure
        → experimentation/user segmentation
        ≠ inherently rollback strategy

SquiFlow release contract
    → one verified immutable artifact
    → config/secrets outside artifact
    → dependency/capacity/migration preflight
    → drain/bound in-flight work where necessary
    → process health + authorized smoke journey
    → explicit stop / rollback-or-roll-forward / maintenance behavior
    → expand-migrate-switch-contract for unsafe schema contraction
    → blue-green/canary only with spare topology + routing + telemetry + compatible data/contracts

architect rules
    → containerization ≠ IaC ≠ orchestration ≠ HA
    → Docker/Kubernetes are not themselves cloud service models
    → provider catalog ≠ provider selection
    → single active rack node may honestly require a maintenance window
    → binary rollback does not imply database rollback
    → canary can still have staging; A/B and canary have different objectives
```

## 4. Scalability and system-design selection

```text
004 scalability
    → measure bottleneck
        → latency
        → centralized constraint
        → contention / capacity
        → coupling
    → choose targeted response
        → load balancing
        → caching
        → async work
        → partition/shard
        → additional nodes
    → re-measure because bottleneck moves

031 top-20 concept checklist
    → load balancing
    → caching
    → sharding / partitioning
    → replication
    → CAP / eventual consistency
    → consistent hashing
    → queues
    → rate limiting
    → gateway
    → microservices / service discovery
    → CDN
    → indexing
    → WebSockets
    → scalability / fault tolerance
    → monitoring
    → authentication / authorization

architect-derived SquiFlow completeness additions
    → semantic idempotency
    → concurrency control
    → tenant isolation
    → schema/protocol evolution
    → authoritative-vs-derived state
    → recovery/RPO/RTO
    → backpressure/admission
    → partial-effect ambiguity/reconciliation

architect rule
    → system-design checklist is a question generator, not an infrastructure backlog
    → no microservices, service discovery, sharding, distributed cache, replicas, or extra nodes without an earned workload/failure reason
```

## 5. Frontend / browser performance

```text
021 frontend critical path
    → transferred bytes / compression
    → render work / selective rendering / windowing
    → module payload / splitting / tree shaking / dynamic import
    → loading order / preload / prefetch
    → measure network waterfall, page weight, parse/execute/render/interaction cost

architect rules
    → preload/prefetch can compete with critical resources
    → browser caching/prefetch never creates offline business authority
    → tenant/authorization-sensitive cacheability is explicit
```

## 6. Messaging and asynchronous work

```text
015 communication semantics
    → one durable work item
        → queue / competing consumers
    → one fact, several independent reactions
        → pub/sub
    → rule/filter routing among systems
        → event bus
    → replayable high-volume ordered/partitioned data
        → event stream

AWS source examples
    → SQS queue
    → SNS pub/sub
    → EventBridge event bus
    → Kinesis stream

SquiFlow
    → transactional outbox + simplest durable Worker/job path first
    → pub/sub only for several real independent consumers
    → stream/Kafka-style log only when replay/offset/throughput need exists

architect rule
    → in-process Observer (032) ≠ durable message delivery
```

## 7. Cache and key-value branch

```text
019 storage-selection dimensions
    → data model
    → replication
    → consistency
    → discovery/membership
    → partitioning
    → durability/transactions/operations/tenant isolation

023 Redis vs Memcached framing
    → Memcached: simpler key/value + LRU comparison point
    → Redis: richer structures + optional persistence/pub-sub/scripting/replication examples

cache contract
    → authoritative source + bypass
    → tenant/permission/config/version-safe key
    → TTL / freshness / invalidation
    → bounded memory/entries + eviction
    → stampede / miss amplification
    → cold-start / repopulation
    → outage behavior
    → privacy/diagnostics

architect rules
    → cache product not selected from feature matrix
    → persistence capability does not make cache authoritative
    → stale cache never becomes payment/stock/credit/tenant/authorization authority
```

## 8. Database, SQL, query performance, and PostgreSQL

```text
009 database performance
    → workload profile
    → query/access path
    → indexes
    → concurrency/transactions
    → maintenance/storage
    → replication/partition/sharding only after evidence

012 PostgreSQL internal map
    → client/backend-process cost
    → shared memory / buffers
    → WAL writer / background writer / checkpointer
    → autovacuum
    → archiver / replication launcher
    → data/WAL/archive/log files

036 SQL joins
    → INNER
    → LEFT
    → RIGHT
    → FULL OUTER
    → join semantics affect missing rows / NULLs
    → one-to-many expansion can multiply rows and corrupt aggregates if misunderstood

038 SQL execution
    → parse / validate
    → internal relational representation
    → optimize
    → choose physical plan using statistics/indexes
    → execute

logical SQL order
    → FROM/JOIN/ON
    → WHERE
    → GROUP BY / HAVING
    → SELECT
    → ORDER BY
    → LIMIT
    ≠ literal physical execution order

SquiFlow Phase-3 proof
    → representative tenant/cardinality workload
    → actual execution plans
    → index benefit + write/WAL/storage/migration cost
    → connection-pool saturation
    → WAL/checkpoint/autovacuum behavior
    → temp/sort spill
    → disk/archive/log growth
    → crash/restart recovery
    → reconnect/import burst behavior

architect rules
    → index availability ≠ index should always be used
    → denormalization is not the first response to a slow join
    → tenant skew can make an average-good plan bad for one tenant
```

## 9. Data engineering / analytics

```text
029 roadmap
    → batch
    → stream
    → messaging
    → lake / warehouse
    → orchestration
    → CI/CD/IaC
    → notebook/dashboard

architect rules
    → reports ≠ automatic warehouse requirement
    → analytical copy remains derived, not OLTP authority
    → copied data retains tenant/privacy/retention obligations
    → Kafka/Spark/Flink/lake infrastructure requires actual volume/freshness/replay/backfill need
```

## 10. Design patterns and clean-code branch

```text
028 broad pattern vocabulary
    → creational
    → structural
    → behavioral

032 focused OOP patterns
    → Factory / Singleton / Builder
    → Adapter / Decorator / Proxy
    → Strategy / Observer / Command

035 clean code
    → meaningful names
    → cohesive/single-purpose functions
    → named constants / descriptive booleans
    → avoid needless duplication
    → reduce deep nesting
    → comments explain why
    → keep APIs/arguments understandable
    → self-explanatory structure

architect rules
    → pattern name follows concrete problem
    → Singleton is runtime/container scope, not global cross-node uniqueness
    → global mutable singleton state is hazardous for tenancy/testing/concurrency
    → Command object ≠ CQRS/event sourcing/broker
    → DRY must not merge business concepts with different reasons to change
    → clean-code rules are heuristics, not correctness substitutes
```

## 11. Versioning and compatibility

```text
030 SemVer
    → MAJOR.MINOR.PATCH
    → pre-release labels
    → build metadata

SquiFlow version domains
    → product release (current v0.0.15)
    → HTTP API
    → Workstation sync protocol
    → central/local schema
    → durable job/message
    → rule/workflow/form/config snapshots

compatibility evolution
    → additive/compatible first
    → expand
    → migrate
    → switch readers/writers
    → contract after inventory/drain evidence
    → retirement window + rollback/roll-forward proof

architect rule
    → SemVer communicates release intent; it does not implement compatibility
    → `0.x` does not permit silent customer-data/client breakage
```

## 12. Evidence and learning-roadmap discipline

```text
014 architecture resources
    → secondary sources expose concepts/questions
    → primary specifications/provider docs close exact semantics
    → representative POC closes SquiFlow-specific performance/capacity claims

024 / 026 / 029 / 037 learning maps
    → useful breadth prompts
    ≠ production dependency lists

034 provider catalog
    → useful capability taxonomy
    ≠ cloud migration decision
```

## 13. Current SquiFlow integrated path

```text
Workstation local-first operation
    → durable SQLite/local transaction
    → durable outbox
    → HTTP baseline OR measured gRPC candidate
    → server identity/device validation
    → TenantContext
    → OpenFGA authorization
    → domain/rule validation
    → semantic idempotency
    → expected-version/conflict handling
    → authoritative central transaction
    → durable acknowledgement/reconciliation

same-runtime modules
    → in-process communication

long/after-commit consequence
    → durable outbox + Worker
    ≠ forced synchronous chain

central persistence
    → PostgreSQL strongest reference candidate
    → closes through Phase-3 workload/operational POC

Web
    → online-only business operations in v0.0.15
    → optimize measured browser bottlenecks

production deployment
    → reproducible definitions/runbooks
    → simplest single-node packaging that works
    → immutable artifact + compatible migrations + health/smoke + recovery
    → cloud/multi-node/Kubernetes/progressive release only after earned trigger
```

## Pending future links visible from the index only

These are **not yet studied** and therefore are not treated as completed concept nodes:

- `041` System Design Topic Map (next)
- `042` Transformer architecture
- `043` additional JWT framing
- `044` API design pillars
- `045` HTTPS internals
- `048` production code-shipping flow
- `049` event sourcing and `056` CQRS
- `051`, `058`, `062`, `097` deeper SQL/index/query material
- `052`, `083`, `114` RabbitMQ/Kafka
- `053` Kubernetes detail
- `063`, `080`, `085` cache failure/placement/query lifecycle
- `077` and URL `040` API versioning strategies
- `093` explicit modular-monolith source occurrence
- `096` duplicate system-design concept occurrence
- `099-122` deeper networking/DNS/HTTP/service material
- URL section remains independently pending from PDF page `245` onward.

They will be connected only when their sequential PDF pages are processed.
