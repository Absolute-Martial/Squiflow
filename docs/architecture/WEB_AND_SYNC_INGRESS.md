# Web API and Workstation Sync Ingress

**Status:** Accepted architecture direction

## 1. Decision

SquiFlow uses separate backend ingress/workload hosts for interactive Web/API traffic and Workstation synchronization traffic.

This is an operational/runtime separation, not a duplication of business logic or authoritative state.

```text
                         Cloud/edge
                             │
              ┌──────────────┴──────────────┐
              │                             │
              ▼                             ▼
        SquiFlow.WebApi               SquiFlow.SyncApi
        interactive API               workstation sync ingress
              │                             │
              └──────────────┬──────────────┘
                             ▼
                  authoritative capabilities
                             │
                         PostgreSQL
                             │
                           Outbox
                             │
                           Worker
```

Both hosts compose the same Capability Cores and authoritative server application paths.

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
  └─ CreateOrder authoritative use case ─┐
                                         ├─ Orders Capability Core
SyncApi                                  │
  └─ AdmitProvisionalOrder operation ────┘
```

The use cases differ because the inputs and trust state differ. The business capability remains one implementation.

## 4. Web/Cloud persistence boundary

Persistent SQLite is **not** an alternative persistence model for SquiFlow Web or Cloud.

```text
Workstation
  SQLite/WAL local store
  local provisional state + durable outbox

Web browser
  no persistent local business database
  optional disposable UI/session cache only

Cloud WebApi / SyncApi / AdminApi / Worker
  PostgreSQL authoritative persistence
```

Do not create per-user or per-tenant SQLite replicas in Web clients or cloud service instances. Do not use browser `localStorage`, IndexedDB, service-worker cache, or another browser store as authoritative business state or as a durable offline outbox under the current architecture.

Browser-local storage may be used only for data that can be safely recreated or abandoned, such as presentation preferences, bounded response cache, session-safe UI state, or temporary upload staging where explicitly designed. Deleting that storage must not lose committed or pending business truth.

If SquiFlow later chooses a truly offline-capable PWA/Web client with durable business operations, that requires a new explicit authority, synchronization, security, migration and recovery decision rather than reusing the Workstation SQLite model implicitly.

## 5. Scaling and failure independence

The ingress hosts can scale independently.

Examples:

```text
Web/API traffic normal
Sync backlog high after outage
→ scale SyncApi without scaling WebApi
```

or:

```text
Web reporting/dashboard spike
sync traffic normal
→ scale WebApi independently
```

A SyncApi overload should be throttled/backpressured rather than consuming all interactive Web capacity. A WebApi failure must not imply that already-running local Workstation operation stops; Workstations continue according to the local-first contract and synchronize later.

## 6. Protocol selection

- REST/task-oriented HTTP remains the ordinary Web/external API baseline.
- gRPC remains a preferred candidate for the Workstation synchronization boundary when the implementation POC proves its value for batching/streaming/contract generation.
- The sync semantics are transport-independent; HTTP can remain a fallback/initial transport if it is simpler during early proof.
- GraphQL is not required merely because the Web host exists.

## 7. Security boundary

Both hosts independently enforce server authority appropriate to their requests.

WebApi derives current authenticated user/TenantContext and performs current authorization.

SyncApi additionally validates device/workstation identity and treats all submitted local operations/receipts as untrusted client input. The Workstation never receives central database or OpenFGA administrative credentials.

## 8. Repository direction

Accepted future host locations are conceptually:

```text
services/
├── web-api/     # create when split from the current CoreApi is implemented
└── sync-api/    # create when first isolated synchronization ingress exists
```

The existing `services/core-api/SquiFlow.CoreApi` remains the compact early host until a real split is implemented. Documentation must not pretend the split already exists in code.

Do not scaffold empty hosts only to satisfy this diagram.
