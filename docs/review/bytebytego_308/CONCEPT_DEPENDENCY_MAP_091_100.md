# ByteByteGo Concept Dependency Map — Entries 091-100 Extension

**Coverage:** archive entries `091-100`; together with prior maps, concept coverage is current through `100 — Network Services That Power Modern Connectivity`.

This extension applies the fit/use rule, critical-interrogation rule, and implementation-evidence rule: a source comparison/catalog does not select architecture, and a documented requirement is not proof of implementation.

## 1. Browser content safety

```text
091 XSS
    -> attacker-controlled source
        -> reflected request data
        -> stored business/user content
        -> DOM/client-side data source
    -> unsafe sink/context
        -> HTML
        -> attribute/URL
        -> JavaScript/DOM
    -> browser-origin execution

SquiFlow controls
    -> framework output encoding by default
    -> no raw HTML for convenience
    -> allow-list sanitizer only for real rich-content need
    -> CSP/headers as defense-in-depth
    -> Secure/HttpOnly/SameSite session hardening
    -> hostile real-rendering tests

critical question
    -> which exact SquiFlow fields/sinks can become executable browser content?
```

## 2. Background processing semantics

```text
092 processing model
    -> one durable owned task
        -> DB-backed job / Worker
    -> scheduled/rebuild/reconciliation set
        -> batch processing
    -> retained facts + replay + independent consumer progress
        -> stream-processing candidate
    -> live wake-up only
        -> SignalR/WebSocket-style signal + durable state fetch

batch != accuracy by definition
stream != correctness loss by definition
real-time != undefined marketing label

critical question
    -> what freshness/replay/state-recovery requirement actually exists?
```

## 3. Modular monolith -> extraction trigger

```text
093 modular monolith
    -> business capability boundary
    -> cohesive ownership
    -> explicit in-process interface
    -> shared deployment/process where appropriate
    -> shared DB infrastructure allowed with data ownership

SquiFlow current documented fit
    -> ordinary business modules in-process
    -> Core API / Admin API / future Worker as runtime hosts
    -> Guard / Workstation separate for supervision/fault reasons

extraction trigger
    -> repeated independent scaling
    -> independent deployment cadence
    -> fault/security isolation
    -> team/ownership boundary
    -> explicit data ownership
    -> operational evidence that network cost is worth it

critical question
    -> is this boundary a real invariant/ownership boundary or only a folder?
```

## 4. Process isolation vs in-process concurrency

```text
094 process
    -> separate address space
    -> stronger fault/security/deployment boundary
    -> IPC/version/recovery overhead

thread/task
    -> same-process memory/fault boundary
    -> cheaper communication
    -> races/contention

SquiFlow examples
    -> Guard != Workstation process
        because external supervision
    -> Admin API != Core API process
        because security/availability plane
    -> Worker process
        because durable long-running work isolation
    -> modules
        stay in-process until real isolation need

critical question
    -> what must survive or be inaccessible when this process fails?
```

## 5. Performance objective decomposition

```text
095 latency
    -> end-to-end journey delay
    -> service time + queue wait + network + dependency
    -> distribution/tails

throughput
    -> completed work / time
    -> requests/s, jobs/s, bytes/s, records/s

saturation
    -> higher offered load
    -> queue growth
    -> tail latency
    -> backpressure/admission

SquiFlow metrics by surface
    -> Web/API latency distribution
    -> local Workstation action latency
    -> sync backlog age + drain throughput
    -> Worker oldest-item age/job duration
    -> object/backup transfer throughput

critical question
    -> which exact journey and unit are we optimizing?
```

## 6. Concept catalog -> problem-first adoption

```text
096 system-design catalog
    -> load balance / cache / sharding / replication / CAP / hashing
    -> queue / rate limit / gateway / microservices / discovery / CDN
    -> index / partition / eventual consistency / WebSocket
    -> scale / fault tolerance / monitoring / authn/authz

SquiFlow adds missing decision dimensions
    -> semantic idempotency
    -> tenant isolation
    -> compatibility/version skew
    -> authority vs derived state
    -> concurrency/invariants
    -> backpressure/resource budgets
    -> recovery/RPO/RTO
    -> privacy lifecycle
    -> small-team operability

rule
    -> catalog = question inventory, not implementation backlog
```

## 7. Slow API diagnosis -> evidence-driven fix

```text
097 slow API
    -> define slow journey + load percentile
    -> trace end-to-end
    -> separate queue vs execution
    -> inspect CPU/GC/locks
    -> DB pool/query plan/index/cardinality
    -> provider latency/timeouts/retries
    -> host CPU/RAM/disk/network
    -> change one proven bottleneck
    -> retest correctness + performance

source fixes challenged
    CDN
    compression
    background work
    async
    indexes
    parallel calls
    retries
    autoscale
    pool tuning
        -> all conditional on the actual bottleneck/semantics

critical question
    -> what evidence proves this fix addresses the first constrained resource?
```

## 8. Server roles -> capabilities, not mandatory machines

```text
098 roles
    -> DNS
    -> Web
    -> Application
    -> Load balancer / Proxy
    -> Database
    -> Cache
    -> File

SquiFlow mapping
    -> DNS/edge routing
    -> Core/Admin application hosts
    -> central relational DB authority
    -> IObjectStore business-object bytes
    -> optional non-authoritative cache
    -> load balancer only when multiple healthy instances exist

critical corrections
    -> DB replica != backup
    -> object storage != shared file server
    -> role != required separate product/process
```

## 9. Network capability inventory

```text
099 categories
    -> switching/routing/DNS/DHCP/NTP
    -> firewall/VPN/IDS-IPS
    -> load balance/reverse proxy/gateway
    -> IdP/AAA/PKI
    -> SIEM/NMS
    -> Wi-Fi/IoT edge
    -> NFV

SquiFlow current documented needs
    -> DNS/TLS/time
    -> firewall/edge exposure
    -> ZITADEL IdP
    -> private recovery path
    -> actual rack/uplink/topology qualification

conditional only
    -> VPN
    -> IDS/IPS
    -> SIEM/NMS
    -> SD-WAN
    -> IoT gateway
    -> NFV

critical question
    -> which capability is actually required by the first-customer topology and who operates it?
```

## 10. Service/protocol exposure map

```text
100 service vocabulary
    -> DNS / DHCP / NTP
    -> SSH / RDP
    -> SMTP submission
    -> HTTPS / HTTP3-QUIC
    -> LDAP over TLS
    -> OAuth2 / OIDC
    -> DB protocols
    -> WireGuard / IPsec

SquiFlow fit
    public
        -> HTTPS/TLS only intended application/identity surfaces
    outbound/integration
        -> ZITADEL OIDC/OAuth
        -> SMTP only if selected notification channel needs it
    private
        -> DB protocol
        -> operator recovery SSH/RDP/VPN according to host topology
    infrastructure-owned
        -> DHCP / NTP / DNS mechanics

identity rule
    -> enterprise LDAP can federate through IdP if needed
    -> no direct LDAP coupling merely because customer uses AD

critical question
    -> why is each port/service exposed, who initiates it, and what happens when it fails?
```

## 11. Integrated interrogation after entry 100

```text
comparison/catalog/proposal
    -> exact SquiFlow boundary
    -> current documented mechanism
    -> implementation evidence level
    -> concrete problem/invariant
    -> why current mechanism fits
    -> alternative/candidate strengths on another surface
    -> coexistence possibility
    -> failure/recovery/operational burden
    -> adoption evidence
    -> falsification/change trigger
    -> unchanged authority/security/idempotency rules
```

## Next pending concepts

`101` API design is next at PDF page `198`. Archive entries `101-123` and the complete URL section remain unprocessed in this sequential audit.
