# Phase 0 — Architectural Development Foundation

**Status:** Active foundation package  
**Current repository state:** Phase-0 implementation has already started; this package describes how to complete and safely extend it.  
**High-level owner:** `docs/implementation/PHASES_AND_GATES.md`  
**All detailed packages:** `docs/implementation/phases/README.md`

## 1. Phase purpose

Phase 0 establishes the architecture and engineering foundation that all later development is allowed to depend on.

It is **not** a freeze around ApplicationKernel work, and it does **not** mean only the files listed here may be created.

The Phase-0 rule is:

> Build enough stable architecture that multiple real capabilities and host surfaces can continue growing without requiring disposable structure or violating future authority, persistence, security, recovery, compatibility, or process boundaries.

Phase 0 is only the first package in a full Phase 0–10 hierarchy. Later phase folders add identity/authorization, local durability, authoritative sync, long-offline recovery, configurable rules/workflow/forms, Admin/Worker, files/backup, system qualification, protected financial/stock authority, and paying-customer production qualification.

## 2. Current repository baseline

The repository already contains real Phase-0 code:

```text
apps/
├── web/SquiFlow.Web
└── desktop/
    ├── workstation/SquiFlow.Workstation
    └── guard/SquiFlow.Guard

services/
└── core-api/SquiFlow.CoreApi

foundation/
├── application-kernel/SquiFlow.ApplicationKernel
└── observability/SquiFlow.Observability

modules/customers/
├── SquiFlow.Customers
├── SquiFlow.Customers.Api
└── SquiFlow.Customers.Workstation

infrastructure/
├── storage/SquiFlow.Storage
└── backup/SquiFlow.Backup

tests/
├── SquiFlow.ApplicationKernel.Specs
└── SquiFlow.Architecture.Specs
```

The existing kernel already proves useful primitives such as module/feature/permission/setting identities, TenantContext, module descriptors/dependency graph, effective feature/permission snapshots, host applicability, and typed settings.

The existing architecture spec already protects host-neutral shared code from selected host/provider dependencies.

The Customers endpoint intentionally exposes no fake business data. That is a valid architecture proof, not a requirement to keep Customers shallow.

## 3. Phase 0 subphases

```text
0A  Architecture baseline reconciliation
    ↓
0B  Application-kernel and module foundation
    ↓
0C  Host/process composition foundation
    ↓
0D  Engineering safety, observability and reproducibility
    ↓
0E  Active capability and parallel-track development
    ↓
0F  Integrated Phase-0 gate and carry-forward ledger
```

These are sequencing checkpoints, not isolated silos. Work from more than one track may be present in the same merge request when it forms one coherent implementation slice.

## 4. Active development tracks throughout Phase 0

The following areas may all evolve during Phase 0:

| Track | Phase-0 direction |
|---|---|
| Capability modules | Customers continues; other real capabilities may be seeded when required. |
| Workstation | shell/composition/navigation, capability adapters, lifecycle, local UX foundations. |
| Guard | real external supervision/restart-budget/lifecycle evidence. |
| Web | shell/composition/navigation/session-independent presentation foundation. |
| Core API | transport/composition/health/endpoint classification foundations; no business-rule duplication. |
| Foundation | only narrow cross-product primitives with demonstrated multi-consumer need. |
| Observability | structured logging/correlation/stable failure/event vocabulary as real paths appear. |
| Storage/backup | narrow provider replacement contracts; full providers are implemented in their owning later slices. |
| CI/tests | dependency-direction tests, deterministic specs, secret/dependency/build checks. |
| Deployment | version-controlled reproducible representation of the components that actually exist. |
| Documentation | focused owner documents and explicit open/carry-forward decisions. |

## 5. Components that may be added during Phase 0

Phase 0 is **not** a permit list. The following may be added when real development needs them and they obey the architecture:

- a new capability module such as Orders, Inventory, Quotes, Products/Pricing, Suppliers, or another real business capability;
- a capability-owned Workstation adapter/view;
- a capability-owned API adapter/endpoint;
- additional application-kernel primitives justified by at least one real use and correct ownership;
- architecture/dependency tests;
- host composition code;
- structured logging, event/failure definitions, health checks, correlation helpers;
- deployment/runbook automation;
- a narrow provider adapter behind an already-accepted replacement boundary;
- proof-of-concept code for a later phase when it is isolated, clearly non-authoritative, and does not become accidental production architecture.

A component does not need to wait for its later headline phase merely to **exist**. It must wait for the required architecture foundation before claiming behavior that depends on that foundation.

## 6. Material additions that remain earned-only

Do not introduce these merely to make Phase 0 look complete:

- Admin Web/Admin API without a real platform-control slice;
- Worker without a real durable workload;
- a dedicated Sync API before real synchronization exists;
- PostgreSQL/SQLite placeholder persistence layers that are not yet being qualified by a real slice;
- HTTP/gRPC between ordinary in-process modules;
- GraphQL/BFF/service mesh/Kubernetes/message broker/cache/schema-registry runtime merely as architecture decoration;
- generic repository/unit-of-work/one-interface-per-class frameworks;
- arbitrary runtime plug-in loading/per-tenant DI containers;
- dozens of empty Domain/Application/Infrastructure projects that provide no compiler or deployment value.

## 7. Sustainability requirement

Phase-0 code does not need to implement every future behavior, but it must not make accepted future architecture unnecessarily difficult.

Examples:

```text
Customers may be simple now,
but it must remain able to gain authoritative server persistence,
local Workstation representation, Sync contracts and Web composition later.

Orders may start as host-neutral meaning now,
but it must not encode ASP.NET, Avalonia or SQLite provider types into its reusable core.

CoreApi may be the compact current host,
but business logic must not become inseparable from controllers/endpoints,
because later interactive Web and Sync workloads may use distinct ingress hosts.
```

## 8. Phase-0 completion does not freeze Phase-0 components

After Phase 0:

```text
ApplicationKernel continues evolving
Workstation continues evolving
Guard continues evolving
Web continues evolving
Core API/ingress continues evolving
Customers continues evolving
Observability continues evolving
Deployment continues evolving
```

The exit gate only means later phases may depend on the established contracts.

## 9. Global Phase-0 hard rules

- host-neutral capability code remains free of host/provider/framework leakage;
- modules communicate in-process inside one runtime host;
- no UI/API/Guard/provider adapter becomes business authority merely because it is convenient;
- no fake durable behavior is presented as final architecture;
- no production secret is committed into source/artifacts;
- no unbounded restart/retry/queue/buffer is introduced;
- no material boundary is introduced without its current failure/recovery/security responsibilities;
- no accepted future boundary is implemented through a shortcut that would force a rewrite of business meaning later.

## 10. Subphase owners

Read the subphase files in order, but treat them cumulatively. When 0C begins, 0A and 0B rules still apply. When 0E begins, all earlier Phase-0 gates still apply.