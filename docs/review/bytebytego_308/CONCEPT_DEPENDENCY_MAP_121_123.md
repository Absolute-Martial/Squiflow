# ByteByteGo Concept Dependency Map — Entries 121-123 / Archive Completion

**Coverage:** archive entries `121-123`, plus the sequential transition through structural PDF pages `242-244`. Together with prior maps, the archive concept map is current through all `123` archive occurrences.

## 1. Protocol family -> exact SquiFlow boundary

```text
121 protocol map
    -> DNS
        naming / custom-domain routing
    -> TLS + HTTP
        ordinary secure Web/API transport
    -> OIDC/OAuth
        ZITADEL human identity
    -> gRPC
        Workstation sync / real typed RPC candidate
    -> WebSocket/SSE/polling
        live UI signaling candidate
    -> SSH/private networking
        operator recovery
    -> NTP/time health
        certificates/tokens/leases/schedules/diagnostics
    -> SMTP/SFTP/MQTT/VPN/LDAP/etc.
        requirement-triggered integrations/infrastructure

critical rule
    -> protocol reachability/authentication
       != TenantContext/OpenFGA/domain authority
```

## 2. Transport versions -> unchanged application correctness

```text
122 TCP / UDP / QUIC
    -> underlying transport properties

HTTP/1.1 / HTTP/2 / HTTP/3
    -> Web/API transport versions

HTTPS
    -> TLS-protected HTTP family

SquiFlow command semantics
    -> authentication
    -> tenant scope
    -> resource authorization
    -> semantic idempotency
    -> concurrency
    -> compatibility

critical rule
    -> transport upgrade may improve latency/connection behavior
       but does not fix business correctness
```

## 3. Live/file/email protocols -> fit by workload

```text
live UI
    -> polling / SSE / WebSocket/SignalR
    -> reconstruct from durable state after loss

email
    -> SMTP or provider HTTPS API
    -> provider/delivery/duplicate/OutcomeUnknown contract decides

file exchange
    -> HTTPS/object storage / SFTP / FTPS / legacy FTP
    -> partner compatibility + security + dedupe + recovery decide

raw TCP/UDP
    -> only for concrete device/protocol need
```

## 4. Microservice source -> boundary interrogation

```text
123 microservice practices
    -> separate data ownership
    -> independent build/deploy
    -> cohesive responsibility
    -> stateless process semantics
    -> DDD boundary reasoning
    -> optional container/orchestration/frontend split

SquiFlow current
    -> modular-monolith business core
    -> in-process module calls
    -> Core API / Admin API / Worker / Guard process boundaries
       only where runtime/security/supervision responsibility is real
    -> shared central DB permitted for hosts of same modular core
       with explicit module/data ownership
```

## 5. Extraction gate

```text
candidate module
    -> measured independent scaling problem?
    -> fault/security isolation requirement?
    -> independent release/team ownership?
    -> specialized runtime/hardware?
    -> residency/compliance boundary?

if no
    -> keep in-process/module/process boundary as appropriate

if yes
    -> define invariant/capability
    -> authoritative data ownership
    -> API/event contract
    -> cross-boundary consistency/idempotency
    -> auth/tenant propagation + revalidation
    -> version compatibility
    -> deployment/discovery/observability
    -> backup/restore/reconciliation
    -> rack/team operational cost
    -> rollback/reintegration path
```

## 6. Anti-distributed-monolith rule

```text
many services
+ shared-table writes
+ synchronized deployment
+ long synchronous chains
+ ordinary distributed transactions
+ one bottlenecked dependency

    -> not meaningful independence
    -> distributed-monolith risk
```

## 7. Archive -> URL transition

```text
archive occurrences 001-123
    -> all independently reviewed
    -> PDF pages 5-241 complete

structural pages 242-244
    -> re-inspected sequentially
    -> 64 supplied URL entries confirmed

next
    -> URL 001 on PDF page 245
    -> every URL occurrence independently reviewed
    -> archive overlap never auto-completes URL row
```

## 8. Current communication portfolio after archive review

```text
same runtime
    -> in-process

ordinary Web/business API
    -> HTTPS + REST/task HTTP

complex client-selected read need
    -> GraphQL candidate

Workstation sync / real typed RPC
    -> HTTP baseline + gRPC preferred candidate when POC earns it

live UX
    -> polling/SSE/WebSocket/SignalR by screen requirement

long-running consequence
    -> transactional outbox + Worker

future replayable stream
    -> Kafka-like stream candidate if retained history/offsets/throughput are real

future broker-managed work/routing
    -> RabbitMQ-like broker candidate if queue/routing semantics are real
```

The map does not declare technology winners. It records the problem surface, authority boundary, reason, alternative fit, operational cost, evidence gate, and falsification trigger.
