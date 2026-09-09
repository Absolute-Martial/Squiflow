# ByteByteGo Concept Dependency Map — Entries 081-090 Extension

**Status:** Sequential extension after `CONCEPT_DEPENDENCY_MAP_071_080.md`.  
**Coverage:** archive entries `081-090`; together with prior maps, concept coverage is current through `090 — 24 Good Resources to Learn Software Architecture in 2025`.

This extension applies both the technology-fit rule and `CRITICAL_INTERROGATION_RULE.md`: comparisons and catalogs do not select architecture. Each mechanism must name the exact SquiFlow boundary, the problem/invariant it solves, why it fits, its new failure/operations cost, recovery, adoption evidence and falsification evidence.

## 1. Design pattern vocabulary -> concrete SquiFlow pressure

```text
081 Design Patterns Cheat Sheet
    -> creation patterns
        -> Factory / Abstract Factory / Builder / Prototype / Singleton
    -> behavioral patterns
        -> Chain of Responsibility / Command / Iterator / Observer / State / Strategy
    -> structural patterns
        -> Adapter / Bridge / Composite / Facade / Flyweight / Proxy

SquiFlow decision rule
    -> direct code first
    -> introduce pattern only when a real pressure exists
        -> provider replacement/failure
        -> explicit business intent
        -> state-machine complexity
        -> staged cross-cutting pipeline
        -> true algorithm/provider variation
        -> hierarchical object model
        -> measured memory pressure

current documented fits
    -> IObjectStore / IBackupTarget
        -> Adapter-shaped provider boundary
        -> stable SquiFlow semantics
        -> provider SDK hidden behind adapter
    -> ApproveQuote / RefundPayment / AdjustInventory
        -> Command-shaped business intent
        -> idempotency + authz + concurrency stay explicit
    -> quotation/payment/workflow state
        -> State semantics
        -> richer State pattern only if implementation complexity earns it
    -> ASP.NET request pipeline
        -> Chain-like staged concerns
        -> framework middleware/policies preferred over custom pattern ceremony

critical caveats
    -> DI singleton != durable/shared/global business state
    -> in-memory Observer != durable integration event
    -> Proxy must not hide remote latency/failure
    -> one interface/Factory per class is not architecture quality
```

## 2. Delivery evidence -> release/recovery

```text
082 CI/CD
    -> source change
    -> build
    -> deterministic checks/tests
    -> review/merge policy
    -> immutable artifact
    -> deploy/release
    -> health + authorized smoke
    -> recovery

source example
    -> Jenkins
    -> unit/integration/code/security checks
    -> Docker image
    -> Kubernetes deployment

SquiFlow reality
    -> current repo has no application source / .gitlab-ci.yml yet
    -> owner docs already define release contract
        -> checksum/provenance
        -> configuration/secrets outside artifact
        -> migration/capacity/dependency preflight
        -> same bytes promoted
        -> health + business smoke
        -> rollback OR roll-forward OR maintenance restore
        -> actual-hardware/provider/restore evidence outside ordinary CI

critical question
    -> what production claim does this stage actually prove?
    -> what remains unproven?
```

## 3. Durable job/outbox -> event stream trigger

```text
083 Kafka
    -> producer
    -> serializer
    -> partitioner/key
    -> topic partition
    -> brokers
    -> replication
    -> retained ordered partition log
    -> consumer group
    -> offsets / ownership / rebalance

source guarantee refined
    -> ordering is per partition
    -> replication safety depends on acks/ISR/config
    -> effect-before-offset can duplicate
    -> offset-before-effect can lose work
    -> Kafka exactly-once scope != external business exactly-once

SquiFlow current
    -> authoritative relational transaction
    -> transactional outbox
    -> simple durable DB-backed job/Worker candidate

Kafka adoption trigger
    -> durable replay/history genuinely needed
    + independent consumer offsets
    + several real consumers
    + sustained throughput
    + partition-scoped ordering
    + manageable broker/storage/upgrade operations

coexistence
    -> DB transaction/outbox may publish committed facts to Kafka later
    -> Kafka transport/replay does not replace business authority/idempotency
```

## 4. Public edge roles -> one or more capabilities

```text
084 edge traffic
    -> reverse proxy
        -> TLS termination
        -> host/path routing
        -> forwarding
    -> API-gateway capability
        -> coarse admission/rate/request-size/WAF/private policy
        -> optional protocol/edge observability
    -> load balancer
        -> choose among multiple healthy backend instances

source diagram
    -> edge LB
    -> gateway validations/whitelist/auth/rate/discovery/transform/proxy
    -> per-service load balancers
    -> microservice instances

SquiFlow current fit
    -> stable edge/TLS/custom-domain routing boundary
    -> Core API and Admin API independently authn/authz/validate resources
    -> no business authorization moved into edge
    -> no internal load balancer until multiple healthy instances exist
    -> no microservice inference from source diagram

critical failure
    -> edge can be SPOF
    -> forwarded-header trust
    -> DB/rack/IdP SPOFs remain even with LB
    -> private recovery path cannot depend only on public edge
```

## 5. Redis role -> persistence policy

```text
085 Redis
    -> RAM-first execution
    -> AOF
        -> append command history
        -> fsync policy controls durability/latency
        -> rewrite/disk behavior
    -> RDB
        -> periodic snapshot
        -> fork + copy-on-write
        -> bounded-loss window if alone
    -> mixed persistence

first question
    -> what role would Redis own?
        -> disposable cache
        -> shared session/revocation state
        -> rate/admission counter
        -> coordination
        -> stream/queue-like structure

role determines
    -> authority
    -> acceptable loss
    -> persistence
    -> replication/failover
    -> cold-start/rebuild
    -> outage behavior

SquiFlow current
    -> no Redis requirement yet
    -> caches remain disposable/non-authoritative
    -> multi-node server session state may create future shared-store need
```

## 6. Browser cookie + server session -> identity/application authorization split

```text
086 Web session
    -> browser cookie
        -> minimal opaque/session material
        -> Secure / HttpOnly / SameSite / scope / expiry
    -> server-side SquiFlow session
        -> app session context
        -> revocation/rotation
        -> shared store only when multi-node requires it
    -> ZITADEL/OIDC
        -> authentication / IdP session
    -> SquiFlow TenantContext + OpenFGA + domain state
        -> current authorization

not equivalent
    -> cookie vs session is not either/or
    -> IdP session != SquiFlow application session
    -> Blazor circuit != durable business/session authority
    -> Workstation PKCE native flow != browser cookie session
```

## 7. Access control -> composed models

```text
087 source models
    -> RBAC
        -> user -> role -> permission
    -> ABAC
        -> user/resource/environment attributes -> policy decision
    -> ACL
        -> explicit user/group/resource entries

missing but relevant
    -> ReBAC
        -> relationship graph
        -> OpenFGA/Zanzibar-style authorization

SquiFlow authorization composition
    -> stable capability vocabulary + tenant custom roles
        -> RBAC-like grouping
    -> OpenFGA relationships / role objects / resource scopes
        -> ReBAC
    -> TenantId filtering/RLS defense
        -> data-isolation layer
    -> domain/workflow/current state/delegation ceiling
        -> attribute/state-like business constraints

architect rule
    -> no single-model winner
    -> OpenFGA allow != domain-state allow
    -> role claim != current permission authority
    -> per-row ACL only when resource-sharing model earns it
```

## 8. Provider contract -> API + optional SDK

```text
088 API
    -> external contract/protocol
    -> endpoints/messages/status/errors/auth semantics

SDK
    -> language/platform toolkit/client over API
    -> models/auth/serialization/retries/helpers

SquiFlow integration rule
    -> provider API semantics remain real boundary
    -> SDK or raw HTTP chosen per provider
        -> SDK quality/coverage
        -> streaming/features
        -> error transparency
        -> retry control
        -> dependency footprint
    -> SquiFlow adapter where replacement/failure seam is real
    -> provider SDK types do not leak into domain/business contracts
    -> retry owner remains explicit

security caveat
    -> bearer-like API key should not be placed in URL unless provider requires/safely designs it
```

## 9. SQL structure -> injection-safe query path

```text
089 SQLi
    -> attacker-controlled value becomes SQL structure
        -> tautology
        -> UNION/error
        -> blind boolean
        -> blind timing

primary defense
    -> trusted query structure
    -> parameterized untrusted values
    -> allow-listed identifiers/operators for dynamic sort/filter/report paths

SquiFlow defense-in-depth
    -> TenantContext-scoped query
    -> least-privilege DB runtime identity
    -> no superuser/BYPASSRLS convenience
    -> safe Problem Details/errors
    -> field/request allow-lists
    -> two-tenant hostile tests
    -> timeout/WAF/RLS as secondary, not replacement controls

critical evidence
    -> currently documented requirement
    -> not yet verified implementation because application code is not committed
```

## 10. Architecture resources -> evidence hierarchy

```text
090 resources
    -> books
    -> company engineering blogs/newsletters
    -> talks/channels/architecture centers
    -> whitepapers
    -> career/design books

SquiFlow evidence hierarchy
    -> source raises concept/question
    -> current SquiFlow requirement/boundary checked
    -> primary spec/product docs checked for consequential claims
    -> representative POC/measurement where needed
    -> explicit failure/recovery/operations cost
    -> only then architecture decision

large-company/provider architecture
    -> useful mechanism lessons
    != automatic fit for small-team owned-rack SquiFlow
```

## 11. Integrated interrogation after entry 090

```text
proposal/comparison/catalog
    -> what exact SquiFlow boundary?
    -> what are we doing there now?
    -> is it implemented or only documented/planned?
    -> why was current mechanism chosen?
    -> what real pain/invariant is unresolved?
    -> what specific property of candidate helps?
    -> can current + candidate coexist on different surfaces?
    -> what new failure/recovery/ops burden appears?
    -> what evidence justifies adoption?
    -> what evidence would falsify/change it?
    -> what remains authoritative and unchanged?
```

## Pending future links visible from the index only

These remain **not yet studied**:

- `091` Cross-Site Scripting (XSS) Attacks (next)
- `092` Batch vs Stream Processing
- `093` What are Modular Monoliths?
- `094` Process vs Thread
- `095` Latency vs Throughput
- `096` duplicate System Design Concepts occurrence
- `097` How to Debug a Slow API?
- `098` modern server types
- `099-100` networking/service building blocks
- `101` API design follow-up
- `102-103` virtualization/cloud comparisons
- `104` backend stack
- `105-106` HTTP/HTTPS and proxy follow-up
- `107-108` concurrency/virtualization duplicate follow-up
- `109-123` auth/firewall/REST/DB/Kafka/network/realtime/HTTP/Nginx/microservices follow-up
- URL section remains independently pending from PDF page `245` onward.
