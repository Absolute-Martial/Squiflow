# Repository and Deployment Boundaries

**Version:** v0.0.15

## Current state versus target state

The tree below is the **target implementation structure**, not a claim that the projects already exist.

At the current review point the GitLab repository is still **pre-Phase-0** and contains the curated architecture/planning baseline. Phase 0 creates only the executable projects/directories required for the first vertical slices.

Do not create empty projects/directories simply because this document contains a box.

## Target structure

```text
SquiFlow/
├── apps/
│   ├── web/          # Blazor Web App: tenant staff Web + tenant-owner Settings
│   ├── admin-web/    # Blazor Web App: SquiFlow platform administration/control plane
│   └── desktop/      # Avalonia Windows Workstation + Guard/on-demand helpers
├── services/
│   ├── core-api/     # ASP.NET Core HTTP/composition host
│   └── worker/       # durable background execution
├── modules/          # business capability modules
├── packages/         # stable reusable implementation primitives/contracts
├── persistence/
│   ├── abstractions/
│   └── reference/    # provider proof/reference adapters only when actively evaluated
├── infrastructure/   # object storage, telemetry, identity/provider integrations
├── contracts/        # versioned wire/inter-process contracts only when a separate boundary is useful
├── tests/
├── benchmarks/       # only measured qualification/POCs that matter
├── dev/
├── tools/
├── build/
├── deploy/
└── docs/
```

## Runtime boundaries

Web, Admin Web, Desktop, Core API and Worker are genuine build/run/deployment boundaries.

Business modules remain modular-monolith code and do not automatically become network services.

## Creation rule

Create a separate project only when it provides at least one real boundary:
- dependency direction;
- independently built/deployed executable;
- provider adapter under active evaluation;
- test/benchmark isolation;
- security/fault/process isolation;
- stable wire/inter-process contract shared across runtimes.

If `Manager → Service → Executor → Handler` merely forwards the same call, collapse it.

A capability can begin as a small number of coherent files inside an existing module and split later when dependency/ownership evidence appears.

## `packages/` versus `contracts/`

Avoid turning both into generic shared-code dumping grounds.

- `packages/` contains reusable implementation primitives/libraries that may have behavior.
- `contracts/` contains stable versioned wire/inter-process/API contract assemblies where separating them protects runtime compatibility.

Domain/application projects should not depend on transport-specific DTO assemblies merely for convenience.

## Provider reference projects

Provider-specific projects can be created under `persistence/reference` or `infrastructure/reference` to prove a candidate.

Their existence means **we are evaluating/proving this adapter**, not that the product selected the provider.

## Web/control-plane separation

- `apps/web`: tenant business Web + tenant-owner Settings/Administration.
- `apps/admin-web`: SquiFlow platform control plane only.
- `apps/desktop`: local-first business client; never tenant/platform permission management or platform server controls.

Using Blazor Web App for both Web projects does not merge their security surface, route audience or release responsibilities.

## ASP.NET Core host pattern

`services/core-api` owns HTTP routing/composition, similar in architectural role to an executable host project.

Domain/application logic lives in modules and should not depend on ASP.NET Core or provider-specific infrastructure.

## Workstation process boundary

`apps/desktop` contains the Avalonia Workstation application plus the deliberately tiny Guard/helper boundary described in `docs/workstation/GUARD_AND_DEVICE_INTEGRATION.md`.

A helper process is created only when native/heavy/hanging/crashing work earns process isolation; Guard is not a second business application.

## Phase-0 proof

The target structure becomes real only when Phase 0 proves:
- every executable builds independently;
- forbidden dependency directions are executable architecture tests;
- Web/Admin/Desktop do not reference provider persistence implementations directly;
- stable provider abstractions do not leak provider-specific types into domain/application contracts;
- no placeholder projects exist solely to make the tree look complete.
