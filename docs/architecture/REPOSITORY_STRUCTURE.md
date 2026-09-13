# Repository and Deployment Boundaries

**Version:** v0.0.19

## 1. Structural vocabulary

SquiFlow uses these code categories consistently:

```text
Foundation
   ↓
Capability-owned business modules
   ↓
Host / infrastructure adapters
```

`Foundation` replaces vague names such as `BuildingBlocks`, `Common`, or a universal `Shared` bucket.

A capability-owned module is the common business point for Orders, Customers, Inventory, Staff, Devices, etc. A **Capability Core** is the host-neutral/deterministic center of that module when one exists; it is not automatically a separate project.

Detailed owners:
- `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`;
- `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`.

## 2. Current and target repository shape

The repository is still early-stage. Do not scaffold every future module/process/host, but preserve accepted ownership boundaries.

```text
SquiFlow/
|- apps/
|  |- web/                         # Blazor tenant Web / tenant administration UX
|  `- desktop/
|     |- workstation/             # Avalonia local-first Workstation
|     |- guard/                   # always-running supervision/recovery companion
|     |- diagnostics/             # create with first real isolated diagnostic workflow
|     |- maintenance/             # create with first real isolated backup/maintenance workflow
|     |- sync/                    # create only if sync earns desktop process isolation
|     `- document/                # create when heavy document work earns isolation
|- services/
|  |- core-api/                   # current compact ASP.NET Core authoritative host
|  |- web-api/                    # future interactive API host when split is implemented
|  |- sync-api/                   # future workstation-sync host when split is implemented
|  |- admin-api/                  # future independent platform-control backend
|  `- worker/                     # future durable background execution host
|- modules/
|  `- <capability>/
|     |- SquiFlow.<Capability>/   # compact capability-owned business module by default
|     |- SquiFlow.<Capability>.Postgres/       # create when real provider code earns isolation
|     `- SquiFlow.<Capability>.Workstation/    # create when real local adapter code exists
|- foundation/
|  |- application-kernel/
|  |- observability/
|  `- workstation-runtime/        # only when stable desktop IPC contract exists
|- infrastructure/
|  |- storage/                    # truly cross-capability/provider infrastructure only
|  |- backup/
|  |- identity/
|  `- authorization/
|- tests/
|- deploy/
`- docs/
```

Directories shown as future ownership locations are not permission to create empty projects. The current `services/core-api/SquiFlow.CoreApi` remains the compact server host until WebApi/SyncApi are actually split.

## 3. Compact capability shape is the default

Do not assume every module needs Domain/Application/Infrastructure/Core/Server/Workstation projects on day one.

Preferred initial shape:

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

The current compact `modules/customers/SquiFlow.Customers` project remains valid.

If concrete PostgreSQL/EF/Npgsql code would contaminate the host-neutral business module, add a capability-owned adapter **when implementation exists**:

```text
modules/orders/
|- SquiFlow.Orders/
`- SquiFlow.Orders.Postgres/
```

If real Workstation-specific local execution/persistence/platform code exists, add its adapter/project then.

## 4. Earned Core/Server/Workstation split

`Core`, `Server`, and `Workstation` are conceptual responsibilities first.

A later compile-time split may be appropriate:

```text
modules/orders/
|- SquiFlow.Orders.Core/
|- SquiFlow.Orders.Server/
|- SquiFlow.Orders.Workstation/
`- SquiFlow.Orders.Postgres/
```

but only when real pressure exists, such as:

- shared deterministic code is executed by both server and Workstation;
- compile-time protection from Avalonia/ASP.NET/EF/Npgsql/SQLite/provider dependencies is valuable;
- module size/dependency ownership is becoming unclear;
- selective packaging/reference/test boundaries are useful;
- platform/provider dependencies genuinely differ.

Do not create the split merely to match an architecture diagram.

Detailed rule: `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`.

## 5. Server hosts use the same modules

Interactive Web/API, Workstation sync, Worker and Admin API are runtime hosts/adapters, not separate business backends.

```text
WebApi -----+
SyncApi ----+---> SquiFlow.Orders
Worker -----+---> SquiFlow.Customers
AdminApi ---+---> SquiFlow.Inventory
                     |
                     v
                  PostgreSQL
```

Different hosts may call different module entry points, but they do not own duplicate business implementations.

The same module assembly may be present in multiple deployments. That is binary/deployment duplication, not source/business duplication.

## 6. Host-specific execution

One capability can be invoked through different execution paths:

```text
Workstation
  local facts / snapshots
  -> capability deterministic logic where shared
  -> provisional/local effect

WebApi
  -> module command/query
  -> current authoritative facts/rules
  -> PostgreSQL authoritative result

SyncApi
  -> sync/device/protocol admission
  -> module admission/query
  -> current authoritative facts/rules
  -> PostgreSQL authoritative result

Worker
  -> durable work claim/retry context
  -> owning module operation
  -> PostgreSQL/object-store result
```

Do not create `Orders.WorkstationBusiness`, `Orders.WebBusiness`, `Orders.SyncBusiness`, and `Orders.WorkerBusiness` implementations that independently encode the same rules.

Execution mode vocabulary remains:

```text
DeviceLocal
LocalProvisional
ServerAuthoritative
```

## 7. Reads do not bypass module ownership

The phrase `persistence adapter` does not mean every read must reconstruct a rich aggregate or go through a generic repository.

A Web read can be:

```text
WebApi
  -> Orders.GetOrder
  -> Orders-owned query/data access
  -> PostgreSQL
  -> DTO
```

A mutation remains:

```text
WebApi/SyncApi/Worker
  -> owning module application operation
  -> business rules/invariants/concurrency/idempotency
  -> authoritative transaction
  -> PostgreSQL
```

A read-only query may use a direct optimized module-owned projection/query path when it preserves tenant/security/data ownership and does not mutate business state.

## 8. Persistence/state placement

Persistence means state survives process loss according to its durability contract. It does not imply a separate database per host.

Current state classes:

- ephemeral runtime/cache/session/rate state: disposable/rebuildable;
- durable processing state: inbox/jobs/idempotency/outbox/operation status, initially allowed in PostgreSQL;
- authoritative server business state: PostgreSQL;
- large files/objects: object storage plus authoritative metadata;
- Workstation local provisional/pending state: SQLite/WAL.

Do not create a generic `WebApiDB -> CoreDB` chain. Purpose-specific cache/processing/object state is allowed without creating a second business authority.

Owner: `docs/server/SERVER_STATE_AND_PROCESSING.md`.

## 9. Web API and Sync API separation

Interactive Web/API traffic and Workstation sync traffic are accepted as separate future hosts because their protocols, batching, backpressure, fairness, latency and scaling profiles differ.

Calling them ingress hosts describes the operational boundary; it does not mean they only accept data.

```text
Cloud/edge
  |- WebApi  -> interactive commands and queries
  `- SyncApi -> workstation upload/admission and pull/download
        |
        `---- both invoke the same authoritative business modules/state
```

Detailed owner: `docs/architecture/WEB_AND_SYNC_INGRESS.md`.

## 10. Desktop application versus process boundaries

The Windows desktop product is one application. Separate executables exist only where process isolation, lifecycle independence, recovery, or resource reclamation materially helps.

```text
SquiFlow Desktop Application
|
|- SquiFlow.Workstation    always-running UI/local application runtime
|- SquiFlow.Guard          always-running supervision/recovery companion
`- capability processes    normally stopped, launched only when work exists
   |- SquiFlow.Diagnostics
   |- SquiFlow.Maintenance
   |- SquiFlow.Sync
   `- SquiFlow.Document
```

A separate executable does not imply a microservice, separate product, or new business authority.

Detailed owner: `docs/workstation/DESKTOP_PROCESS_MODEL.md`.

## 11. Minimal structure must not mean incomplete behavior

Bad simplification:

```text
remove Guard because one process looks simpler
put backup/migration/diagnostic implementation into Guard because it already runs
put all Web and Sync workload policy into one giant endpoint set forever
copy business logic into Workstation/Web/Sync/Worker adapters
collapse platform super-admin into tenant API routes
collapse authorization into token roles
```

Good simplification:

```text
keep one capability source implementation and small host adapters
keep a compact capability project until a real compile-time split earns itself
keep current CoreApi until real workload evidence earns WebApi/SyncApi split
keep Guard small and launch heavy capability processes only when needed
keep provider interfaces only for real replacement boundaries
keep separate Admin API because platform-control security/availability differs
```

The goal is low accidental complexity, not low capability.

## 12. Project creation rule

A separate project/executable earns its existence for a real boundary such as:

- independently running lifecycle;
- security/fault/process isolation;
- materially different workload scaling/backpressure;
- memory/resource reclamation after heavy work;
- availability independence;
- dependency direction that protects the codebase;
- real shared code that needs compiler-enforced provider/platform neutrality;
- stable wire/IPC/plugin contract;
- active provider migration/multiple implementations;
- selective packaging/reference requirements;
- a benchmark/test harness requiring its own executable.

Do not create one project per folder name in an architecture diagram.

## 13. Foundation rule

`foundation/` contains narrow product-wide primitives and runtime composition infrastructure. It must not become a place for Customers/Orders/Inventory/Staff/Devices business objects.

Current examples:

```text
foundation/application-kernel/SquiFlow.ApplicationKernel
foundation/observability/SquiFlow.Observability
```

A future `SquiFlow.Workstation.Runtime.Contracts` is created only when stable Guard/Workstation/capability IPC requires it; it contains transport-neutral DTOs/enums/contracts only.

## 14. Feature/release management

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

## 15. Identity/authorization boundaries

- ZITADEL is identity/authentication provider integration.
- OpenFGA is application authorization integration.
- WebApi/CoreApi and SyncApi independently derive/verify current server authority appropriate to the operation.
- API-host authentication/coarse policy does not replace module/resource/domain authorization.
- Workstation/Web feature visibility improves UX but never replaces server authorization.
- Provider SDK/client types stay out of a physically separated Capability Core/Foundation business contract.

## 16. Observability foundation

`foundation/observability/SquiFlow.Observability` is a shared instrumentation library, not the Diagnostics executable.

It owns common Serilog/OpenTelemetry bootstrap and provider-neutral instrumentation primitives. Desktop/server processes may reference it without inheriting provider-specific business dependencies.

## 17. Verification direction

Architecture verification should enforce boundaries that actually exist rather than force speculative projects.

Prove, as implementation grows:

- Web, Workstation, Guard and current CoreApi build/run;
- compact capability modules do not leak forbidden host/provider dependencies into code intended for cross-host reuse;
- any physically separated `*.Core` stays plain .NET/provider-neutral;
- host filtering/feature publication remain separate from authorization;
- Guard survives independent Workstation failure without owning business logic;
- no empty future hosts/processes/modules exist solely to complete a diagram;
- per-tenant behavior uses scoped context/versioned data rather than per-tenant DI containers.
