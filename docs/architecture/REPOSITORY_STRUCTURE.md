# Repository and Deployment Boundaries

**Version:** v0.0.15

## 1. Current state

The repository is still pre-Phase-0. Do not scaffold the full future architecture before the first vertical slice needs it.

The first executable structure can be deliberately small:

```text
SquiFlow/
├── apps/
│   ├── web/          # Blazor tenant Web + tenant Settings/Admin
│   └── desktop/      # Avalonia Windows Workstation
├── services/
│   └── core-api/     # ASP.NET Core HTTP/composition host
├── modules/          # only modules needed by implemented slices
├── infrastructure/   # only concrete providers/integrations currently used
├── tests/
├── deploy/
└── docs/
```

Create these later only when their first real feature exists:

```text
apps/admin-web/       # when platform-control UI is implemented
services/worker/      # when durable background work is implemented
```

## 2. Do not pre-create abstraction directories

The following are **not required directories** in the initial repository:

```text
packages/
contracts/
persistence/abstractions/
helpers/
benchmarks/
tools/
build/
dev/
```

Any of them may appear later if real code gives them a clear responsibility. Their presence in an old architecture tree is not an instruction to scaffold them.

## 3. Project creation rule

A new project earns its existence only for a real boundary such as:
- independently built/deployed executable;
- security/fault/process isolation;
- dependency direction that cannot remain clear inside the current project;
- stable wire/inter-process/plugin contract;
- active provider migration/dual implementation where a separate adapter project materially helps;
- a benchmark/test harness that genuinely needs a separate executable/project.

Do not create a project because a noun exists in the domain model.

## 4. Interface/abstraction rule

Do not default to:

```text
IRepository<T>
IUnitOfWork
IManager
IHelper
IService for every Service
one interface per concrete provider class
```

Use concrete implementations until an actual inversion/replacement/process boundary requires abstraction.

Testing alone does not justify wrapping every framework/provider API in a custom interface. When transaction/locking/provider behavior matters, test the real adapter.

Provider details still stay localized. For example, Hugging Face storage calls belong in infrastructure code and provider-specific types do not leak into business records. Localization is enough until migration begins.

## 5. Runtime boundaries

Accepted runtime direction remains:
- tenant Web;
- Windows Workstation;
- Core API;
- future Platform Admin Web;
- future Worker.

The last two are architectural runtime boundaries, not Phase-0 project requirements.

Business modules remain modular-monolith code and do not automatically become network services.

## 6. Web/control-plane separation

- Tenant Web owns ordinary tenant business Web plus tenant Owner Settings/Administration.
- Future Platform Admin Web is a separate SquiFlow-operator surface.
- Workstation is a local-first business client and never becomes a tenant/platform permission editor or platform control plane.

Using Blazor for tenant Web and future Platform Admin Web does not merge their authorization/audience boundaries.

## 7. Workstation process model

Baseline is one process:

```text
SquiFlow.Workstation
```

There is no always-running Guard/helper process requirement.

If a future updater/native library/driver proves it can hang/crash/leak in a way that warrants process isolation, add one narrow helper then. Do not design its project before the problem exists.

Printing starts through the normal Workstation/Windows printing path. Printer failure remains separate from committed business truth.

## 8. Phase-0 proof

Phase 0 should prove only what the early slices need:
- Web, Workstation and Core API build/run;
- basic CI exists;
- forbidden dependency directions are tested where real projects exist;
- tenant context/security boundaries can be implemented without provider leakage into business code;
- no empty placeholder projects/directories were created for future architecture.
