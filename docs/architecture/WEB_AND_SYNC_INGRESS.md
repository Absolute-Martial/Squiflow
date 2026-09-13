# Web API and Workstation Sync Ingress

**Status:** Accepted architecture direction

## 1. Decision

SquiFlow uses separate backend ingress/workload hosts for interactive Web/API traffic and Workstation synchronization traffic.

This is an operational/runtime separation, not a duplication of business logic or authoritative state.

The ingress hosts are also **not** defined as thin `HTTP -> PostgreSQL` facades. They sit in front of application/capability processing and use explicit ephemeral runtime state and durable processing state where the workload requires it.

```text
                         Cloud/edge
                             |
              +--------------+--------------+
              |                             |
              v                             v
        SquiFlow.WebApi               SquiFlow.SyncApi
        interactive API               workstation sync ingress
              |                             |
              +--------------+--------------+
                             |
            +----------------+----------------+
            |                                 |
            v                                 v
   ephemeral runtime state          durable processing state
   cache/rate/session/temp           inbox/jobs/idempotency/outbox
            |                                 |
            +----------------+----------------+
                             |
                             v
                  application use cases
                             |
                             v
                     Capability Cores
                             |
                             v
                   persistence adapters
                             |
                             v
                        PostgreSQL
                 authoritative business state
                             |
                           Outbox
                             |
                           Worker
```

Detailed state-placement owner: `docs/server/SERVER_STATE_AND_PROCESSING.md`.

## 2. Why separate the hosts

Interactive Web requests and Workstation sync have materially different workload shapes.

### Web/API ingress

Optimized for:

- low-latency user interaction;
- task-oriented REST/HTTP endpoints;
- Web administration and tenant business operations;
- ordinary request/response payloads;
- current user/session authorization;
- read/query workloads and authoritative commands.

### Sync ingress

Optimized for:

- device authentication/session validation;
- bounded operation batches;
- idempotency and operation receipts;
- sync cursor/checkpoint handling;
- revision comparison;
- selective authoritative admission;
- compression/streaming where justified;
- per-device/tenant fairness;
- explicit backpressure/retry-after;
- longer-lived or higher-throughput synchronization workloads.

The Sync API must not force these concerns into every ordinary interactive Web endpoint.

## 3. Shared authority, different entry paths

Do not implement separate business services such as `WebOrderService` and `SyncOrderService` that independently encode Order meaning.

Instead:

```text
WebApi
  -> CreateOrder authoritative use case ---+
                                           +-> Orders Capability Core
SyncApi                                    |
  -> AdmitProvisionalOrder operation ------+
```

The use cases differ because the inputs and trust state differ. The business capability remains one implementation.

The host supplies transport/admission context. The application/capability path owns processing. Persistence-specific code remains in infrastructure/persistence adapters rather than HTTP endpoints.

## 4. Three server-state classes

The server distinguishes:

```text
Ephemeral runtime state
= memory/cache/rate/session/temp
= safe to lose/rebuild

Durable processing state
= inbox/jobs/idempotency/outbox/operation status
= must survive process loss
= not automatically business authority

Authoritative business state
= PostgreSQL business records/invariants
= final server authority
```

This is a logical separation, not a requirement for three database products.

Durable processing state may initially be PostgreSQL-backed under explicit tables/schemas/ownership when that provides the simplest correct atomicity and recovery model. A broker, Redis-like cache, or dedicated processing store is introduced only when a concrete workload justifies it.

## 5. Web/Cloud persistence boundary

Persistent SQLite is **not** an alternative persistence model for SquiFlow Web or Cloud.

```text
Workstation
  SQLite/WAL local store
  local provisional state + durable outbox

Web browser
  no persistent local business database
  optional disposable UI/session cache only

Cloud WebApi / SyncApi / AdminApi / Worker
  ephemeral runtime state where useful
  durable processing state where required
  PostgreSQL authoritative business state
```

Do not create per-user or per-tenant SQLite replicas in Web clients or cloud service instances. Do not use browser `localStorage`, IndexedDB, service-worker cache, or another browser store as authoritative business state or as a durable offline outbox under the current architecture.

Browser-local storage may be used only for data that can be safely recreated or abandoned, such as presentation preferences, bounded response cache, session-safe UI state, or temporary upload staging where explicitly designed. Deleting that storage must not lose committed or pending business truth.

Cloud hosts may use bounded cache, temporary/object staging, durable inbox/job/idempotency/outbox state, and Worker processing without creating a second general-purpose business database.

If SquiFlow later chooses a truly offline-capable PWA/Web client with durable business operations, that requires a new explicit authority, synchronization, security, migration and recovery decision rather than reusing the Workstation SQLite model implicitly.

## 6. Interactive synchronous path

Do not queue every Web command.

For a short operation where the user needs the authoritative result now:

```text
Web
 -> WebApi
 -> authenticate / authorize / validate
 -> application use case
 -> Capability Core
 -> persistence adapter
 -> PostgreSQL transaction
 -> authoritative response
```

This is not `controller -> SQL`; the owning application/capability path remains between transport and persistence.

## 7. Durable asynchronous path

For long-running/resource-heavy work or asynchronous consequences:

```text
WebApi
 -> authenticate / authorize / validate / idempotency
 -> durable operation or job
 -> 202 Accepted + operation resource

Worker
 -> claim durable work
 -> application use case / Capability Core
 -> PostgreSQL and/or object storage
 -> persist result/status
```

Typical examples include document generation, reports/exports, imports, image processing, notifications, integrations and reconciliation.

When an authoritative business transaction has already committed and only consequences remain, prefer business transaction + outbox atomicity followed by Worker processing.

## 8. Sync admission and durable staging

Do not model reconnect traffic as:

```text
Workstation -> SyncApi -> immediately hammer arbitrary business tables
```

The ingress boundary performs device/session validation, protocol/schema checks, item/byte/rate limits, deduplication, fairness and backpressure first.

Then either:

```text
small bounded operation
 -> synchronous authoritative admission
```

or, when burst isolation/long processing/recovery semantics justify it:

```text
SyncApi
 -> durable sync inbox / operation state
 -> admission processor / Worker
 -> Capability Core + authoritative checks
 -> PostgreSQL commit
 -> AuthoritativeReceipt
```

The durable inbox is processing state. It does not become a second source of business truth.

## 9. Received is not authoritatively accepted

If an asynchronous SyncApi path returns `202 Accepted`, `Received`, or equivalent before business admission completes, that result means only that the server durably received/staged the operation.

It does not mean the business operation is authoritatively committed.

The later result remains explicit, for example:

```text
Accepted
Adjusted
Rejected
Conflict
AuthorizationChanged
Retryable
AlreadyApplied
```

This preserves the Workstation provisional-execution / authoritative-admission model.

## 10. Scaling and failure independence

The ingress hosts can scale independently.

Examples:

```text
Web/API traffic normal
Sync backlog high after outage
-> scale SyncApi without scaling WebApi
```

or:

```text
Web reporting/dashboard spike
sync traffic normal
-> scale WebApi independently
```

A SyncApi overload should be throttled/backpressured rather than consuming all interactive Web capacity. A WebApi failure must not imply that already-running local Workstation operation stops; Workstations continue according to the local-first contract and synchronize later.

Ephemeral cache/runtime-store failure may reduce performance or UX, but it must not erase committed business truth or durable jobs. Durable processing state must survive API/Worker process loss according to its recovery contract.

## 11. Protocol selection

- REST/task-oriented HTTP remains the ordinary Web/external API baseline.
- gRPC remains a preferred candidate for the Workstation synchronization boundary when the implementation POC proves its value for batching/streaming/contract generation.
- The sync semantics are transport-independent; HTTP can remain a fallback/initial transport if it is simpler during early proof.
- GraphQL is not required merely because the Web host exists.

## 12. Security boundary

Both hosts independently enforce server authority appropriate to their requests.

WebApi derives current authenticated user/TenantContext and performs current authorization.

SyncApi additionally validates device/workstation identity and treats all submitted local operations/receipts as untrusted client input. The Workstation never receives central database or OpenFGA administrative credentials.

A durable inbox/job/cache does not weaken this boundary. Deferred actor operations are reauthorized at execution when their semantics require current actor authority; committed consequences execute from the already-authoritative business fact according to the Worker authorization classification.

## 13. Repository direction

Accepted future host locations are conceptually:

```text
services/
|- web-api/     # create when split from the current CoreApi is implemented
|- sync-api/    # create when first isolated synchronization ingress exists
`- worker/      # create when first durable background workload is implemented
```

The existing `services/core-api/SquiFlow.CoreApi` remains the compact early host until a real split is implemented. Documentation must not pretend the split already exists in code.

Do not scaffold empty hosts, cache providers, brokers or processing databases only to satisfy this diagram.
