# Repository and Deployment Boundaries

**Version:** v0.0.18

## 1. Current state

The repository is still early-stage. Do not scaffold every future module or capability process, but do not remove accepted runtime/recovery boundaries merely to make the tree smaller.

The initial target shape is intentionally compact but functionally complete enough for early slices:

```text
SquiFlow/
├── apps/
│   ├── web/                         # Blazor tenant Web + tenant Settings/Admin
│   └── desktop/                     # one desktop product, multiple justified process boundaries
│       ├── workstation/             # Avalonia local-first Workstation
│       ├── guard/                   # always-running supervision/recovery companion
│       ├── diagnostics/             # create when first diagnostic capability is implemented
│       ├── maintenance/             # create when first backup/maintenance capability is implemented
│       ├── sync/                    # create if/when sync earns process isolation
│       └── document/                # create when heavy document work earns process isolation
├── services/
│   └── core-api/                    # ASP.NET Core tenant/business HTTP/composition host
├── modules/                         # only modules required by implemented slices
├── foundation/
│   ├── application-kernel/          # small SquiFlow-owned module/settings/feature composition
│   ├── observability/               # Serilog + OpenTelemetry shared instrumentation boundary
│   └── workstation-runtime/         # create when first stable desktop IPC contract requires it
├── infrastructure/
│   ├── storage/                     # IObjectStore + bootstrap provider
│   ├── backup/                      # IBackupTarget + bootstrap provider
│   ├── identity/                    # ZITADEL integration
│   └── authorization/               # OpenFGA integration
├── tests/
├── deploy/
└── docs/
```

The desktop directories above describe **accepted ownership locations**, not permission to create empty projects. Only `workstation` and `guard` exist as executable projects today. `diagnostics`, `maintenance`, `sync`, `document`, and `foundation/workstation-runtime` are created when their first real implementation exists.

Create later when their first real feature exists:

```text
apps/admin-web/           # platform-control UI
services/admin-api/       # independent Platform Admin backend
services/worker/          # durable background work
```

This tree still does not authorize hundreds of placeholder files or empty module projects.

## 2. Desktop application versus process boundaries

The Windows desktop product is one SquiFlow application. Separate executables are used only where process isolation, lifecycle independence, recovery, or resource reclamation materially helps.

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

A separate executable here does not imply a microservice, separate product, or new business authority.

Detailed owner: `docs/workstation/DESKTOP_PROCESS_MODEL.md`.

## 3. Minimal structure must not mean incomplete behavior

Use the smallest structure that preserves all accepted responsibilities.

Bad simplification:

```text
remove Guard because one process looks simpler
put backup/migration/diagnostic implementation into Guard because it already runs
remove storage interface even though provider migration is already committed
collapse platform super-admin into Core API routes
collapse authorization into token roles and lose current resource checks
```

Good simplification:

```text
keep Guard but do not add GuardManager → GuardService → GuardCoordinator forwarding layers
keep capability processes normally stopped until their work exists
keep shared runtime contracts narrow rather than referencing executable implementations sideways
keep IObjectStore because provider replacement is planned, but do not create one interface per SDK type
keep separate Admin API because platform-control availability/security must not depend on Core API
keep OpenFGA integration, but keep workflow/financial invariants in normal domain code
```

The goal is **low accidental complexity, not low capability**.

## 4. Project creation rule

A separate project/executable earns its existence for a real boundary such as:
- independently running lifecycle;
- security/fault/process isolation;
- memory/resource reclamation after heavy work;
- availability independence that matters operationally;
- dependency direction that materially protects the codebase;
- stable wire/inter-process/plugin contract;
- active/committed provider migration or multiple implementations;
- benchmark/test harness that genuinely needs its own executable.

`SquiFlow.Guard` passes this test because it must observe/recover Workstation process failure from outside that process.

`SquiFlow.Diagnostics` will pass this test when the first heavy/offline diagnostic workflow is implemented because crash-bundle collection, compression/encryption, retained-log promotion, dump handling, or uploads should not inflate or destabilize Guard/Workstation. Until then it remains a documented boundary rather than an empty project.

`services/admin-api` passes this test because platform/super-admin control must remain a separate security/availability surface and must not require the tenant/business Core API process to be healthy.

`IObjectStore` and `IBackupTarget` pass the abstraction test because provider replacement is already planned.

## 5. Interface/abstraction rule

Do not default to:

```text
IRepository<T>
IUnitOfWork
IManager
IHelper
IService for every Service
one interface per concrete class
```

But do create a narrow interface/contract when there is a real replacement, inversion, or cross-process compatibility boundary.

Current justified provider interfaces:

```text
IObjectStore
IBackupTarget
```

A future `SquiFlow.Workstation.Runtime.Contracts` project is justified only when the first stable Guard/Workstation/capability IPC contract is implemented. It must contain transport-neutral DTOs/enums/contracts, not Avalonia, SQLite/EF, Proto.Actor, Quartz, document libraries, or business modules.

Executable projects should not reference another executable implementation project merely to reuse internal classes.

## 6. Runtime boundaries

Accepted runtime direction:
- tenant Web;
- Windows Workstation;
- Workstation Guard;
- on-demand desktop capability processes where implementation earns isolation;
- Core API for tenant/business operations;
- future Platform Admin Web;
- future **Admin API** for platform/super-admin operations;
- future Worker.

Business modules remain modular-monolith code and do not automatically become network services.

## 7. Platform backend separation

The normal control-plane route is:

```text
apps/admin-web
→ services/admin-api
```

not:

```text
apps/admin-web
→ services/core-api
```

and not:

```text
apps/admin-web
→ services/admin-api
→ services/core-api
```

for ordinary platform administration.

Core API and Admin API may share reviewed libraries/modules and may intentionally share underlying infrastructure such as the same central database, ZITADEL, OpenFGA, Worker, object store, or observability. Shared infrastructure does not make Core API the backend for Admin API.

Admin API owns its own ASP.NET Core composition root, authentication/session validation, platform authorization, health/readiness, rate/admission limits, endpoint inventory, service credentials, audit/correlation, deployment and restart lifecycle.

A Core API outage should not automatically remove the platform application control plane. An Admin API outage should not stop ordinary tenant business operations.

## 8. Identity/authorization integrations

- ZITADEL is the identity/authentication provider boundary.
- OpenFGA is the application-authorization engine boundary.
- Core API is the business server composition/authorization boundary for tenant Web/Workstation actions.
- Admin API is the platform-control composition/authorization boundary for SquiFlow operators.
- Web/Desktop do not call OpenFGA as a way to bypass server business authorization.
- Admin Web does not receive OpenFGA administrative credentials or directly invoke infrastructure provider APIs.
- Provider SDK/client types stay in infrastructure code rather than leaking into domain records.

ASP.NET Core `IAuthorizationService` remains the host-facing integration primitive in both backends, but tenant and platform policy/model scopes remain separated.

## 9. Workstation Guard and capability processes

`apps/desktop/guard` is a real executable boundary because process supervision cannot reliably be owned solely by the process being supervised.

Guard owns launch/supervision, bounded restart/hang recovery, update lifecycle coordination, child/capability process supervision, and bounded diagnostic/resource evidence. See `docs/workstation/GUARD_AND_RECOVERY.md`.

Guard does not implement heavy diagnostic packaging/upload, SQLite backup, schema migration, document rendering, or business sync semantics. Those responsibilities belong to the Workstation/application runtime or a narrowly scoped on-demand capability process.

Ordinary online logs can flow directly from Workstation/Guard through Serilog OTLP. `SquiFlow.Diagnostics` is reserved for heavy/offline diagnostic work and must not be launched per ordinary log batch.

## 10. Application-kernel and module project shape

The first slice may use a compact project layout such as:

~~~text
foundation/application-kernel/
modules/customers/
  SquiFlow.Customers.Domain
  SquiFlow.Customers.Application
  SquiFlow.Customers.Contracts
  host adapter/UI projects only when the first slice needs them
~~~

Do not create every possible layer for every module. A simple module may remain in fewer projects until dependency or deployment pressure earns a split.

Module domain/application contracts remain pure .NET and do not depend on ABP or Orchard. Workstation/server/UI/persistence contributions point inward to those contracts. Per-tenant composition is a versioned availability/settings/permission snapshot, not a per-tenant project, service provider, schema, database, or process.

Detailed owner: `docs/architecture/APPLICATION_KERNEL_AND_MODULES.md`.

## 11. Shared observability foundation

`foundation/observability/SquiFlow.Observability` is a shared instrumentation library, not the diagnostics executable.

It owns the common Serilog/OpenTelemetry bootstrap and provider-neutral instrumentation primitives. Desktop/server processes may reference it without inheriting provider-specific business dependencies.

Detailed owners:
- `docs/observability/OBSERVABILITY_IMPLEMENTATION_CONTRACT.md`
- `docs/observability/SERILOG_OTLP_PIPELINE.md`

## 12. Phase-0 proof

Phase 0 should prove:
- Web, Workstation, Guard and Core API build/run;
- `SquiFlow.Observability` builds as a shared foundation project;
- Guard can launch/supervise Workstation and survive an independent Workstation crash without corrupting local data;
- basic CI exists;
- provider-specific storage/backup/ZITADEL/OpenFGA types do not leak into domain/business models;
- `IObjectStore` and `IBackupTarget` compile as narrow provider seams without generic interface proliferation;
- Admin Web/Admin API/Worker and future desktop capability processes are documented boundaries but are not empty placeholder projects before their first slice;
- no empty module/provider/capability projects exist solely to complete a diagram;
- first SquiFlow module descriptor/dependency graph, host filtering, typed setting, feature availability revision and module-owned permission definition are proven;
- `Volo.Abp.*` and `OrchardCore.*` dependencies do not enter domain/application contracts;
- per-tenant behavior uses scoped `TenantContext` and versioned data rather than per-tenant DI containers.
