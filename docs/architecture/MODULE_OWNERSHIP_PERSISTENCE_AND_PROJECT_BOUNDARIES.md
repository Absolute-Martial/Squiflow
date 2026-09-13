# Module Ownership, Persistence, and Project Boundaries

**Status:** Accepted architecture direction

## 1. Decision

SquiFlow has **one source implementation for each business capability**. `WebApi`, `SyncApi`, `Worker`, and `AdminApi` are runtime hosts/adapters that enter those capabilities through different transport and workload paths; they do not contain separate Orders, Customers, Inventory, or other business implementations.

The terms `Core`, `Server`, and `Workstation` describe **responsibility categories first**. They do not automatically require separate `.csproj` projects.

A compile-time split is created only when it gives a real benefit such as dependency enforcement, cross-host reuse, platform/provider isolation, independent packaging, or materially clearer ownership.

## 2. Canonical server topology

```text
                         CLIENTS
                +----------+----------+
                |                     |
                v                     v
              Web UI              Workstation
                |                     |
                v                     v
             WebApi                SyncApi
                |                     |
                |                     +-- sync protocol/device/batch/cursor/backpressure
                +----------+----------+
                           |
                           v
                    BUSINESS MODULES
          +----------------+----------------+
          |                |                |
          v                v                v
       Orders          Customers        Inventory
          |                |                |
     Commands/         Commands/         Commands/
     Queries/          Queries/          Queries/
     Admission         Admission         Admission
          |                |                |
          +----------------+----------------+
                           |
                           v
                 module-owned data access
                           |
                           v
                      PostgreSQL
                authoritative server state
```

The same rule applies to `Worker`:

```text
WebApi -----+
SyncApi ----+---> SquiFlow.Orders
Worker -----+        |
AdminApi ---+        v
                 PostgreSQL
```

There is one Orders source implementation even if the same compiled assembly is shipped with more than one host.

## 3. API hosts are not "ingress only"

`WebApi` and `SyncApi` are server hosts/API adapters. Calling them ingress hosts describes why they are separated operationally; it does **not** mean they only accept data or cannot return/read data.

### WebApi owns

- HTTP routing and serialization;
- interactive authentication/session integration;
- TenantContext establishment;
- generic request/body/rate/admission limits;
- correlation and HTTP error/result mapping;
- calling the appropriate module command/query.

### SyncApi owns

- Workstation/device/session authentication integration;
- sync protocol/version framing;
- bounded batches and payload limits;
- compression/streaming when justified;
- cursor/checkpoint framing;
- reconnect fairness/backpressure;
- calling synchronization and capability admission/query paths.

### Worker owns

- durable work claim/lease;
- bounded concurrency;
- retry/backoff/quarantine;
- cancellation/drain;
- invoking the owning business module for the actual business operation.

### Scheduler owns

- deciding **when** a durable occurrence should exist.

It does not own the business mutation. The preferred chain is:

```text
Scheduler
  -> durable occurrence/job
  -> Worker
  -> owning module
  -> authoritative persistence
```

## 4. Business rules do not live in the hosts

Bad:

```text
WebApi
  -> WebOrderService

SyncApi
  -> SyncOrderService

Worker
  -> WorkerOrderService
```

when each service independently decides what an Order means.

Accepted:

```text
WebApi
  -> Orders.CreateOrder

SyncApi
  -> Orders.AdmitCreateOrder

Worker
  -> Orders.ExpireOrder
```

The entry use cases may differ because the trust state and workflow differ, but they belong to the **same Orders capability** and use the same business meaning/invariants.

## 5. Data reads are first-class module operations

The API does not need to bypass the module to get data.

Interactive read example:

```text
GET /orders/123
  -> WebApi
  -> Orders.GetOrder
  -> Orders-owned query/data-access code
  -> PostgreSQL
  -> OrderDetails DTO
  -> WebApi
  -> client
```

The rule is **not** "all reads must rebuild a rich aggregate". A read-only query may use a direct optimized projection/query path when it does not mutate business state and still preserves tenant/security/data-ownership rules.

Mutation example:

```text
POST /orders
  -> WebApi
  -> Orders.CreateOrder
  -> load current authoritative facts
  -> business rules/invariants
  -> concurrency/idempotency
  -> authoritative transaction
  -> PostgreSQL
```

A host controller/endpoint must not perform ad-hoc SQL against module tables merely because it has database connectivity.

## 6. Sync reads and pull/download are also explicit

Sync is not only upload/admission.

A pull path can be:

```text
Workstation
  -> SyncApi
  -> Synchronization capability
  -> cursor/change-feed/outbox query
  -> module-owned authoritative records/projections
  -> PostgreSQL
  -> bounded sync response
```

Cross-capability sync mechanics such as cursor progression, batching, device checkpoint state and reconnect coordination belong to the Synchronization capability/host path, not inside every Orders/Customers/Inventory implementation.

## 7. What persistence means

**Persistence** means state is stored outside the transient process memory so it can survive process loss/restart according to its durability contract.

Persistence is a responsibility, not necessarily a separate database or a generic repository layer.

SquiFlow distinguishes:

| State class | Examples | Current direction |
|---|---|---|
| Ephemeral runtime state | memory cache, rate counters, circuit/session state | process memory or future bounded distributed cache; safe to rebuild/lose |
| Durable processing state | inbox, jobs, operation status, idempotency receipts, outbox | initially may be PostgreSQL-backed; must survive process loss |
| Authoritative business state | orders, customers, payments, stock, staff/device records | PostgreSQL |
| Large object state | artwork, PDFs, uploads | object storage with authoritative metadata |
| Workstation local state | provisional projection, pending sync/outbox | SQLite/WAL |

A "persistence adapter" is simply the concrete code that reads/writes a persistence technology for a module/use case. It can be EF Core, Dapper/ADO.NET, or another deliberate implementation. It is not permission to create `IRepository<T>`, `IUnitOfWork`, or one interface per class by default.

## 8. Query persistence and mutation persistence can differ

For a protected mutation, the business/application path controls the transaction and invariants.

```text
Command
  -> module application/business logic
  -> authoritative transaction
  -> PostgreSQL
```

For a read-only query, an optimized module-owned query path can read a projection directly without pretending every read is a domain mutation.

```text
Query
  -> module query handler
  -> PostgreSQL projection/query
  -> DTO
```

This does not create a second business authority and does not imply separate command/query databases.

## 9. Durable processing state is not a WebApi business database

Do not introduce this baseline:

```text
WebApi
  -> WebApi business DB
  -> Core business DB
```

That would create a second authority and new replication/transaction/restore/reconciliation problems.

Instead, use purpose-specific processing state:

```text
WebApi / SyncApi
  |- ephemeral runtime/cache state
  |- temporary/object staging
  `- durable inbox/job/idempotency/outbox state
            |
            v
       owning module
            |
            v
       PostgreSQL authority
```

Durable processing tables may initially live in PostgreSQL under explicit ownership when that is the simplest correct atomicity/recovery model. A broker or dedicated processing store is introduced only when a demonstrated workload earns it.

## 10. Conceptual boundaries versus physical projects

These are useful conceptual responsibilities:

```text
Orders Core
= deterministic business meaning reusable across hosts

Orders Server execution
= authoritative server use cases, current facts/authority, admission and commit

Orders Workstation execution
= provisional/local orchestration, local facts, SQLite/outbox integration
```

They do **not** imply that all three must immediately be separate projects.

Default rule:

> model the responsibilities clearly first; add a `.csproj` boundary only when the compiler/package/runtime boundary solves a real problem.

## 11. Preferred compact module shape first

For a capability that has not earned extra assembly boundaries, prefer a compact capability-owned project:

```text
modules/orders/
`- SquiFlow.Orders/
   |- Domain/
   |- Application/
   |  |- Commands/
   |  |- Queries/
   |  `- Admission/
   |- Decisions/
   |- Rules/
   |- Contracts/
   `- Events/
```

Concrete provider code that would contaminate this host-neutral project can be added as a capability-owned adapter **when implementation exists**, for example:

```text
modules/orders/
|- SquiFlow.Orders/
`- SquiFlow.Orders.Postgres/       # create when real PostgreSQL integration exists
```

A Workstation adapter/project is created when actual Workstation-specific code requires it:

```text
modules/orders/
|- SquiFlow.Orders/
|- SquiFlow.Orders.Postgres/
`- SquiFlow.Orders.Workstation/    # only when local execution/persistence integration exists
```

Do not create empty projects for symmetry.

## 12. When `Orders.Core` earns a compile-time split

A separate `SquiFlow.Orders.Core` project is valuable when one or more of these are true:

- Workstation and server genuinely execute the same deterministic business code;
- the compiler should prohibit ASP.NET, EF/Npgsql, SQLite, Avalonia, Windows, OpenFGA/ZITADEL, scheduler/actor, or provider dependencies from that shared code;
- the shared decision model needs independent tests/package/reference boundaries;
- server- and Workstation-specific code has grown enough that one assembly makes dependency ownership unclear;
- selective host packaging/reference pressure is real.

Then an earned physical structure can be:

```text
modules/orders/
|- SquiFlow.Orders.Core/
|- SquiFlow.Orders.Server/
|- SquiFlow.Orders.Workstation/
`- SquiFlow.Orders.Postgres/
```

Dependency direction:

```text
Orders.Core
    ^
    +-- Orders.Server
    `-- Orders.Workstation
```

The value is **compile-time dependency enforcement and real cross-host reuse**, not architectural aesthetics.

## 13. When not to split

Do not create `Core`, `Server`, `Workstation`, `Contracts`, `Infrastructure`, or `Ports` projects merely because a reference architecture contains them.

Do not split when:

- the capability is still small;
- there is no real cross-host reusable code;
- provider/platform dependencies are already contained safely;
- the extra assembly would mostly contain forwarding classes;
- an interface would exist only because an implementation class exists;
- the split adds navigation/build/package overhead without enforcing a useful rule.

Different SquiFlow modules may legitimately have different physical complexity.

## 14. Why a compile-time boundary can still matter

Folders and namespaces communicate intent but cannot prevent dependencies.

If `Core/` and `Server/` are folders inside one project, code under `Core/` can still reference any package available to that project.

A separate project can make forbidden references structurally impossible:

```text
SquiFlow.Orders.Core.csproj
  references only Foundation/plain .NET

SquiFlow.Orders.Server.csproj
  references Orders.Core + server abstractions/adapters

SquiFlow.Orders.Workstation.csproj
  references Orders.Core + Workstation abstractions/adapters
```

Use this only when that enforcement is worth the project boundary.

## 15. Binary duplication is not business-logic duplication

If both server hosts execute Orders in-process, deployments may each contain the same compiled assembly:

```text
WebApi deployment
|- SquiFlow.WebApi.dll
`- SquiFlow.Orders.dll

SyncApi deployment
|- SquiFlow.SyncApi.dll
`- SquiFlow.Orders.dll
```

That is expected deployment/binary duplication. There is still one source implementation of Orders.

Do not introduce a mandatory `CoreApi` network hop merely to avoid carrying the same module assembly in multiple hosts. A forced `WebApi -> CoreApi` and `SyncApi -> CoreApi` chain adds network, timeout, retry, service-authentication and availability coupling without improving business ownership.

## 16. Interface/port restraint

A module may use narrow interfaces when a real inversion boundary exists, for example a replaceable provider, testable external fact source, or host-specific effect.

It does not automatically require:

```text
IOrderRepository
IOrderReader
IOrderWriter
IOrderService
IUnitOfWork
IPersistenceProvider
```

for every capability.

SquiFlow keeps the existing rule: introduce an interface when it protects a real dependency/replacement boundary, not for diagram symmetry.

## 17. Reference alignment

SquiFlow is not copying one framework template. The accepted decision is informed by several real .NET architectures:

### Microsoft .NET Clean Architecture guidance

Microsoft describes Clean Architecture as keeping business/application logic at the center while infrastructure depends inward on that core. This supports using a project boundary when compile-time dependency direction is materially valuable.

Reference: <https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures>

### Microsoft persistence/DDD guidance

Microsoft's DDD persistence guidance places concrete data-access implementations in infrastructure/persistence code and explicitly allows query paths other than aggregate repositories for reads, because queries do not mutate state. SquiFlow adopts that distinction without adopting generic repositories as a mandatory convention.

Reference: <https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design>

### ABP module architecture

ABP demonstrates a strongly layered module with Domain, Application, Contracts, database integration and HTTP packages, and its HTTP layer delegates into the application layer. This is evidence that compile-time module/package boundaries can be useful for reusable/large modules. SquiFlow does **not** copy the full ABP project count by default.

Reference: <https://abp.io/docs/latest/framework/architecture/best-practices/module-architecture>

### Kamil Grzybek modular-monolith reference

The modular-monolith reference keeps the API thin and forwards work to modules. Its module-level design uses Application, Domain and Infrastructure assemblies, but it also explicitly notes that those assemblies may be merged and that separation should be pragmatic when a bounded context is simple.

References:
- <https://www.kamilgrzybek.com/blog/posts/modular-monolith-domain-centric-design>
- <https://github.com/kgrzybek/modular-monolith-with-ddd>

The SquiFlow conclusion is therefore:

> modular ownership is mandatory; physical project decomposition is earned.

## 18. Current default

For SquiFlow now:

- keep one source implementation per capability;
- let WebApi, SyncApi, Worker and AdminApi invoke that same capability implementation;
- treat `Core`, `Server`, and `Workstation` as conceptual responsibilities first;
- keep small capabilities compact;
- split `*.Core` only when cross-host reuse/dependency enforcement earns it;
- split provider-specific persistence when concrete provider dependencies need containment;
- use module-owned query paths for reads and authoritative application paths for writes;
- keep PostgreSQL as server authority, SQLite/WAL as Workstation-local persistence, and processing/cache state purpose-specific rather than a second business database.

## 19. Related owners

- `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`
- `docs/architecture/REPOSITORY_STRUCTURE.md`
- `docs/architecture/WEB_AND_SYNC_INGRESS.md`
- `docs/server/SERVER_STATE_AND_PROCESSING.md`
- `docs/server/CORE_API_AND_WORKER.md`
- `docs/data/PERSISTENCE_SELECTION.md`
- `docs/sync/SYNC_AND_AUTHORITY.md`
