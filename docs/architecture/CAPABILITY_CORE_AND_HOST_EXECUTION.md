# Capability Core and Host-Specific Execution

**Status:** Accepted architecture direction

## 1. Vocabulary

SquiFlow does not use a generic `shared modules` layer. The stable model is:

```text
Foundation
   ↓
Capability Core
   ↓
Host / infrastructure adapters
```

- **Foundation** contains small product-wide technical/domain primitives with no business-capability ownership.
- **Capability Core** owns one business capability's meaning and deterministic decisions.
- **Host/infrastructure adapters** supply facts, perform effects, expose UI/API surfaces, and integrate with technology-specific dependencies.

Examples of Capability Cores are Customers, Orders, Inventory, Quotations, Staff and Devices.

## 2. Capability Core rule

A capability has one business meaning. Do not create parallel business implementations such as:

```text
Orders.WorkstationBusiness
Orders.ServerBusiness
Orders.WebBusiness
```

Instead, model the shared business processor once:

```text
Intent + Facts + Rule/Policy Snapshot
              ↓
       Capability Core
              ↓
           Decision
```

The host supplies facts and performs effects.

For Orders:

```text
                    Orders Core
                       │
         ┌─────────────┴─────────────┐
         │                           │
 local facts / SQLite       authoritative facts / PostgreSQL
         │                           │
 Workstation adapter              Server adapter
```

The processor may be the same code even though authority and persistence differ.

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

## 4. Host adapters

Host adapters are intentionally different.

### Workstation adapter

May own:

- Avalonia presentation and ViewModels;
- local fact providers backed by SQLite/local snapshots;
- provisional execution orchestration;
- local persistence/outbox;
- local hardware/platform integration;
- local process/runtime integration.

### Server adapter

May own:

- authoritative fact providers;
- current ZITADEL/OpenFGA checks;
- PostgreSQL transaction/concurrency boundaries;
- server-only invariants and provider integrations;
- authoritative admission/commit.

### Web adapter

May own:

- Web-specific pages/components/presentation state;
- task-oriented API client calls;
- Web-only administration UX where appropriate.

Web presentation does not contain another business implementation.

The Web client does **not** own a persistent local business database. SQLite/WAL is a Workstation-only persistence choice. Browser storage, if used, is limited to disposable UI/session cache or temporary transfer state that can be deleted without losing authoritative or pending business truth. Offline-authoritative Web/PWA persistence would require a separate explicit architecture decision and is not part of the current SquiFlow design.

### API adapters

`WebApi` and `SyncApi` are different ingress/workload adapters into the same authoritative capabilities. They may expose different protocol, rate, batching and backpressure behavior without duplicating business meaning.

## 5. Processing modes

Each capability operation declares the execution mode that applies on a host:

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

## 7. Repository naming

The repository category is `foundation/`, not `BuildingBlocks`, `Common`, or a universal `Shared` bucket.

For a capability, the common point is the Capability Core. The current compact `modules/customers/SquiFlow.Customers` project acts as the Customers Capability Core. If/when a capability earns more project separation, prefer explicit names such as:

```text
modules/orders/
├── SquiFlow.Orders.Core/
├── SquiFlow.Orders.Workstation/
├── SquiFlow.Orders.Web/
├── SquiFlow.Orders.Api/
└── persistence adapters only when earned
```

Do not split a tiny capability into empty projects simply to match the diagram.

## 8. Mechanical enforcement

Architecture tests should eventually keep Capability Core and Foundation projects free of host/provider dependencies. This remains an architecture obligation; this document does not require the current PR to add implementation/test code for every accepted future boundary.

The intended dependency direction is:

```text
Foundation
   ↑
Capability Core
   ↑
Application/host adapters
   ↑
Executable composition roots
```
