# ByteByteGo 308-Page Concept Dependency Map

**Status:** Grows sequentially with the exhaustive study.  
**Last mapped article:** `060 — REST API Vs. GraphQL`

The map records concepts only after their source occurrence is reached, while allowing architect-derived links to already accepted SquiFlow design. It is a dependency/reasoning map, not a mandatory technology stack.

## Review-method rule

Technology-comparison articles do **not** create universal winners. Each option is mapped to the SquiFlow boundary where it is strongest, what it costs, what SquiFlow currently uses there and why, and what evidence would justify complementary adoption. See `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md`.

Entries `001-060` have also been re-audited under that rule in `RETROSPECTIVE_TECHNOLOGY_FIT_AUDIT_001_060.md`. Any older shorthand such as `AVOID`, `not baseline`, or `deferred` is surface-scoped and must not be read as a global technology rejection.

## 1. Communication, HTTP, API, and network

```text
same process
    → in-process call

real process/network boundary
    → select protocol from boundary/workload
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

026 + 033 + 044 API design
    → audience / use case
    → interface inputs/outputs
    → resource-oriented or task-oriented contract where useful
    → semantic action endpoints where clearer
    → relationships / domain navigation
    → HTTP methods / status / headers
    → stable Problem Details
    → explicit version compatibility
    → semantic idempotency key for retryable mutation
    → bounded pagination
        → offset / cursor / keyset trade-offs
    → rate/admission limiting
    → authentication
    → TenantContext
    → resource/OpenFGA authorization
    → domain + concurrency validation
    → HTTPS/TLS
    → cacheability/freshness
    → observability/audit correlation

045 HTTPS/TLS
    → authenticated server identity/certificate validation
    → negotiated key establishment
    → symmetric protected application traffic
    → maintained runtime/edge implementation
    → no insecure downgrade

060 REST / GraphQL fit
    → REST/task HTTP strengths
        → explicit resource/command semantics
        → familiar HTTP status/cache/idempotency behavior
        → bounded server-owned shapes
        → external/partner friendliness
        → OpenAPI-style inventory/tooling
    → GraphQL strengths
        → client-selected fields
        → nested/aggregate reads
        → rapidly changing Web/Admin read composition
        → reduce over-fetch/under-fetch where measured
        → typed flexible query schema
    → GraphQL costs to own
        → query depth/complexity/result bounds
        → field/resource/tenant authorization
        → N+1/batching
        → schema/deprecation/introspection policy
        → cache/freshness and observability
    → complementary use is allowed
        → REST/task HTTP for suitable commands/resources
        → GraphQL for suitable read-composition surfaces
        → gRPC for suitable streaming/high-frequency RPC
        → durable async for long-running/after-commit work

architect rules
    → POST is not automatically retry-safe
    → HTTP method idempotence table ≠ proof of application-level retry safety
    → idempotency protects semantic effect, not necessarily byte-identical response
    → negotiated HTTP version does not change business semantics
    → hostname/domain assists routing but is never tenant authority
    → arbitrary outbound URLs require SSRF-safe handling
    → gRPC is preferred to evaluate for earned synchronous boundaries, not universal
    → modern TLS 1.3 is not generally “RSA-encrypt a client-generated session key”
    → TLS termination creates an explicit edge-to-backend trust boundary
    → `A vs B` comparison ≠ project-wide winner/loser decision
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

039 + 043 JWT explanations
    → signed claims / tamper detection
    → symmetric or asymmetric signing
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
    → token algorithm/issuer/audience/key policy is verifier-owned, not trusted from unvalidated claims
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

048 delivery pipeline
    → planned change / source commit
    → automated build + tests + quality/security checks
    → artifact/package storage
    → dev/QA/UAT-style verification as appropriate
    → release candidate
    → production deployment
    → post-release monitoring/alerts

053 Kubernetes detail
    → manifests / desired state
    → API Server / etcd / controllers / scheduler
    → worker/Kubelet/runtime/network
    → Pod / Deployment / Service / volume
    → HPA
    → useful only after a real orchestration workload

SquiFlow release contract
    → one verified immutable artifact
    → source commit / checksum / provenance traceability
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
    → pipeline tool names (Jenkins/JFrog/etc.) are examples, not requirements
    → small team can combine roles without dropping controls
    → single active rack node may honestly require a maintenance window
    → binary rollback does not imply database rollback
    → canary can still have staging; A/B and canary have different objectives
    → PersistentVolume ≠ backup
    → HPA can amplify a downstream bottleneck
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

041 system-design topic map
    → Application Layer
    → Network & Communication
    → Data Layer
    → Scalability & Reliability
    → Security & Observability
    → Infrastructure & Deployments
    → cross-links OOP/DDD/modular monolith/microservices, HTTP/gRPC/AMQP, event-driven comms, SQL/NoSQL/distributed DB, auth, monitoring, IaC, containers/orchestration and disaster recovery

architect-derived SquiFlow completeness additions
    → semantic idempotency
    → concurrency control
    → tenant isolation
    → schema/protocol evolution
    → authoritative-vs-derived state
    → recovery/RPO/RTO
    → backpressure/admission
    → privacy/retention/data lifecycle
    → cost/provider lock-in/operational ownership
    → partial-effect ambiguity/reconciliation

architect rule
    → system-design checklist/map is a question generator, not an infrastructure backlog
    → optional mechanisms and universal concerns are not the same thing
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

052 RabbitMQ
    → producer
    → exchange
        → direct/topic/fanout routing
    → queue
    → consumer
    → production correctness also requires
        → publisher confirm / uncertain publish result
        → consumer ACK/redelivery
        → prefetch/backpressure
        → poison/dead-letter handling
        → bounded retries
        → resource/outage/drain/recovery behavior
    → broker delivery ≠ exactly-once business effect

AWS source examples
    → SQS queue
    → SNS pub/sub
    → EventBridge event bus
    → Kinesis stream

049 event sourcing distinction
    → append-only authoritative domain-event stream
    → replay builds current state/projections
    → durable event-schema/version/rebuild obligations
    ≠ transactional outbox
    ≠ ordinary integration-event log by itself

SquiFlow
    → transactional outbox + simplest durable Worker/job path first
    → normalized current state remains authoritative
    → RabbitMQ is a positive future candidate if broker-managed queue/routing/fan-out needs exceed the simpler mechanism
    → pub/sub only for several real independent consumers
    → stream/Kafka-style log only when replay/offset/throughput need exists
    → event sourcing only if one domain truly needs replay-derived authority

architect rule
    → in-process Observer (032) ≠ durable message delivery
    → event sourcing does not automatically guarantee determinism/global ordering
    → external effects are never replayed blindly during projection rebuild
    → RabbitMQ persistence/routing does not remove semantic idempotency/reconciliation
```

## 7. Cache, approximate structures, and key-value branch

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

054 storage-saving / approximate structures
    → Bloom Filter / Cuckoo Filter
        → membership hint with probabilistic behavior
    → HyperLogLog
        → approximate cardinality
    → MinHash
        → approximate similarity
    → Count-Min Sketch
        → approximate frequency
    → SkipList
        → ordered expected-logarithmic access structure
    → exactness trade-off must be explicit

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
    → approximate structure never becomes exact payment/stock/credit/authorization/quota authority
```

## 8. Database, SQL, query performance, normalization, and PostgreSQL

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

038 SQL logical/execution overview
    → parse / validate
    → internal relational representation
    → optimize
    → choose physical plan using statistics/indexes
    → execute

051 deeper query execution
    → connection/session/transport
    → parse / bind / analyze
    → optimize / plan
    → execution engine
    → storage engine
        → transaction
        → locks/concurrency
        → buffers/cache
        → recovery/durable logging
    → performance depends on plan + statistics + contention + memory + I/O + pool + WAL

055 normalization
    → 1NF
    → 2NF
    → 3NF
    → BCNF
    → 4NF
    → normalize authoritative facts/relationships/invariants
    → denormalize only as derived read optimization

058 index taxonomy
    → primary/unique invariant indexes
    → clustered/physical organization is provider-specific
    → secondary/nonclustered access path
    → composite/partial/covering/specialized choices after provider/workload evidence
    → every index trades reads against write/WAL/storage/maintenance/migration cost

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
    → tenant/data skew + parameter-sensitive plan behavior
    → index benefit + write/WAL/storage/migration cost
    → connection-pool saturation
    → WAL/checkpoint/autovacuum behavior
    → temp/sort spill
    → disk/archive/log growth
    → crash/restart recovery
    → reconnect/import burst behavior

architect rules
    → index availability ≠ index should always be used
    → primary key ≠ universal physical clustering behavior
    → denormalization is not the first response to a slow join
    → tenant skew can make an average-good plan bad for one tenant
    → normalization protects fact identity; it does not prohibit derived read projections
```

## 9. CQRS and Event Sourcing

```text
056 CQRS
    → command responsibility
        → business mutation intent
    → query responsibility
        → business-state read only
    → may optionally split
        → models
        → databases
        → services
        → async projections
    ≠ mandatory separate stores
    ≠ mandatory broker
    ≠ Event Sourcing

049 Event Sourcing
    → event log itself is authoritative state history
    → current state/projections reconstructed from events

SquiFlow
    → command/query separation already useful inside modular monolith
    → one authoritative relational DB initially
    → optional read projections only when real query workload benefits
    → Event Sourcing remains a separate domain-level decision
```

## 10. Data engineering / analytics

```text
029 roadmap
    → batch
    → stream
    → messaging
    → lake / warehouse
    → orchestration
    → CI/CD/IaC
    → notebook/dashboard

050 data lake
    → heterogeneous sources
    → batch OR streaming ingestion
    → raw store
    → transform/process
    → processed analytical store
    → dashboards / AI / warehouse / alerts / reports
    → production needs governance
        → catalog/schema/lineage
        → access/tenant/privacy controls
        → retention/deletion
        → quality/deduplication
        → backfill/reprocessing/versioning
        → cost/lifecycle

SquiFlow distinctions
    → transactional relational system = operational authority
    → object storage = business file/object capability
    → Kaggle backup target = encrypted opaque recovery artifact carrier
    → future analytical dataset = derived/exported, governed, tenant-safe

architect rules
    → reports ≠ automatic warehouse/lake requirement
    → analytical copy remains derived, not OLTP authority
    → copied data retains tenant/privacy/retention obligations
    → Kafka/Spark/Flink/lake infrastructure requires actual volume/freshness/replay/backfill need
    → raw lake without governance can become an untrusted data swamp
```

## 11. Design patterns and clean-code branch

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

## 12. Versioning and compatibility

```text
030 SemVer
    → MAJOR.MINOR.PATCH
    → pre-release labels
    → build metadata

SquiFlow version domains
    → product release
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

## 13. Evidence and learning-roadmap discipline

```text
014 / 057 architecture resources
    → secondary sources expose concepts/questions
    → primary specifications/provider docs close exact semantics
    → representative POC closes SquiFlow-specific performance/capacity claims
    → duplicate occurrences stay independently traceable

024 / 026 / 029 / 037 learning maps
    → useful breadth prompts
    ≠ production dependency lists

034 provider catalog
    → useful capability taxonomy
    ≠ cloud migration decision
```

## 14. AI / probabilistic model branch

```text
042 canonical Transformer teaching flow
    → token/input embedding
    → positional information
    → attention + feed-forward layers
    → masked decoder attention
    → linear/softmax output distribution

architect qualifications
    → canonical encoder-decoder diagram ≠ exact architecture of every named LLM
    → generation may sample; not always highest-probability token

SquiFlow rule
    → no AI/model-serving baseline from this article
    → future AI feature gets explicit provider/privacy/cost/error/offline/audit/human-confirmation contract
    → probabilistic model output never silently becomes payment/stock/permission/financial authority
```

## 15. Server roles and physical/partner integrations

```text
046 server-role vocabulary
    → web
    → mail
    → DNS
    → proxy
    → FTP
    → origin

architect rule
    → role ≠ one physical machine/process
    → only required roles are operated
    → no self-hosted mail/FTP/DNS merely because common

047 Amazon Key case study
    → logistics/partner ingress
    → access-management authorization
    → device-management lifecycle
    → IoT connectivity / MQTT / device shadow / OTA job concepts
    → monitoring/alarms/metrics/logs
    → analytical/BI path
    → physical device at property

future device-integration questions
    → device identity/provisioning/credential rotation
    → replay protection + command expiry
    → intermittent connectivity + reconciliation
    → OTA signing/rollback/bricked-device recovery
    → tamper/stolen credential threat model
    → partner isolation/quotas/versioning
    → physical OutcomeUnknown + audit/manual recovery

SquiFlow connection
    → current print/device side effect remains separate from committed business truth
    → future connected-device capability uses explicit identity/offline/update/reconciliation semantics
    ≠ copy AWS microservice topology
```

## 16. Current SquiFlow integrated path

```text
Workstation local-first operation
    → durable local transaction
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

Web / Admin reads
    → REST/task query surfaces where explicit server-owned shape is simplest
    → GraphQL is a positive candidate for complex/nested/client-driven read composition when the implemented UI proves benefit

long/after-commit consequence
    → durable outbox + Worker
    ≠ forced synchronous chain

central persistence
    → PostgreSQL strongest reference candidate
    → closes through Phase-3 workload/operational POC
    → normalized current state + explicit audit/history
    → optional derived read projections after measured need
    → event sourcing remains separate/deferred

production deployment
    → reproducible definitions/runbooks
    → simplest single-node packaging that works
    → immutable artifact + source/provenance + compatible migrations + health/smoke + recovery
    → cloud/multi-node/Kubernetes/progressive release only after earned trigger

analytics
    → no data lake baseline
    → backup/object storage remain separate operational/business capabilities
```

## Pending future links visible from the index only

These are **not yet studied** and therefore are not treated as completed concept nodes:

- `061` Tokens vs API Keys (next)
- `062`, `097` deeper index/query/performance material
- `063`, `080`, `085` cache failure/placement/query lifecycle
- `067` HTTP status code detail
- `069` additional SSO occurrence
- `073` concurrency vs parallelism
- `074` JWT vs PASETO
- `076`, `082` CI/CD follow-up occurrences
- `077` and URL `040` API versioning strategies
- `083`, `114` Kafka/RabbitMQ follow-up
- `093` explicit modular-monolith source occurrence
- `096` duplicate system-design concept occurrence
- `099-122` deeper networking/DNS/HTTP/service material
- URL section remains independently pending from PDF page `245` onward.

They will be connected only when their sequential PDF pages are processed.
