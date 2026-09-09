# ByteByteGo Concept Dependency Map — Entries 111-120 Extension

**Coverage:** archive entries `111-120`; together with prior maps, concept coverage is current through `120 — Why Is Nginx So Popular?`.

This extension follows the standing rule: comparison/catalog material is converted into exact SquiFlow **what / where / why / alternative-fit / evidence / falsification** reasoning. It does not create winner/loser technology decisions.

## 1. REST constraints -> pragmatic SquiFlow API contracts

```text
111 REST constraints
    -> client/server
    -> stateless request semantics
    -> uniform interface
    -> explicit cacheability
    -> layered system
    -> optional code-on-demand

SquiFlow actual API use
    -> REST/task HTTP for explicit command/resource contracts
    -> semantic action subresources allowed
    -> REST purity is not a product requirement

other valid surfaces
    -> GraphQL candidate for complex read composition
    -> gRPC candidate for typed/streaming real boundaries
    -> SignalR/WebSocket/SSE for live hints
    -> Worker/outbox for durable long-running consequences

unchanged authority
    -> TenantContext + OpenFGA + domain/concurrency/idempotency
```

## 2. Hypervisor placement -> deployment evidence

```text
112 virtualization
    Type 1
        guest OSes -> hypervisor -> hardware
    Type 2
        guest OSes -> hypervisor app -> host OS -> hardware

SquiFlow
    packaging remains OPEN
        -> bare metal
        -> VM
        -> container
        -> VM + container

selection evidence
    -> actual rack RAM/CPU/storage
    -> fault/isolation need
    -> patching/rebuild
    -> backup/restore
    -> replacement-hardware recovery

rule
    VM snapshot != backup
    VM boundary != independent physical failure domain
```

## 3. Database catalog -> authority-first storage selection

```text
113 database categories
    relational / columnar / KV / in-memory / wide-column
    time-series / ledger / graph / document / geospatial
    text-search / blob / vector

critical caveat
    categories overlap
    one product may span several categories

SquiFlow current roles
    central business authority -> relational candidate
    Workstation local -> SQLite/libSQL candidates
    object bytes -> IObjectStore
    backup -> IBackupTarget
    authorization relationships -> OpenFGA
    operational log search -> OpenSearch target

new store gate
    exact missing capability
    -> authority or derived?
    -> freshness/rebuild
    -> tenant/privacy lifecycle
    -> recovery/exit
```

## 4. Messaging semantics -> broker only when earned

```text
114
Kafka
    retained partitioned log
    independent offsets
    replay/history
    partition ordering

RabbitMQ
    exchange routing
    queues
    competing consumers
    acknowledgements

SquiFlow current first fit
    transactional outbox
    -> DB-backed durable job

future RabbitMQ trigger
    -> broker-managed routing / competing consumers / queue lifecycle

future Kafka trigger
    -> durable replay/history / independent offsets / stream throughput

rule
    broker ack/offset != end-to-end exactly-once effect
```

## 5. HTTP ecosystem -> capability inventory

```text
115 HTTP mindmap
    transport       HTTP1/2/3, TCP/QUIC
    security        TLS/WAF
    routing         DNS/proxy/gateway
    APIs            REST/RPC/GraphQL
    live            WebSocket
    delivery        CDN/web server
    diagnostics     packet capture + telemetry

SquiFlow current
    HTTPS/TLS
    REST/task HTTP
    reverse-proxy edge capability
    DNS/custom domains
    OpenTelemetry

conditional
    GraphQL / gRPC / live transport / CDN

correction
    OpenTelemetry != packet capture
    WAF != business authorization
```

## 6. DNS -> routing, not tenant authority

```text
116 resolution
    browser/OS cache
    -> recursive resolver
    -> root
    -> TLD
    -> authoritative nameserver
    -> A/AAAA/etc
    -> client connection

SquiFlow custom-domain trust
    DNS ownership evidence
    + unique hostname mapping
    + TLS lifecycle
    + callback lifecycle
    != TenantContext/OpenFGA authority

failure dimensions
    TTL/stale cache
    IPv4 vs IPv6
    certificate expiry
    DNS outage
    old/new target overlap
```

## 7. Live delivery -> recoverable hint over durable state

```text
117
short polling
long polling
SSE
WebSocket / SignalR

selection axis
    required freshness
    directionality
    connection count
    proxy compatibility
    reconnect/backpressure

SquiFlow rule
    live message
        -> wake/refetch/update UX
    durable DB/outbox/status
        -> authoritative truth

multi-node backplane
    -> only after real multi-node Web topology
```

## 8. HTTP version evolution -> transport-independent semantics

```text
118
HTTP/1.0 -> richer headers/status
HTTP/1.1 -> persistent connections
HTTP/2 -> multiplexed streams over TCP
HTTP/3 -> QUIC streams over UDP

SquiFlow
    edge/runtime negotiates supported version
    business API semantics unchanged

gRPC candidate
    -> HTTP/2 path/proxy/customer-network proof required

rule
    newer protocol != fixed DB/auth/idempotency bottleneck
```

## 9. Performance metrics -> constrained-resource diagnosis

```text
119 source metrics
    QPS
    TPS
    concurrency
    response time

SquiFlow adds
    latency percentiles
    errors/timeouts
    queue wait
    backlog oldest age
    DB pool wait/locks/WAL/disk
    provider latency/quota
    CPU/RAM/network
    tenant fairness

measurement loop
    define workload/unit
    -> measure load + completed throughput
    -> latency distribution
    -> concurrency/queueing
    -> first constrained resource
    -> targeted change
    -> repeat correctness + performance test
```

## 10. Edge product -> capability-first selection

```text
120 Nginx capabilities
    web server
    reverse proxy
    load balancer
    cache
    TLS termination

SquiFlow actual edge requirement
    public TLS
    hostname/custom-domain routing
    Core/Admin direct route separation
    request/body limits
    coarse edge policy
    protocol negotiation

product remains OPEN
    Nginx / Caddy / HAProxy / Envoy / platform/managed alternatives

selection evidence
    resource cost
    certificate lifecycle
    gRPC/HTTP behavior
    config reload/rollback
    observability
    private recovery

rule
    one edge + one backend != HA
    proxy cache != authority
```

## 11. Integrated interrogation after entry 120

```text
source technology/comparison/catalog
    -> exact SquiFlow boundary
    -> current documented mechanism
    -> why that mechanism exists
    -> implementation evidence level
    -> alternative's better-fit surface
    -> coexistence possibility
    -> authority unchanged
    -> failure/recovery cost
    -> adoption evidence
    -> falsification/change trigger
```

## Next pending concepts

Archive `121 — Common Network Protocols Every Engineer Should Know` begins at PDF page `237`, followed by `122 — 8 Popular Network Protocols` and `123 — 9 best practices for developing microservices`. After archive page `241`, the detailed URL section begins at PDF page `245`; structural pages `242-244` remain inventory-only until reached sequentially.
