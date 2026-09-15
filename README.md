# SquiFlow

**Current architecture/development baseline: `v0.0.20`**  
**Implementation state:** Phase 0A is Complete / Qualified under the production-honest governance model; Phase 0B is **IN PROGRESS** with the first capability-owned implementation slice.

SquiFlow is rebuilding from accepted architecture and current responsibility rather than carrying premature implementation or speculative phase structure forward. Earlier code and retired plans remain in Git history as evidence/context, not current authority.

Reset decision: `docs/decisions/PRINCIPLES_FIRST_RESET_2026-09-14.md`.  
Gate model: `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`.  
Evidence/permanence model: `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`.

## Read in this order

1. `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md` — canonical scope/quality contract.
2. `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` — evidence permanence/requalification contract.
3. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` — qualified reset baseline and enduring 0A guarantees.
4. `docs/implementation/phases/phase-0/0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md` and `0B_STATUS.md` — active 0B gate and current slice.
5. `docs/domain/BUSINESS_TERMS.md` — consequential accepted versus discovery-sensitive terminology.
6. `docs/architecture/ENGINEERING_PRINCIPLES.md` — engineering principles.
7. `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md` — dependency/ownership rules.
8. `docs/decisions/CURRENT_DECISIONS.md` and `OPEN_DECISIONS.md` — accepted versus unresolved direction.
9. `docs/architecture/REPOSITORY_STRUCTURE.md` and `REPOSITORY_FOLDER_STRUCTURE.md` — current/growth placement maps.
10. The focused owner for the responsibility being changed.

## Source precedence

```text
focused canonical owner
        ↓
accepted/current decision record
        ↓
active implementation/gate record
        ↓
historical review/source-study/branch/MR material
```

Repository state proves what is implemented. Architecture documents may define future ownership and accepted direction without implying that a project/runtime currently exists.

## Current repository implementation boundary

The active 0B branch contains exactly two projects:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
```

There is no Foundation/ApplicationKernel project and no application/service executable.

The production-code scope currently implemented is only the accepted structural Party classification:

```text
PartyKind
├── Person
└── Organization
```

That is **not** a claim that Party/Customer/Account lifecycle, identity format, persistence, API, host, authorization, synchronization, or customer-complete product scope exists. Those remain `NOT_INTRODUCED` unless and until a real current slice earns them.

## Architecture direction retained through the reset

- C# / .NET 10 is the application baseline.
- Avalonia Workstation and Blazor tenant Web remain accepted presentation directions when those surfaces are implemented.
- ASP.NET Core remains the server-host foundation when server hosts are implemented.
- Modular monolith first; ordinary capability communication stays in-process.
- Dependency direction remains Foundation → capability-owned business meaning → host/provider adapter → executable composition root (outer layers depend inward).
- One source implementation of business meaning per capability.
- Workstation remains the local-first/offline direction; PostgreSQL remains selected central authority; SQLite/WAL remains selected Workstation local/provisional persistence when those scopes are introduced.
- ZITADEL identity, OpenFGA authorization, and OpenBao/Vault-style key-management directions remain accepted but unimplemented until their scopes activate.
- Guard remains an external supervision/recovery boundary when rebuilt; it is not business authority, database authority, Worker, scheduler, or key vault.
- Project/process/provider/interface splits are earned by real compiler, provider, lifecycle, fault, security, resource, deployment, compatibility, packaging, or workload pressure.

## Development rule

The governing rule is:

> **Scope is a choice; honesty is not.**

A phase is not a checklist and a phase label is not authority to build its imagined contents. Work proceeds as the smallest production-honest scope:

```text
real responsibility
        ↓
declare production intent + exact scope
        ↓
NOT_INTRODUCED / PRODUCTION_HONEST / BLOCKED
        ↓
implement only what current scope earns
        ↓
prove claims with falsifiable evidence
        ↓
keep permanent/recurring regression guards
        ↓
qualify only with BLOCKED = none
```

KISS reduces accidental complexity; YAGNI reduces speculative breadth. Neither permits prototype-grade depth for a responsibility already introduced.

## 0A → 0B transition

0A guarantees that a developer can identify current authority, current implementation truth, repository ownership, and future-only scope without relying on deleted code or speculative governance.

Its zero-project inventory was qualification-time reset evidence, not a permanent invariant. The first 0B projects are legitimate because they are newly derived from current capability semantics rather than recreated from the purged tree.

0B is capability-first. `SquiFlow.ApplicationKernel` remains absent. Shared Foundation is introduced only when real current reuse/change/ownership pressure justifies shared semantics; no second consumer is manufactured to satisfy a quota.

## Current verification status

The active tests cover the Party-kind contract and the initial Parties dependency boundary. The .NET 10 test path uses Microsoft Testing Platform and xUnit v3.

Repository-owned verification is:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

The assistant environment does not have the .NET SDK, so these commands have **not** been claimed as passing locally.

GitLab runs the commands from root `.gitlab-ci.yml`; its current jobs fail before start because hosted CI quota is exhausted. GitHub runs the same commands from `.github/workflows/verify-dotnet.yml`, reading the SDK from `global.json`; the user manages that mirror/remote independently, so no GitHub passing run is claimed yet.

A successful real run on either legitimate CI host can provide executable evidence for the current code claims. Until then, the introduced implementation claims remain `BLOCKED` in `0B_STATUS.md`.

## Future governance

Phase 0 and the already-concrete Phase 1 trust boundary have detailed governance. Phase 2–10 remain direction-only `NOT_INTRODUCED` planning until real work earns detail. `FUTURE_PHASE_CARRY_FORWARD.md` preserves anticipation without turning it into specification.

## Versioning

`v0.0.20` marks the principles-first reset baseline and production-honest 0A qualification. Older document labels may remain as provenance where their decisions still stand; focused owners and current repository status govern conflicts.
