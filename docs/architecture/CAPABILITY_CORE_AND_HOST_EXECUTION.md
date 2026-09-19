# Capability Core and Host-Specific Execution

**Status:** Accepted architecture direction  
**Version:** v0.1.0

## 1. Vocabulary

SquiFlow does not use a generic `shared modules` layer. The stable model is:

```text
Foundation
   ↓
Capability-owned business meaning
   ├── Capability Core (host-neutral/deterministic where reusable)
   └── Authoritative Application (server current authority/facts/commit)
   ↓
Host / infrastructure adapters
```

- **Foundation** contains small product-wide technical/domain primitives with no business-capability ownership.
- **Capability Core** owns the host-neutral/deterministic center of one business capability where such reusable logic exists.
- **Authoritative Application** owns server-side authoritative entry use cases, current authority/facts, admission, concurrency/idempotency and commit orchestration for that capability.
- **Host/infrastructure adapters** supply transport, presentation, provider/persistence integration and host-specific effects.

Examples of capabilities are Customers, Orders, Inventory, Quotations, Staff and Devices.

Detailed server-authority owner: `docs/architecture/AUTHORITATIVE_CAPABILITY_MODULES.md`.

## 2. One capability, one business meaning

A capability has one source implementation of its business meaning. Do not create parallel business implementations such as:

```text
Orders.WorkstationBusiness
Orders.ServerBusiness
Orders.WebBusiness
Orders.SyncBusiness
Orders.WorkerBusiness
```

For deterministic decisions that are genuinely reusable, model the processor once:

```text
Intent + Facts + Rule/Policy Snapshot
              ↓
       Capability Core
              ↓
           Decision
```

The execution path supplies facts and performs effects.

For Orders:

```text
                    Orders Capability
                          │
                  Capability Core
                    /           \
                   /             \
                  v               v
       local facts/SQLite   authoritative facts/PostgreSQL
                  │               │
       Workstation execution   authoritative application
```

The deterministic processor may be the same code even though authority and persistence differ.

## 3. Same authoritative modules behind WebApi and SyncApi

Interactive Web/API traffic and Workstation synchronization are separate backend workload hosts, but they converge after transport/admission into the same authoritative modules.

```text
WebApi -----┐
            ├──> Orders / Customers / Inventory / ... authoritative modules
SyncApi ----┘                         │
                                     ▼
                              PostgreSQL + Outbox
```

`WebApi` and `SyncApi` may invoke different entry use cases because their inputs/trust states differ. Example:

```text
WebApi  -> Orders.CreateOrder
SyncApi -> Orders.AdmitProvisionalOrder
Worker  -> Orders.ExpireOrder
```

These entry points belong to one Orders capability and share its business meaning/invariants. They are not separate Order implementations.

The API hosts own protocol/workload concerns. The authoritative module owns current business authorization where required, current facts/rules, invariants, concurrency/idempotency and authoritative commit.

## 4. What belongs in a Capability Core

A Capability Core may own:

- entities/value objects where they protect real business meaning;
- operation/intention contracts;
- fact contracts;
- deterministic validation and calculations;
- business decision results;
- deterministic rules/policies;
- stable feature definitions;
- stable permission definitions;
- stable setting definitions;
- domain/business events.

A Capability Core must not depend on host/provider technologies such as:

- Avalonia;
- ASP.NET Core;
- Windows APIs;
- EF Core/Npgsql/SQLite provider APIs;
- OpenFGA/ZITADEL SDKs;
- Quartz/TickerQ;
- Proto.Actor;
- MassTransit/RabbitMQ;
- provider-specific telemetry/storage SDKs.

`net10.0` shared projects remain the baseline. .NET 10 LTS is the selected runtime baseline for the current implementation.

## 5. Host/application adapters

### Workstation execution

May own:

- Avalonia presentation and ViewModels;
- local fact providers backed by SQLite/local snapshots;
- provisional execution orchestration;
- local persistence/outbox;
- local hardware/platform integration;
- local process/runtime integration.

### Server authoritative application

May own/orchestrate:

- authoritative fact providers;
- current authorization through appropriate abstractions/integration;
- PostgreSQL transaction/concurrency boundaries;
- current rule/configuration versions;
- semantic idempotency;
- authoritative admission/commit;
- outbox creation;
- provider effects through explicit boundaries.

### Web presentation adapter

May own:

- Web-specific pages/components/presentation state;
- task-oriented API client calls;
- Web-only administration UX where appropriate.

Web presentation does not contain another business implementation.

The Web client does **not** own a persistent local business database. SQLite/WAL is a Workstation-only persistence choice. Browser storage, if used, is limited to disposable UI/session cache or temporary transfer state that can be deleted without losing authoritative or pending business truth. Offline-authoritative Web/PWA persistence requires a separate explicit architecture decision.

### API hosts/adapters

`WebApi` and `SyncApi` are different server workload/API adapters into the same authoritative modules. They may both support commands and reads. They differ in protocol, identity/device context, batching, cursor, fairness, rate/backpressure and scaling concerns without duplicating business meaning.

## 6. Processing modes

Each capability operation declares/documents the execution mode that applies on a host:

```text
DeviceLocal
LocalProvisional
ServerAuthoritative
```

- **DeviceLocal** exists only on the Workstation/device and has no server business effect.
- **LocalProvisional** may execute locally for offline continuity and later requires authoritative admission.
- **ServerAuthoritative** requires current server authority and does not become valid merely because a client UI exposed it.

Examples:

| Capability | Workstation | Web |
|---|---|---|
| Create order | LocalProvisional where allowed | ServerAuthoritative |
| Create/update customer | LocalProvisional where allowed | ServerAuthoritative |
| Assign staff permissions | ServerAuthoritative | ServerAuthoritative |
| Register/revoke device | ServerAuthoritative | ServerAuthoritative |
| SQLite maintenance | DeviceLocal | unavailable |
| Printer/scanner integration | DeviceLocal | unavailable |
| Guard/update recovery | DeviceLocal | unavailable |

## 7. Provisional execution is not blind double processing

The accepted Workstation flow is:

```text
local intent
  -> deterministic local execution
  -> SQLite + local outbox
  -> semantic OperationEnvelope + revision evidence
  -> SyncApi
  -> authoritative admission
  -> fast path or selective re-evaluation
  -> PostgreSQL authoritative commit
```

The server does not trust a local result merely because the same rule version was used. Current security/authority and protected invariants are still enforced. Revision evidence exists to avoid unnecessary recomputation, not to transfer authority to the device.

## 8. Presentation sharing rule

Share business/domain contracts and deterministic processing, not UI framework state.

Do not share one Avalonia/Web ViewModel merely to increase reuse. Workstation and Web can share:

- operation inputs;
- query/read contracts where semantically identical;
- validation definitions;
- feature/permission/setting definitions;
- domain and decision logic.

They keep host-specific presentation state and navigation separate.

## 9. Repository naming and project boundaries

The repository category is `foundation/`, not `BuildingBlocks`, `Common`, or a universal `Shared` bucket.

A small capability may remain compact:

```text
modules/orders/
└── SquiFlow.Orders/
    ├── Domain/
    ├── Application/
    ├── Admission/
    ├── Queries/
    ├── Decisions/
    ├── Rules/
    ├── Contracts/
    └── Events/
```

If real cross-host reuse/provider isolation/dependency pressure earns a physical split, an explicit later shape may be:

```text
modules/orders/
├── SquiFlow.Orders.Domain/
├── SquiFlow.Orders.Contracts/
├── SquiFlow.Orders.Application/
├── SquiFlow.Orders.Postgres/
├── SquiFlow.Orders.Sqlite/
├── SquiFlow.Orders.Workstation/
├── SquiFlow.Orders.Web/
└── SquiFlow.Orders.Api/
```

This is not a mandate to create empty projects. Physical decomposition is earned by real compile-time/provider/platform/reuse/packaging pressure.

## 10. Worker and Scheduler ownership

Worker invokes the same authoritative capability modules; it does not own duplicate business services.

Scheduler owns when work should become durable, not the mutation:

```text
Scheduler
  -> durable occurrence/job
  -> Worker
  -> owning authoritative module
  -> PostgreSQL/object-storage effect
```

## 11. Reads and persistence ownership

Reads remain module-owned operations. API endpoints do not bypass module ownership with arbitrary SQL merely because they can reach PostgreSQL.

Optimized read-only projections are allowed where they preserve tenant/security/data ownership and do not become a second business authority.

Persistence/provider code lives behind the module/application boundary. PostgreSQL is authoritative server business state; SQLite/WAL is Workstation-local/provisional state; object storage owns large objects; caches are disposable; durable processing state has explicit ownership/recovery.

## 12. Mechanical enforcement

Architecture tests should enforce project boundaries that actually exist and keep Foundation/physically separated Capability Core code free of forbidden host/provider dependencies.

The intended dependency direction is:

```text
Foundation
   ↑
Capability business meaning / Capability Core
   ↑
Authoritative application + host/provider adapters
   ↑
Executable composition roots
```

Modular ownership is mandatory; physical project decomposition is earned.
