# Repository and Deployment Boundaries

**Version:** v0.0.15

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
│   └── core-api/         # ASP.NET Core HTTP/composition host
├── modules/              # only modules required by implemented slices
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
services/worker/          # durable background work
```

This tree still does not authorize hundreds of placeholder files or empty module projects.

## 2. Minimal structure must not mean incomplete behavior

Use the smallest structure that preserves all accepted responsibilities.

Bad simplification:

```text
remove Guard because one process looks simpler
remove storage interface even though provider migration is already committed
collapse authorization into token roles and lose current resource checks
```

Good simplification:

```text
keep Guard but do not add GuardManager → GuardService → GuardCoordinator forwarding layers
keep IObjectStore because provider replacement is planned, but do not create one interface per SDK type
keep OpenFGA integration, but keep workflow/financial invariants in normal domain code
```

The goal is **low accidental complexity, not low capability**.

## 3. Project creation rule

A separate project/executable earns its existence for a real boundary such as:
- independently built/deployed executable;
- security/fault/process isolation;
- dependency direction that materially protects the codebase;
- stable wire/inter-process/plugin contract;
- active/committed provider migration or multiple implementations;
- benchmark/test harness that genuinely needs its own executable.

`SquiFlow.Guard` passes this test because it must observe/recover Workstation process failure from outside that process.

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
- Core API;
- future Platform Admin Web;
- future Worker.

Business modules remain modular-monolith code and do not automatically become network services.

## 6. Identity/authorization integrations

- ZITADEL is the identity/authentication provider boundary.
- OpenFGA is the application-authorization engine boundary.
- Core API remains the only business server composition/authorization boundary for ordinary Web/Workstation actions.
- Web/Desktop do not call OpenFGA as a way to bypass server business authorization.
- Provider SDK/client types stay in `infrastructure/identity` and `infrastructure/authorization` rather than leaking into domain records.

ASP.NET Core `IAuthorizationService` remains the API-facing integration primitive; OpenFGA is invoked behind the semantic authorization path where appropriate.

## 7. Workstation Guard process

`apps/desktop/guard` is a real executable boundary because process supervision cannot reliably be owned solely by the process being supervised.

Guard owns launch/supervision, bounded restart/hang recovery, update handoff/recovery, child process cleanup, and bounded diagnostic/resource evidence. See `docs/workstation/GUARD_AND_RECOVERY.md`.

Do not put business rules, OpenFGA authorization, sync semantics or central DB access in Guard.

## 8. Phase-0 proof

Phase 0 should prove:
- Web, Workstation, Guard and Core API build/run;
- Guard can launch/supervise Workstation and survive an independent Workstation crash without corrupting local data;
- basic CI exists;
- provider-specific Hugging Face/Kaggle/ZITADEL/OpenFGA types do not leak into domain/business models;
- `IObjectStore` and `IBackupTarget` compile as narrow provider seams without generic interface proliferation;
- no empty Worker/Admin/module/provider projects exist solely to complete a diagram.
