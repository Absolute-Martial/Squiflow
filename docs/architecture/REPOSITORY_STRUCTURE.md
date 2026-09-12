# Repository and Deployment Boundaries

**Version:** v0.0.18

## 1. Current state

The repository is still pre-Phase-0. Do not scaffold every future module, but do not remove accepted runtime/recovery boundaries merely to make the tree smaller.

The initial target shape is intentionally compact but functionally complete enough for early slices:

```text
SquiFlow/
├── apps/
│   ├── web/              # Blazor tenant Web + tenant Settings/Admin
│   └── desktop/
│       ├── workstation/  # Avalonia local-first Workstation
│       └── guard/        # Workstation supervision/recovery companion
├── services/
│   └── core-api/         # ASP.NET Core tenant/business HTTP/composition host
├── modules/              # only modules required by implemented slices
├── foundation/
│   └── application-kernel/ # small SquiFlow-owned module/settings/feature composition
├── infrastructure/
│   ├── storage/          # IObjectStore + HuggingFaceObjectStore
│   ├── backup/           # IBackupTarget + KaggleBackupTarget
│   ├── identity/         # ZITADEL integration
│   └── authorization/    # OpenFGA integration
├── tests/
├── deploy/
└── docs/
```

Create later when their first real feature exists:

```text
apps/admin-web/           # platform-control UI
services/admin-api/       # independent Platform Admin backend
services/worker/          # durable background work
```

This tree still does not authorize hundreds of placeholder files or empty module projects.

## 2. Minimal structure must not mean incomplete behavior

Use the smallest structure that preserves all accepted responsibilities.

Bad simplification:

```text
remove Guard because one process looks simpler
remove storage interface even though provider migration is already committed
collapse platform super-admin into Core API routes
collapse authorization into token roles and lose current resource checks
```

Good simplification:

```text
keep Guard but do not add GuardManager → GuardService → GuardCoordinator forwarding layers
keep IObjectStore because provider replacement is planned, but do not create one interface per SDK type
keep separate Admin API because platform-control availability/security must not depend on Core API
keep OpenFGA integration, but keep workflow/financial invariants in normal domain code
```

The goal is **low accidental complexity, not low capability**.

## 3. Project creation rule

A separate project/executable earns its existence for a real boundary such as:
- independently built/deployed executable;
- security/fault/process isolation;
- availability independence that matters operationally;
- dependency direction that materially protects the codebase;
- stable wire/inter-process/plugin contract;
- active/committed provider migration or multiple implementations;
- benchmark/test harness that genuinely needs its own executable.

`SquiFlow.Guard` passes this test because it must observe/recover Workstation process failure from outside that process.

`services/admin-api` passes this test because platform/super-admin control must remain a separate security/availability surface and must not require the tenant/business Core API process to be healthy.

`IObjectStore` and `IBackupTarget` pass the abstraction test because provider replacement at the first paying customer is already planned.

## 4. Interface/abstraction rule

Do not default to:

```text
IRepository<T>
IUnitOfWork
IManager
IHelper
IService for every Service
one interface per concrete class
```

But do create a narrow interface when there is a real replacement/inversion boundary.

Current justified provider interfaces:

```text
IObjectStore
  └── HuggingFaceObjectStore

IBackupTarget
  └── KaggleBackupTarget
```

Later paid providers implement the same contracts during migration.

The interfaces should reflect SquiFlow semantics and stay narrow; they should not mirror every method/feature in Hugging Face/Kaggle APIs.

A separate `persistence/abstractions` or `packages/` project is still not required merely because two interfaces exist. They can live in coherent infrastructure namespaces until code/dependency growth earns a separate assembly.

## 5. Runtime boundaries

Accepted runtime direction:
- tenant Web;
- Windows Workstation;
- Workstation Guard;
- Core API for tenant/business operations;
- future Platform Admin Web;
- future **Admin API** for platform/super-admin operations;
- future Worker.

Business modules remain modular-monolith code and do not automatically become network services.

## 6. Platform backend separation

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

## 7. Identity/authorization integrations

- ZITADEL is the identity/authentication provider boundary.
- OpenFGA is the application-authorization engine boundary.
- Core API is the business server composition/authorization boundary for tenant Web/Workstation actions.
- Admin API is the platform-control composition/authorization boundary for SquiFlow operators.
- Web/Desktop do not call OpenFGA as a way to bypass server business authorization.
- Admin Web does not receive OpenFGA administrative credentials or directly invoke infrastructure provider APIs.
- Provider SDK/client types stay in infrastructure code rather than leaking into domain records.

ASP.NET Core `IAuthorizationService` remains the host-facing integration primitive in both backends, but tenant and platform policy/model scopes remain separated.

## 8. Workstation Guard process

`apps/desktop/guard` is a real executable boundary because process supervision cannot reliably be owned solely by the process being supervised.

Guard owns launch/supervision, bounded restart/hang recovery, update handoff/recovery, child process cleanup, and bounded diagnostic/resource evidence. See `docs/workstation/GUARD_AND_RECOVERY.md`.

Do not put business rules, OpenFGA authorization, sync semantics or central DB access in Guard.

## 9. Application-kernel and module project shape

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

Detailed owner: docs/architecture/APPLICATION_KERNEL_AND_MODULES.md.

## 10. Phase-0 proof

Phase 0 should prove:
- Web, Workstation, Guard and Core API build/run;
- Guard can launch/supervise Workstation and survive an independent Workstation crash without corrupting local data;
- basic CI exists;
- provider-specific Hugging Face/Kaggle/ZITADEL/OpenFGA types do not leak into domain/business models;
- `IObjectStore` and `IBackupTarget` compile as narrow provider seams without generic interface proliferation;
- Admin Web/Admin API/Worker are documented future executable boundaries but are not empty placeholder projects before their phase;
- no empty module/provider projects exist solely to complete a diagram;
- first SquiFlow module descriptor/dependency graph, host filtering, typed setting, feature availability revision and module-owned permission definition are proven;
- `Volo.Abp.*` and `OrchardCore.*` dependencies do not enter domain/application contracts;
- per-tenant behavior uses scoped `TenantContext` and versioned data rather than per-tenant DI containers.
