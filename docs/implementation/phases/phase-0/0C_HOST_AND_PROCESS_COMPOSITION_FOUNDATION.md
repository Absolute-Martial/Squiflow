# Phase 0C — Host and Process Composition Foundation

**Detailed phase index:** `docs/implementation/phases/README.md`

**Purpose:** Make the currently justified executable boundaries real, consistent, and sustainable without prematurely creating future processes.

## 1. Current hosts

Phase 0 already contains:

```text
apps/web/SquiFlow.Web
apps/desktop/workstation/SquiFlow.Workstation
apps/desktop/guard/SquiFlow.Guard
services/core-api/SquiFlow.CoreApi
```

These may all continue evolving during 0C.

## 2. Host rule

An executable composition root owns framework/platform integration. It does not own business meaning merely because requests/UI/process lifecycle enter there.

```text
Host
→ configuration / DI / lifecycle / transport / presentation
→ capability application/core
→ owning persistence/provider adapters when applicable
```

Do not create host-specific copies of business rules such as `WebOrderService`, `DesktopOrderService`, and `SyncOrderService` that each define what an Order means.

## 3. Common host foundations

Every existing host should implement the subset that applies to it:

- validated startup configuration;
- environment/application version identity;
- safe startup failure behavior;
- cancellation/graceful shutdown;
- structured logging;
- correlation/trace context foundation;
- stable startup/lifecycle failure codes;
- dependency composition only at the appropriate host boundary;
- secret/configuration separation;
- health/liveness/readiness where the host is a server process;
- bounded background loops if the host currently owns any local loop.

Do not force server health-endpoint concepts into Guard/Workstation when a different lifecycle signal is correct.

## 4. Workstation composition

Workstation may continue adding:

- navigation/workspace composition;
- capability-owned views/view-models/presentation adapters;
- host services such as window/lifecycle coordination;
- local UX state that is clearly non-authoritative;
- preparation for later local persistence without inventing a fake temporary business DB contract.

The Workstation must not gain:

- central PostgreSQL credentials;
- OpenFGA administrative credentials;
- Platform Admin authority;
- business authority merely because a view model can calculate something locally.

## 5. Guard composition

Guard remains a distinct tiny process because it must survive/observe Workstation failure externally.

Phase-0 Guard may own:

```text
launch/supervise Workstation
bounded restart budget/backoff
observe exit
intentional-stop distinction
child cleanup where current
bounded lifecycle/resource evidence
```

Guard must not own:

- Orders/Customers business logic;
- SQLite business queries;
- OpenFGA decisions;
- central DB access;
- server scheduling/Worker duties;
- long-lived cryptographic key custody.

Heavy Diagnostics/Maintenance/Document helpers remain later earned processes when their first real isolation need exists.

## 6. Web composition

Tenant Web may continue adding:

- routing/layout/navigation;
- capability-owned UI composition;
- safe presentation-state patterns;
- explicit request contracts once real server functionality exists;
- server-side draft infrastructure only when a real valuable form requires it.

Do not create browser-local authoritative business storage or PWA sync in the current baseline.

## 7. Core API composition

Current CoreApi can own:

- ASP.NET pipeline;
- endpoint registration;
- transport/request DTO handling;
- correlation;
- coarse admission/security metadata foundations;
- health/readiness;
- dependency composition.

It must not become the long-term owner of business-domain rules.

Current compact CoreApi existence does not prohibit later WebApi/SyncApi workload split. Therefore new capability behavior should remain below the transport boundary so ingress can evolve without rewriting business meaning.

## 8. Additional adapters and hosts may be added

A capability-specific adapter/project may be added in 0C when real work needs it.

Examples:

```text
SquiFlow.Orders.Workstation
SquiFlow.Orders.Api
SquiFlow.Inventory.Workstation
```

A **new executable process** requires stronger evidence because it adds lifecycle/deployment/health/security/recovery obligations.

Before adding a process ask:

1. What fault/resource/security/deployment boundary requires a process?
2. Why is an in-process module/adapter insufficient?
3. What durable state survives process death?
4. What health/recovery behavior now becomes mandatory?
5. What compatibility contract exists between processes?

If a real requirement answers those questions earlier than the original roadmap expected, pull the owning later subphase/gate forward explicitly rather than creating a temporary unsafe process.

## 9. Processes deliberately not required yet

Do not create empty placeholders for:

```text
apps/admin-web
services/admin-api
services/worker
services/sync-api
apps/desktop/diagnostics
apps/desktop/maintenance
apps/desktop/document
```

unless their first real responsibility is being implemented.

Documentation may reserve/describe these future boundaries without repository scaffolding.

## 10. In-process module communication

Inside one host:

```text
Orders application/query
→ Customers public query/application surface
```

when a real cross-capability dependency exists.

Not:

```text
Orders
→ localhost HTTP/gRPC
→ Customers
```

unless they are actually separated into independently deployed processes under an explicit later decision.

## 11. Failure and lifecycle tests

At minimum for current hosts:

- Workstation exits unexpectedly while Guard survives;
- Guard exits while Workstation survives;
- restart budget prevents an infinite crash loop;
- normal user-requested Workstation exit is not treated as a crash storm;
- server host receives graceful shutdown/cancellation;
- invalid required configuration fails safely and visibly;
- one host's startup failure does not corrupt another host's durable state;
- capability business code can be invoked without depending on a UI/controller type;
- wrong-host capability contribution is rejected/not registered.

## 12. Future sustainability

0C must preserve these future boundaries:

```text
interactive Web workload
Workstation Sync workload
Worker durable workload
Platform Admin control plane
heavy/on-demand desktop helper processes
```

Preserving them means clean dependency direction and no accidental authority coupling, not creating all of them now.

## 13. Exit gate

0C is complete when:

- all currently justified hosts build/start with consistent composition/lifecycle discipline;
- Guard proves external supervision without business ownership;
- capability logic is not trapped in UI/controllers;
- in-process modules remain in-process;
- current host structure can evolve to future workload-specific processes without duplicating business meaning;
- no empty executable exists merely to make the repository resemble the target architecture diagram.