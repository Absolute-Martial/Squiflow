# Capability Core and Host-Specific Execution

**Status:** Accepted architecture direction

## 1. Vocabulary

SquiFlow does not use a generic `shared modules` layer. The stable conceptual model is:

```text
Foundation
   ↓
Capability-owned business module
   ↓
Host / infrastructure adapters
```

Inside a capability-owned module, **Capability Core** names the host-neutral business meaning and deterministic decisions that may be reused across execution environments. It is a conceptual responsibility first; it is not automatically a separate `.Core.csproj`.

- **Foundation** contains small product-wide technical/domain primitives with no business-capability ownership.
- **Capability-owned module** owns one business capability's commands, queries, domain meaning and application behavior.
- **Capability Core** is the host-neutral/deterministic center of that module when such a center exists.
- **Host/infrastructure adapters** supply host-specific facts/effects, presentation, transport and provider integration.

Examples of capabilities are Customers, Orders, Inventory, Quotations, Staff and Devices.

Detailed physical-project rules: `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`.

## 2. One capability, one business meaning

A capability has one source implementation of its business meaning. Do not create parallel business implementations such as:

```text
Orders.WorkstationBusiness
Orders.ServerBusiness
Orders.WebBusiness
```

WebApi, SyncApi and Worker may invoke different **entry use cases** because their trust/workflow state differs, but those entry points belong to the same Orders capability.

For deterministic decisions that are genuinely shared across server and Workstation, model the processor once:

```text
Intent + Facts + Rule/Policy Snapshot
              ↓
       Capability Core
              ↓
           Decision
```

The host/application path supplies facts and performs effects.

For Orders:

```text
                    Orders capability
                          │
                 deterministic core
                    /           \
                   /             \
                  v               v
       local facts/SQLite   authoritative facts/PostgreSQL
                  │               │
       Workstation execution   Server execution
```

The deterministic processor may be the same code even though authority and persistence differ.

## 3. What belongs in a Capability Core

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

When a separate Core project exists, it must not depend on host/provider technologies such as:

- Avalonia;
- ASP.NET Core;
- Windows APIs;
- EF Core/Npgsql/SQLite provider APIs;
- OpenFGA/ZITADEL SDKs;
- Quartz/TickerQ;
- Proto.Actor;
- MassTransit/RabbitMQ;
- provider-specific telemetry/storage SDKs.

A compact capability project can still preserve these boundaries by convention and architecture tests until dependency pressure earns a physical split.

## 4. Host/application adapters

Host/application paths are intentionally different.

### Workstation execution

May own:

- Avalonia presentation and ViewModels;
- local fact providers backed by SQLite/local snapshots;
- provisional execution orchestration;
- local persistence/outbox;
- local hardware/platform integration;
- local process/runtime integration.

### Server authoritative execution

May own:

- authoritative fact providers;
- current ZITADEL/OpenFGA-backed checks through appropriate abstractions/integration;
- PostgreSQL transaction/concurrency boundaries;
- server-only invariants and provider integrations;
- authoritative admission/commit.

This responsibility may initially be folders/classes inside a compact `SquiFlow.Orders` project. It does not require `SquiFlow.Orders.Server` until a real compile-time boundary is useful.

### Web presentation adapter

May own:

- Web-specific pages/components/presentation state;
- task-oriented API client calls;
- Web-only administration UX where appropriate.

Web presentation does not contain another business implementation.

The Web client does **not** own a persistent local business database. SQLite/WAL is a Workstation-only persistence choice. Browser storage, if used, is limited to disposable UI/session cache or temporary transfer state that can be deleted without losing authoritative or pending business truth. Offline-authoritative Web/PWA persistence would require a separate explicit architecture decision and is not part of the current SquiFlow design.

### API hosts/adapters

`WebApi` and `SyncApi` are different server hosts/API adapters into the same authoritative modules. Calling them ingress hosts describes workload separation; it does not mean they only accept data.

They may both invoke module queries and commands. They differ in protocol, identity/session context, batching, cursor, fairness and backpressure concerns without duplicating business meaning.

Example read path:

```text
WebApi
  -> Orders.GetOrder
  -> Orders-owned query/data access
  -> PostgreSQL
  -> DTO
  -> WebApi response
```

Example sync admission path:

```text
SyncApi
  -> Orders.AdmitCreateOrder
  -> current authoritative facts + rules
  -> authoritative commit
```

## 5. Processing modes

Each capability operation declares or documents the execution mode that applies on a host:

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

## 6. Presentation sharing rule

Share business/domain contracts and deterministic processing, not UI framework state.

Do not share one Avalonia/Web ViewModel merely to increase reuse. Workstation and Web can share:

- operation inputs;
- query/read contracts where semantically identical;
- validation definitions;
- feature/permission/setting definitions;
- domain and decision logic.

They keep host-specific presentation state and navigation separate.

## 7. Compact module first, compile-time split when earned

The repository category is `foundation/`, not `BuildingBlocks`, `Common`, or a universal `Shared` bucket.

A small capability should normally begin compactly:

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

Concrete provider code can be isolated when it actually exists and would otherwise leak provider dependencies:

```text
modules/orders/
|- SquiFlow.Orders/
`- SquiFlow.Orders.Postgres/
```

A Workstation adapter/project is created only when real local execution/persistence/platform code needs it.

A separate `SquiFlow.Orders.Core` is created only when compiler-enforced host/provider neutrality or real server/Workstation reuse justifies it. An earned later split may be:

```text
modules/orders/
|- SquiFlow.Orders.Core/
|- SquiFlow.Orders.Server/
|- SquiFlow.Orders.Workstation/
`- SquiFlow.Orders.Postgres/
```

The current compact `modules/customers/SquiFlow.Customers` project remains valid. Do not rename/split it merely for diagram purity.

## 8. Why/when compile-time separation matters

Folders communicate intent; separate projects can enforce dependency direction.

Use a project split when, for example:

- Workstation and server genuinely reference the same deterministic code;
- the compiler should make Avalonia/ASP.NET/EF/Npgsql/SQLite/provider references impossible in the shared core;
- provider-specific persistence needs containment;
- module size/dependency pressure makes ownership unclear;
- selective packaging/reference boundaries are real.

Do not split because a reference architecture has more projects or because every folder name looks like it could be a `.csproj`.

Detailed criteria and external reference rationale: `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`.

## 9. Mechanical enforcement

Architecture tests should enforce project boundaries that actually exist. When a Capability Core is physically separated, tests/compile-time references should keep it free of forbidden host/provider dependencies.

For compact capabilities, architecture tests may enforce forbidden dependencies/namespaces without forcing premature project decomposition.

The intended dependency direction remains:

```text
Foundation
   ↑
Capability business meaning
   ↑
Application/host/provider adapters
   ↑
Executable composition roots
```
