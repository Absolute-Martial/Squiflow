# Repository and Deployment Boundaries

**Version:** v0.0.18

## 1. Structural vocabulary

SquiFlow uses three code categories consistently:

```text
Foundation
   ↓
Capability Core
   ↓
Host / infrastructure adapter
```

`Foundation` replaces vague names such as `BuildingBlocks`, `Common`, or a universal `Shared` bucket.

A Capability Core is the common point for one business capability. It owns business meaning and deterministic decisions; it is not a Workstation/Server/Web copy.

Detailed owner: `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`.

## 2. Current and target repository shape

The repository is still early-stage. Do not scaffold every future module/process/host, but preserve accepted ownership boundaries.

```text
SquiFlow/
├── apps/
│   ├── web/                         # Blazor tenant Web / tenant administration UX
│   └── desktop/                     # one desktop product, multiple justified process boundaries
│       ├── workstation/             # Avalonia local-first Workstation
│       ├── guard/                   # always-running supervision/recovery companion
│       ├── diagnostics/             # create with first real isolated diagnostic workflow
│       ├── maintenance/             # create with first real isolated backup/maintenance workflow
│       ├── sync/                    # create only if sync earns desktop process isolation
│       └── document/                # create when heavy document work earns isolation
├── services/
│   ├── core-api/                    # current compact ASP.NET Core authoritative host
│   ├── web-api/                     # future interactive ingress host when split is implemented
│   ├── sync-api/                    # future workstation-sync ingress host when split is implemented
│   ├── admin-api/                   # future independent platform-control backend
│   └── worker/                      # future durable background execution host
├── modules/
│   └── <capability>/
│       ├── <Capability Core>        # shared business meaning / deterministic processing
│       ├── *.Workstation/           # host adapter/presentation when needed
│       ├── *.Web/                   # Web presentation adapter when needed
│       └── *.Api/                   # API adapter when needed
├── foundation/
│   ├── application-kernel/          # module/host/settings/features/execution vocabulary
│   ├── observability/               # Serilog + OpenTelemetry instrumentation boundary
│   └── workstation-runtime/         # create only when stable desktop IPC contract exists
├── infrastructure/
│   ├── storage/
│   ├── backup/
│   ├── identity/
│   └── authorization/
├── tests/
├── deploy/
└── docs/
```

Directories shown as future ownership locations are not permission to create empty projects. The current `services/core-api/SquiFlow.CoreApi` remains the compact server host until WebApi/SyncApi are actually split.

## 3. Capability Core shape

The old phrase `shared module` is deliberately avoided.

The common point is a capability-owned Core, for example:

```text
modules/orders/
├── SquiFlow.Orders.Core/
│   ├── Model/
│   ├── Operations/
│   ├── Facts/
│   ├── Decisions/
│   ├── Rules/
│   ├── Events/
│   ├── Features/
│   ├── Permissions/
│   └── Settings/
├── SquiFlow.Orders.Workstation/
├── SquiFlow.Orders.Web/
└── SquiFlow.Orders.Api/
```

The current compact `modules/customers/SquiFlow.Customers` project acts as the Customers Capability Core. Do not rename/split it merely for diagram purity; future separation is earned by real dependency pressure.

Capability Core/Foundation projects remain plain .NET and must not depend on Avalonia, ASP.NET Core, Windows APIs, EF/Npgsql/SQLite provider APIs, OpenFGA/ZITADEL SDKs, Quartz/TickerQ, Proto.Actor, MassTransit/RabbitMQ, or provider-specific infrastructure SDKs.

## 4. Host-specific execution

One Capability Core can be invoked through different execution paths:

```text
Workstation
  local fact provider
  → Capability Core
  → provisional/local effect

WebApi
  authoritative fact provider
  → Capability Core
  → PostgreSQL authoritative effect

SyncApi
  operation-envelope admission
  → authoritative fact provider
  → Capability Core / selective re-evaluation
  → PostgreSQL authoritative effect
```

Do not create `Orders.WorkstationBusiness`, `Orders.WebBusiness`, and `Orders.ServerBusiness` implementations that independently encode the same rules.

Execution mode vocabulary is:

```text
DeviceLocal
LocalProvisional
ServerAuthoritative
```

## 5. Web API and Sync API separation

Interactive Web/API traffic and Workstation sync traffic are accepted as separate future ingress/workload hosts because their protocols, batching, backpressure, fairness, latency and scaling profiles differ.

```text
Cloud/edge
  ├─ WebApi  → interactive users / Web
  └─ SyncApi → workstation device synchronization
        │
        └──── both reach the same authoritative Capability Cores/state
```

This is two backend ingress hosts, not two business backends and not two sources of truth.

Detailed owner: `docs/architecture/WEB_AND_SYNC_INGRESS.md`.

## 6. Desktop application versus process boundaries

The Windows desktop product is one application. Separate executables exist only where process isolation, lifecycle independence, recovery, or resource reclamation materially helps.

```text
SquiFlow Desktop Application
│
├── SquiFlow.Workstation    always-running UI/local application runtime
├── SquiFlow.Guard          always-running supervision/recovery companion
└── capability processes    normally stopped, launched only when work exists
    ├── SquiFlow.Diagnostics
    ├── SquiFlow.Maintenance
    ├── SquiFlow.Sync
    └── SquiFlow.Document
```

A separate executable does not imply a microservice, separate product, or new business authority.

Detailed owner: `docs/workstation/DESKTOP_PROCESS_MODEL.md`.

## 7. Minimal structure must not mean incomplete behavior

Bad simplification:

```text
remove Guard because one process looks simpler
put backup/migration/diagnostic implementation into Guard because it already runs
put Web and Sync workload policy into one giant endpoint set forever
copy business logic into Workstation/Web adapters
collapse platform super-admin into tenant API routes
collapse authorization into token roles
```

Good simplification:

```text
keep one Capability Core and small host adapters
keep current CoreApi until real workload evidence earns WebApi/SyncApi split
keep Guard small and launch heavy capability processes only when needed
keep provider interfaces only for real replacement boundaries
keep separate Admin API because platform-control security/availability differs
```

The goal is low accidental complexity, not low capability.

## 8. Project creation rule

A separate project/executable earns its existence for a real boundary such as:

- independently running lifecycle;
- security/fault/process isolation;
- materially different workload scaling/backpressure;
- memory/resource reclamation after heavy work;
- availability independence;
- dependency direction that protects the codebase;
- stable wire/IPC/plugin contract;
- active provider migration/multiple implementations;
- a benchmark/test harness requiring its own executable.

Do not create one project per folder name in an architecture diagram.

## 9. Foundation rule

`foundation/` contains narrow product-wide primitives and runtime composition infrastructure. It must not become a place for Customers/Orders/Inventory/Staff/Devices business objects.

Current examples:

```text
foundation/application-kernel/SquiFlow.ApplicationKernel
foundation/observability/SquiFlow.Observability
```

A future `SquiFlow.Workstation.Runtime.Contracts` is created only when stable Guard/Workstation/capability IPC requires it; it contains transport-neutral DTOs/enums/contracts only.

## 10. Feature/release management

Feature management belongs to the application kernel/capability definitions and is distinct from permission/domain validity.

Accepted concepts include:

- host availability;
- release channels (`Internal`, `Preview`, `Beta`, `Stable`, `Deprecated`);
- tenant/deployment rollout ceiling;
- versioned Workstation feature snapshots;
- stable experiment/A-B assignments for allowed product/presentation experiments;
- kill switch/rollback;
- offline policy (`SnapshotAllowed`, `StableOnly`, `ServerRequired`).

Detailed owner: `docs/architecture/FEATURE_RELEASE_AND_EXPERIMENTS.md`.

## 11. Identity/authorization boundaries

- ZITADEL is identity/authentication provider integration.
- OpenFGA is application authorization integration.
- WebApi/CoreApi and SyncApi independently derive/verify current server authority.
- Workstation/Web feature visibility improves UX but never replaces server authorization.
- Provider SDK/client types stay out of Capability Cores/Foundation business contracts.

## 12. Observability foundation

`foundation/observability/SquiFlow.Observability` is a shared instrumentation library, not the Diagnostics executable.

It owns common Serilog/OpenTelemetry bootstrap and provider-neutral instrumentation primitives. Desktop/server processes may reference it without inheriting provider-specific business dependencies.

## 13. Phase-0 proof

Phase 0 should prove:

- Web, Workstation, Guard and current CoreApi build/run;
- Customers compact project behaves as a platform-neutral Capability Core;
- architecture tests keep Foundation/Capability Core free of host/provider dependencies;
- host filtering distinguishes Workstation, WebApi, SyncApi and Guard eligibility;
- feature publication supports host and release-channel filtering without becoming authorization;
- stable experiment assignment is deterministic for the selected subject;
- Guard survives independent Workstation failure without owning business logic;
- no empty future hosts/processes/modules exist solely to complete a diagram;
- per-tenant behavior uses scoped context/versioned data rather than per-tenant DI containers.
