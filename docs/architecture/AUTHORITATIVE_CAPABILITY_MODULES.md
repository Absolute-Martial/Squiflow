# Authoritative Capability Modules

**Status:** Accepted architecture direction  
**Version:** v0.1.0
**Base:** `d6df80a2613a330b7abd3adce32ca19ab20f5d1b`

## 1. Decision

SquiFlow has multiple backend workload hosts but **one authoritative implementation of each business capability**.

`SquiFlow.Web.Api`, `SquiFlow.Sync.Api`, `SquiFlow.Worker`, and future tenant-facing server hosts do not own separate Orders, Customers, Inventory, Payments, or other business implementations. They enter the same authoritative capability modules through workload-specific entry paths.

```text
                           SQUIFLOW
                              │
                 Shared business definitions
                              │
              ┌───────────────┴────────────────┐
              │                                │
      Web/API workload                  Workstation workload
              │                                │
              ▼                                ▼
  SquiFlow.Web.Api                    SquiFlow.Sync.Api
    ASP.NET Core                        ASP.NET Core
              │                                │
 REST/task HTTP/optional GraphQL       gRPC/batch sync when proven
 interactive request policy            device/sync workload policy
              │                                │
              └───────────────┬────────────────┘
                              ▼
══════════════════════════════════════════════════════════
          AUTHORITATIVE APPLICATION BOUNDARY
══════════════════════════════════════════════════════════
                              │
                              ▼
                 Authoritative Capability Modules
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
      Orders               Customers             Inventory
        │                     │                     │
  application/use cases  application/use cases  application/use cases
        │                     │                     │
      domain                domain                domain
        │                     │                     │
        └─────────────────────┼─────────────────────┘
                              │
                    module-owned persistence
                              │
                              ▼
                     PostgreSQL + Outbox
                              │
                              ▼
                         Worker Host
```

The API hosts are separated for workload isolation and scaling. They are **not separate business backends** and do not create separate sources of truth.

## 2. The authoritative boundary

Above the authoritative application boundary are transport/admission concerns such as:

- HTTP/gRPC protocol framing;
- serialization and compression;
- authentication/session/device establishment;
- coarse request limits and payload limits;
- sync batch/cursor/checkpoint framing;
- per-device/tenant fairness and backpressure;
- correlation and transport error mapping.

Below the boundary are authoritative business concerns such as:

- current resource/business authorization where required;
- current business rules and policy versions;
- business invariants;
- current authoritative facts;
- state transitions;
- idempotency/concurrency decisions;
- authoritative transaction boundaries;
- outbox creation when asynchronous consequences exist.

A host may reject malformed/unauthenticated/over-budget transport work before entering the module, but it must not independently reimplement the capability's business meaning.

## 3. Different entry use cases, same module

Different hosts may legitimately invoke different application use cases because the input and trust state differ.

### Web interactive path

```text
POST /orders
    │
    ▼
WebApi transport/admission
    │
    ▼
Orders.CreateOrder
    │
    ├── current authority
    ├── current rules/facts
    ├── invariants
    ├── concurrency/idempotency
    └── authoritative commit
```

### Workstation synchronization path

```text
OperationEnvelope<CreateOrder>
    │
    ▼
SyncApi protocol/device/batch admission
    │
    ▼
Orders.AdmitProvisionalOrder
    │
    ├── deduplicate OperationId
    ├── current authority
    ├── compare dependency revisions
    ├── selectively re-evaluate changed facts
    ├── invariants/concurrency
    └── authoritative commit
```

`CreateOrder` and `AdmitProvisionalOrder` are different entry use cases, but both belong to the **same Orders module**, use the same Order meaning/invariants/policies, and commit to the same PostgreSQL authority.

Bad:

```text
Web.Api  -> WebOrderService
Sync.Api -> SyncOrderService
Worker   -> WorkerOrderService
```

when those services independently encode what an Order means.

Accepted:

```text
Web.Api -----┐
Sync.Api ----┼----> Orders authoritative module
Worker ------┤
Admin path --┘
```

## 4. Worker and Scheduler follow the same rule

The Worker is not a third business implementation.

```text
Outbox / durable job
        │
        ▼
      Worker
        │
        ▼
 owning authoritative module
        │
        ▼
 PostgreSQL / object store effect
```

A worker may invoke a background-specific entry use case such as `Orders.ExpireOrder` or `Payments.ReconcilePayment`, but the operation remains owned by the corresponding module.

The Scheduler owns **when** a durable occurrence should exist, not the mutation itself:

```text
Scheduler
   -> durable occurrence/job
   -> Worker
   -> authoritative module
   -> authoritative persistence
```

## 5. Relationship to Capability Core

SquiFlow uses two related but distinct concepts.

### Capability Core

The host-neutral/deterministic business meaning that can safely be reused across execution environments.

Examples:

- value objects and business state models;
- deterministic calculations;
- deterministic validation/policies;
- operation/fact/decision contracts;
- stable feature/permission/setting definitions;
- domain/business events.

It must remain free of Avalonia, ASP.NET Core, EF/Npgsql/SQLite provider APIs, ZITADEL/OpenFGA SDKs, TickerQ, Proto.Actor, MassTransit/RabbitMQ, and provider-specific infrastructure SDKs.

### Authoritative capability application

The server-side application/use-case layer that uses current authority/current facts and performs the authoritative transaction.

It may orchestrate:

- authoritative fact providers;
- authorization abstractions;
- PostgreSQL persistence/transactions;
- current rule/configuration revisions;
- idempotency/concurrency;
- outbox persistence;
- provider effects through explicit boundaries.

The relationship is:

```text
                  Capability Core
                        ▲
                        │
             authoritative application
                        ▲
          ┌─────────────┼─────────────┐
          │             │             │
       WebApi        SyncApi        Worker
```

The Capability Core is not itself a network service and does not imply a mandatory `CoreApi` hop.

## 6. Workstation preparation is not server authority

The Workstation can use deterministic capability code to prepare an operation and maintain an immediate provisional projection for offline continuity, but that result is not an authoritative business transition where central authority matters.

```text
Workstation
  local facts/snapshots
       │
       ▼
 Capability Core preparation
       │
 provisional projection
       │
 SQLite + local outbox
       │
 OperationEnvelope
       │
       ▼
 SyncApi
       │
       ▼
 authoritative module admission/commit
       │
       ▼
 PostgreSQL authority
```

This supersedes simplistic wording such as “execute the complete transaction twice.” The accepted model is **prepare and project locally → admit and commit authoritatively once → reconcile locally**.

The operation envelope carries one semantic operation identity, intent and revision/dependency evidence needed for safe admission. It is not a second full copy of the database and it does not authorize the server to trust client-computed state. The operation-owned admission strategy determines whether the server validates and commits, enforces an expected revision, performs a proven convergent merge, verifies a bounded delegation, or requires an online server operation. The canonical details are owned by `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`.

## 7. Reads are also module-owned

The common authoritative module is not mutation-only. Reads remain first-class module operations.

```text
GET /orders/{id}
   -> WebApi
   -> Orders.GetOrder
   -> module-owned query/projection path
   -> PostgreSQL
   -> DTO/result
```

Sync pull/download likewise uses synchronization/module-owned authoritative query paths rather than controllers directly reaching arbitrary tables.

An optimized read projection does not become a second authority. PostgreSQL remains the authoritative business source, and derived representations must define source/freshness/rebuild semantics.

## 8. Persistence ownership

Persistence belongs to the capability/application boundary rather than to the API host.

Do not create:

```text
WebApi DB -> Core DB
SyncApi DB -> Core DB
```

for business authority.

SquiFlow distinguishes:

- **PostgreSQL:** authoritative server business state and suitable durable processing state;
- **SQLite/WAL:** Workstation-local/provisional/pending state;
- **object storage:** durable large objects with authoritative metadata;
- **ephemeral cache/runtime state:** disposable and rebuildable;
- **outbox/inbox/jobs/idempotency state:** durable processing state with explicit ownership/recovery.

Provider-specific persistence code should be capability-owned or infrastructure-adapter code and must not leak into host-neutral Capability Core projects.

## 9. Module structure

Logical target shape:

```text
modules/orders/
├── SquiFlow.Orders.Domain/
│   ├── Orders/
│   ├── ValueObjects/
│   ├── Policies/
│   ├── Rules/
│   └── Events/
├── SquiFlow.Orders.Contracts/
│   ├── Commands/
│   ├── Queries/
│   ├── Results/
│   ├── IntegrationEvents/
│   ├── Permissions/
│   ├── Features/
│   └── Settings/
├── SquiFlow.Orders.Application/
│   ├── Interactive/
│   ├── Admission/
│   ├── Queries/
│   ├── Background/
│   ├── Validation/
│   └── Ports/
└── adapters only when earned/
    ├── SquiFlow.Orders.Postgres/
    ├── SquiFlow.Orders.Sqlite/
    ├── SquiFlow.Orders.Workstation/
    ├── SquiFlow.Orders.Web/
    └── SquiFlow.Orders.Api/
```

This is a **logical responsibility model**, not an instruction to scaffold every project immediately. Small modules may remain compact until dependency pressure/cross-host reuse makes a compile-time split valuable.

## 10. Dependency direction

```text
Foundation
   ↑
Capability Core / Domain / Contracts
   ↑
Application use cases
   ↑
Host/provider adapters
   ↑
Executable composition roots
```

Cross-module dependencies should target another module's public contracts/application surface, not its persistence/provider internals or tables.

Do not use a generic repository-wide `Shared`/`Common` project to hold Customers, Orders, Inventory, Payments, or other capability business meaning.

## 11. Deployment duplication is not business duplication

It is expected that multiple server hosts may package the same compiled module assembly:

```text
WebApi deployment
├── SquiFlow.WebApi.dll
└── SquiFlow.Orders.dll

SyncApi deployment
├── SquiFlow.SyncApi.dll
└── SquiFlow.Orders.dll
```

That is binary/deployment duplication, not source/business-logic duplication.

Do not introduce a mandatory network `CoreApi` service solely to avoid shipping the same module assembly in multiple hosts. In-process module execution remains the modular-monolith default.

## 12. Architectural invariant

> **One capability, one authoritative business meaning; many workload-specific entry hosts.**

WebApi, SyncApi, Worker, and future server hosts may differ in protocol, batching, rate/backpressure, session/device context, and execution timing. They must converge on the same authoritative capability modules and the same authoritative PostgreSQL state.
