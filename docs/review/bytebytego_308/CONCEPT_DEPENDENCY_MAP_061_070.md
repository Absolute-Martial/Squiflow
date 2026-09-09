# ByteByteGo Concept Dependency Map — Entries 061-070 Extension

**Status:** Sequential extension of `CONCEPT_DEPENDENCY_MAP.md`.  
**Coverage:** archive entries `061-070`; together with the cumulative map through `060`, this brings mapped coverage through `070 — Best Practices in API Design`.

This extension preserves the review rule that technology comparisons are fit-by-boundary rather than winner/loser decisions.

## 1. Identity, credentials, SSO, and authorization

```text
061 Tokens vs API Keys
    -> credential is selected by principal + lifecycle

human interactive identity
    -> ZITADEL / OIDC / OAuth
        -> authentication / IdP session
        -> short-lived protocol tokens where applicable
        -> Web/Workstation local application session
    -> TenantContext / membership after authentication
    -> OpenFGA current resource authorization
    -> domain/workflow/current-state validation

application/developer integration identity
    -> API key candidate when a simple provisioned/revocable application credential fits
        -> key owner/application binding
        -> scope
        -> safe secret generation/storage/verification
        -> rotation + revocation
        -> leak response
        -> audit/redaction
        -> per-key admission/rate limits
        -> tenant binding verified by server
    -> OAuth client credentials candidate when short-lived/scoped machine tokens fit better
    -> mTLS / signed-request candidate when proof-of-possession or stronger machine identity is required

architect rules
    -> valid JWT != current SquiFlow authorization
    -> API key identifies a client/integration, not automatically an end user
    -> gateway credential validation != backend resource/business authorization
    -> request-supplied tenant ID never becomes tenant authority

069 SSO follow-up
    -> protected Service Provider redirects to IdP
    -> IdP authenticates and creates/reuses central session
    -> per-SP token/assertion/response issued
    -> each SP validates its own response and creates its own app session
    -> second SP reuses IdP session, not the first SP's bearer token
    -> SquiFlow TenantContext/OpenFGA/domain authorization still runs afterward
    -> step-up / logout / IdP-outage behavior remains explicit
```

## 2. Database access structures and query-performance branch

```text
062 database query structures
    -> B-tree / B+ family
        -> ordered point lookup
        -> range / sort-friendly access
    -> hash index
        -> equality-only candidate
        -> no ordered range semantics
    -> bitmap concept
        -> low-cardinality filtering / bitwise combination
        -> provider implementation differs
    -> inverted index
        -> term/value -> posting list / row-document IDs
        -> full-text/document search

provider translation
    -> selected DBMS decides actual implementation
    -> PostgreSQL B-tree/hash/GIN/GiST/BRIN/expression/partial/covering options as applicable
    -> persistent bitmap-index semantics are not universal

selection evidence
    -> actual query predicates/order
    -> cardinality + tenant skew
    -> plan evidence
    -> read latency/IO
    -> write/WAL/storage/maintenance cost
    -> reconnect/import/migration behavior

architect rule
    -> index is derived access path, never business authority
    -> no universal 'fastest index' ranking
```

## 3. Cache safety and failure branch

```text
063 cache failures
    -> thundering herd
        -> many expirations / cold keys at once
        -> DB miss amplification
        -> TTL jitter / admission / coalescing candidate

    -> cache penetration
        -> key absent in cache + authority
        -> repeated DB misses
        -> bounded negative cache / Bloom hint candidate

    -> hot-key breakdown
        -> one high-demand key expires
        -> concentrated fallback load
        -> refresh-ahead / versioned invalidation / coalescing candidate
        -> 'never expire' is not universal because staleness still matters

    -> cache outage
        -> all cache traffic can fall onto DB
        -> circuit breaker / load shedding / safe degraded mode / optional redundancy

cache contract retained
    -> authoritative source + bypass
    -> tenant/permission/config/version-safe key
    -> TTL/invalidation/freshness
    -> bounded memory/entries + eviction
    -> negative lookup rules
    -> stampede/miss amplification
    -> cold-start/repopulation
    -> outage behavior
    -> privacy/logging

architect rule
    -> cache failure may reduce performance/availability
    -> cache failure must not grant access or corrupt/loss authoritative truth
    -> shared cache product is adopted only after a real measured workload
```

## 4. Non-functional requirements and system-quality mapping

```text
064 eight quality attributes
    -> availability
    -> latency
    -> scalability
    -> durability
    -> consistency
    -> modularity
    -> configurability
    -> resiliency

source mechanism examples
    -> availability <-> load balancing
    -> latency <-> CDN
    -> scalability <-> replication
    -> durability <-> transaction log
    -> consistency <-> eventual consistency
    -> modularity <-> loose coupling + high cohesion
    -> configurability <-> configuration-as-code
    -> resiliency <-> queues

SquiFlow correction
    -> quality attribute != one product/mechanism
    -> each NFR declares
        -> normal behavior
        -> failure/degraded behavior
        -> authority
        -> consistency/freshness
        -> resource/capacity limits
        -> recovery
        -> compatibility
        -> observable evidence
        -> acceptance test

examples
    -> load balancer does not remove shared DB/rack/IdP SPOF
    -> CDN helps cacheable/edge work, not local transactional DB latency
    -> replication may improve reads/availability but does not generically scale writes
    -> WAL/log durability scope != backup/DR
    -> eventual consistency is one model, not universal consistency
    -> queue provides buffering/decoupling but adds duplicate/lag/broker semantics
```

## 5. Coding/design/architecture branch

```text
065 coding-pattern vocabulary
    -> Two Pointers / Sliding Window / Binary Search
    -> HashMap / Linked List / Stack / Heap / Prefix Sum
    -> Trees / Tries / Graphs
    -> Backtracking / Dynamic Programming / Greedy / Intervals
    -> implementation problem-solving vocabulary
    != architecture dependency list

066 SOLID
    -> SRP -> one coherent reason to change
    -> OCP -> extension without speculative plugin ceremony
    -> LSP -> behavioral substitutability
    -> ISP -> narrow client contracts
    -> DIP -> stable policy depends on abstractions, volatile details implement them

068 Clean Architecture
    -> Entities / core business policy
    -> Use Cases / application operations
    -> Interface Adapters
    -> Frameworks + Drivers
    -> source-code dependency direction points inward

SquiFlow application
    -> domain/application does not depend on provider SDKs
    -> real provider/replacement/security/fault seams get narrow interfaces
    -> Guard/Core/Admin/Worker process boundaries require real failure/security/runtime reasons
    -> business modules remain in-process modular-monolith modules
    -> generic repository/UoW/service-manager forwarding layers remain non-baseline

architect rules
    -> SOLID != one interface per class
    -> SRP != one method per class
    -> OCP != never modify code
    -> Clean Architecture != four mandatory projects/services
    -> dependency inversion != pretending provider/database behavior does not matter
```

## 6. HTTP status and API-contract branch

```text
067 HTTP status classes
    -> 1xx informational
    -> 2xx success
    -> 3xx redirection
    -> 4xx caller/request/auth/admission/precondition family
    -> 5xx server/gateway/dependency family

SquiFlow semantic mapping
    -> 400 malformed protocol/request shape
    -> 401 missing/invalid acceptable authentication
    -> 403 authenticated but forbidden
    -> 404 absent or deliberately non-disclosed
    -> 409 domain/workflow/uniqueness conflict
    -> 412 failed If-Match / expected version
    -> 422 stable semantic validation where useful
    -> 429 rate/admission limit + Retry-After where meaningful
    -> 500 unexpected SquiFlow defect
    -> 502/503/504 dependency/gateway transient classes with bounded retry policy
    -> 202 only when durable async operation/status resource exists

architect rules
    -> status code + stable SquiFlow failure code / Problem Details
    -> 5xx != automatically retry forever
    -> 4xx != always permanent in every semantic case
    -> timeout/response loss can leave OutcomeUnknown / already-committed possibility
```

## 7. API design follow-up

```text
070 API design
    -> clear naming
    -> semantic idempotency
    -> bounded pagination
        -> offset
        -> cursor/keyset where changing/large datasets require it
    -> bounded sorting/filtering allow-list
    -> cross-resource references / composition
    -> rate limiting + admission/backpressure
    -> version compatibility + retirement lifecycle
    -> authentication + authorization chosen per principal/surface

SquiFlow additions already owned
    -> stable idempotency receipt / same-key same-intent semantics
    -> expected-version / ETag concurrency
    -> TenantContext/resource/field authorization before pagination/projection
    -> durable long-running operation state
    -> structured error/failure taxonomy
    -> bounded payload/query/dependency resource use
    -> old Workstation / sync protocol compatibility
    -> observability + audit correlation

technology fit remains plural
    -> REST/task HTTP -> explicit business commands/resources and external compatibility
    -> GraphQL candidate -> flexible/nested read composition where UI proves benefit
    -> gRPC candidate -> streaming/high-frequency typed process boundary
    -> SignalR/WebSocket -> live non-authoritative signaling
    -> outbox/Worker -> durable long-running/after-commit consequences
    -> API keys / tokens / OAuth / mTLS -> credential chosen by principal and trust need
```

## 8. Current integrated SquiFlow path after entry 070

```text
human login
    -> ZITADEL/OIDC
    -> app session
    -> SquiFlow membership/TenantContext
    -> OpenFGA current authorization
    -> domain/current-state validation

future partner/developer API
    -> API key OR OAuth machine credential OR stronger machine-auth pattern
    -> server verifies tenant/resource scope
    -> rate/admission + audit
    -> normal SquiFlow domain/idempotency/concurrency path

read/write database performance
    -> representative query
    -> provider-specific index structure
    -> actual plan
    -> total read + write/WAL/storage/maintenance evidence

cache proposal
    -> safe authority/freshness/key contract
    -> miss/stampede/cold-start/outage proof
    -> cache can be removed/unavailable without losing business truth

API request
    -> clear bounded contract
    -> authentication
    -> tenant/resource authorization
    -> idempotency + version/concurrency
    -> authoritative transaction OR durable operation acceptance
    -> stable HTTP + SquiFlow semantic result
```

## Pending future links visible from the index only

These are **not yet studied** and are not treated as completed concept nodes:

- `071` Key Terms in Domain-Driven Design (next)
- `072` Modern Software Stack
- `073` Concurrency is NOT Parallelism
- `074` JWT vs PASETO
- `075` Kubernetes Pod lifecycle
- `076`, `082` CI/CD follow-up occurrences
- `077` and URL `040` versioning strategies
- `078` Testing Pyramid
- `079` Docker best practices
- `080`, `085` cache placement/Redis lifecycle
- `083`, `114` Kafka/RabbitMQ follow-up
- `087` Access Control
- `093` Modular Monoliths
- `096` duplicate system-design concepts
- `097` slow API diagnostics
- `099-122` deeper network/HTTP/DNS/API/service material
- URL section remains independently pending from PDF page `245` onward.
