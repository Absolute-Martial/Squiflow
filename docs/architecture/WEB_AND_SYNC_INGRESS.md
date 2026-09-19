# Web API and Workstation Sync Hosts

**Status:** CoreApi is the current compact tenant/business HTTP host. It has no tenant business operations yet. SyncApi and Worker remain `NOT_INTRODUCED`; the separate-host flows below become active only when their workloads are implemented.

## 1. Decision

When Workstation synchronization is introduced, SquiFlow separates interactive Web/API traffic from synchronization traffic. The current CoreApi owns the interactive tenant/business API role. A future project rename from CoreApi to WebApi may improve naming, but it replaces that host; SquiFlow does not keep both as forwarding layers.

In this document, `WebApi` means the interactive Web/API workload role currently seeded by CoreApi. `WebApi` and `SyncApi` are **server hosts/API adapters**, not separate business backends and not merely one-way ingress pipes. They may accept commands and return/read data. They differ because their protocol, identity context, batching, cursor, backpressure, fairness, latency and scaling profiles differ.

They invoke the same capability-owned business modules.

```text
                         Cloud/edge
                             |
              +--------------+--------------+
              |                             |
              v                             v
        SquiFlow.CoreApi              SquiFlow.SyncApi
        interactive/Web role          workstation sync host
              |                             |
              +--------------+--------------+
                             |
                             v
                    business modules
              Orders / Customers / Inventory / ...
                             |
                 commands / queries / admission
                             |
                             v
                   module-owned data access
                             |
                             v
                        PostgreSQL
                 authoritative business state
```

CoreApi exists for its currently declared narrow scope. The SyncApi, business capabilities and persistence paths in this diagram remain future earned responsibilities.

Ephemeral runtime state and durable processing state are used around this path where the workload requires them; they do not form a second business backend.

Detailed owners:
- `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`;
- `docs/server/SERVER_STATE_AND_PROCESSING.md`.

## 2. Why separate the hosts

Interactive Web requests and Workstation sync have materially different workload shapes once both workloads exist.

### Web/API host

Optimized for:

- low-latency user interaction;
- task-oriented REST/HTTP endpoints;
- Web administration and tenant business operations;
- ordinary request/response payloads;
- current user/session authorization context;
- read/query workloads and authoritative commands.

### Sync host

Optimized for:

- device authentication/session validation;
- bounded operation batches;
- idempotency and operation receipts;
- sync cursor/checkpoint handling;
- revision/dependency evidence;
- selective authoritative admission;
- compression/streaming where justified;
- per-device/tenant fairness;
- explicit backpressure/retry-after;
- pull/download and upload/admission flows;
- longer-lived or higher-throughput synchronization workloads.

The Sync API must not force these concerns into every ordinary interactive Web endpoint when the split is implemented.

## 3. Hosts do not own duplicate business modules

Bad:

```text
WebApi
  -> WebOrderService

SyncApi
  -> SyncOrderService

Worker
  -> WorkerOrderService
```

when those classes independently encode Order meaning.

Accepted illustrative shape:

```text
WebApi
  -> Orders.CreateOrder --------+
                                |
SyncApi                         +-> one Orders capability source
  -> Orders.AdmitCreateOrder ---+
                                |
Worker                          |
  -> Orders.ExpireOrder --------+
```

The entry use cases may differ because their trust/workflow state differs. The business capability remains one implementation.

The same compiled module assembly may later be deployed with more than one host. That is binary duplication, not business-logic duplication.

## 4. Web data read path

When WebApi exists, it can and should return data through module-owned queries.

```text
GET /orders/123
  -> WebApi
  -> authenticate / TenantContext / coarse request policy
  -> Orders.GetOrder
  -> Orders-owned query/data-access code
  -> PostgreSQL
  -> OrderDetails DTO
  -> WebApi response
```

The API does not need ad-hoc SQL in controllers to read data.

Read-only query code may use an optimized projection/query path and does not have to reconstruct a rich aggregate when no mutation occurs. Tenant isolation, authorization and data ownership still apply.

## 5. Web authoritative mutation path

For a future short operation where the user needs the authoritative result now:

```text
Web
 -> WebApi
 -> authenticate / TenantContext / generic admission
 -> Orders.CreateOrder
 -> current resource/business authorization
 -> authoritative facts/rules
 -> business invariants
 -> concurrency/idempotency
 -> authoritative transaction
 -> PostgreSQL
 -> authoritative response
```

This is not `controller -> SQL` and it is not necessary to queue every command.

## 6. Sync upload/admission path

When synchronization is introduced, the accepted shape is:

```text
Workstation
  -> SyncApi
  -> device/session/protocol/batch/backpressure checks
  -> Orders.AdmitCreateOrder
  -> semantic idempotency
  -> current resource/business authorization
  -> revision/dependency comparison
  -> selective re-evaluation where needed
  -> authoritative transaction
  -> PostgreSQL
  -> authoritative receipt
```

SyncApi owns sync transport/workload policy. The owning module owns what an Order operation means and how it is authoritatively admitted.

## 7. Sync pull/download path

Sync is not only upload.

```text
Workstation
  -> SyncApi
  -> Synchronization capability
  -> cursor/checkpoint/change-feed query
  -> module-owned authoritative records/projections
  -> PostgreSQL/outbox/change records
  -> bounded response
```

Cross-capability cursor/batch/reconnect mechanics belong to the Synchronization capability/host path rather than being duplicated inside every business module.

## 8. Three server-state classes

When server state is introduced, distinguish:

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

## 9. Web/Cloud persistence boundary

Persistent SQLite is **not** an alternative persistence model for SquiFlow Web or Cloud.

Accepted future placement:

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

Do not create per-user or per-tenant SQLite replicas in Web clients or cloud service instances. Browser storage is not authoritative business state or a durable offline business outbox under the current architecture.

Cloud hosts may use bounded cache, temporary/object staging, durable inbox/job/idempotency/outbox state, and Worker processing without creating a second general-purpose business database once those hosts/workloads exist.

If SquiFlow later chooses a truly offline-capable PWA/Web client with durable business operations, that requires a new explicit authority, synchronization, security, migration and recovery decision.

## 10. Durable asynchronous path

For long-running/resource-heavy work or asynchronous consequences once such work exists:

```text
WebApi / SyncApi
 -> authenticate / authorize / validate / idempotency
 -> durable operation or job
 -> accepted/operation identifier

Worker
 -> claim durable work
 -> owning module operation
 -> PostgreSQL and/or object storage
 -> persist result/status
```

Typical examples include document generation, reports/exports, imports, image processing, notifications, integrations and reconciliation.

When an authoritative business transaction has already committed and only consequences remain, prefer business transaction + outbox atomicity followed by Worker processing.

## 11. Sync durable staging and transport acceptance

Do not model reconnect traffic as:

```text
Workstation -> SyncApi -> immediately hammer arbitrary business tables
```

The future host performs device/session validation, protocol/schema checks, item/byte/rate limits, fairness and backpressure first.

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
 -> owning module authoritative checks
 -> PostgreSQL commit
 -> AuthoritativeReceipt
```

If an asynchronous path returns `202 Accepted`, `Received`, or equivalent before business admission completes, that means only that the server durably received/staged the operation. It does not mean the business operation is authoritatively committed.

Later authoritative results remain explicit, for example:

```text
Accepted
Adjusted
Rejected
Conflict
AuthorizationChanged
Retryable
AlreadyApplied
```

## 12. Scaling and failure independence

The reason to earn separate hosts is that they can scale/throttle independently when the workload exists.

```text
Web traffic normal + reconnect backlog high
-> scale/throttle SyncApi path independently

Web reporting/dashboard spike + sync normal
-> scale WebApi path independently
```

A future SyncApi overload should be throttled/backpressured rather than consuming all interactive Web capacity. A future WebApi failure must not imply that already-running local Workstation operation stops; Workstations continue according to the local-first contract and synchronize later.

Ephemeral cache/runtime-store failure may reduce performance or UX, but it must not erase committed business truth or durable jobs.

## 13. Protocol selection

- REST/task-oriented HTTP remains the ordinary Web/external API baseline when that surface exists.
- gRPC remains a preferred candidate for the Workstation synchronization boundary when a real implementation POC proves its value for batching/streaming/contract generation.
- The sync semantics are transport-independent; HTTP can remain a fallback/initial transport if it is simpler during early proof.
- GraphQL is not required merely because a Web host exists.

## 14. Security boundary

When WebApi and SyncApi exist, both independently enforce server authority appropriate to their requests.

WebApi derives the current authenticated user/TenantContext and performs host/coarse policy checks, while current resource/business authorization remains part of the authoritative module/application operation where required.

SyncApi additionally validates device/workstation identity and treats all submitted local operations/receipts as untrusted client input. Workstation never receives central database or OpenFGA administrative credentials.

A durable inbox/job/cache does not weaken this boundary. Deferred actor operations are reauthorized at execution when their semantics require current actor authority; committed consequences execute from the already-authoritative business fact according to the Worker authorization classification.

## 15. Repository direction

Accepted future host locations are conceptually:

```text
services/
|- core-api/    # current interactive/tenant-business API composition host
|- sync-api/    # create only when an isolated synchronization host is real
`- worker/      # create only when the first durable background workload earns it
```

There is currently one `services/core-api/SquiFlow.CoreApi` project. It must evolve into, or be renamed as, the interactive Web/API host rather than becoming an extra network hop in front of another WebApi. SyncApi and Worker remain absent until a real synchronization or durable-work slice earns them.

Do not scaffold empty hosts, cache providers, brokers, processing databases, or per-host business modules only to satisfy this diagram.
