# SquiFlow

**Current architecture/development baseline: `v0.0.20`**  
**Implementation state:** Phase 0A and Phase 0B are **Complete / Qualified** under the production-honest governance model. No later Phase-0 work area is automatically active; the next slice is derived from real current responsibility.

SquiFlow is rebuilding from accepted architecture and current responsibility rather than carrying premature implementation or speculative phase structure forward. Earlier code and retired plans remain in Git history as evidence/context, not current authority.

Reset decision: `docs/decisions/PRINCIPLES_FIRST_RESET_2026-09-14.md`.  
Gate model: `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`.  
Evidence/permanence model: `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`.

## Read in this order

1. `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md` — canonical scope/quality contract.
2. `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` — evidence permanence/requalification contract.
3. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` — qualified reset baseline and enduring 0A guarantees.
4. `docs/implementation/phases/phase-0/0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md` and `0B_STATUS.md` — qualified 0B gate and evidence record.
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

Qualified 0B contains exactly two projects:

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

The absence of shared Foundation is also intentional: the real Parties slice did not expose current product-wide reuse/change pressure that justified a shared primitive. That is the qualified 0B discovery result, not unfinished work.

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

## 0A → 0B qualification

0A guarantees that a developer can identify current authority, current implementation truth, repository ownership, and future-only scope without relying on deleted code or speculative governance.

Its zero-project inventory was qualification-time reset evidence, not a permanent invariant. 0B legitimately introduced the first projects from current capability semantics rather than recreating the purged tree.

0B then proved the capability-first rule: `PartyKind` remains Party-owned, its compile-time dependency boundary is mechanically protected, and `SquiFlow.ApplicationKernel` remains absent because no real current shared product-wide primitive was earned. No second consumer was manufactured to satisfy a quota.

## Current verification status

The qualified tests cover the Party-kind contract and the initial Parties dependency boundary. The .NET 10 test path uses Microsoft Testing Platform and xUnit v3.

Repository-owned verification is:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

The GitHub Actions workflow executed this verification successfully in maintainer-supplied run `34916005915`, job `104213630182`:

`https://github.com/Absolute-Martial/Squiflow/actions/runs/34916005915/job/104213630182`

GitLab runs the same commands from root `.gitlab-ci.yml`; its hosted jobs currently fail before runner start because quota is exhausted. No GitLab runner-success claim is made, and that infrastructure limitation does not invalidate the successful GitHub execution.

0B therefore has `BLOCKED = none` for its declared scope. See `0B_STATUS.md` for exact claims, non-claims, regression guards, and requalification triggers.

## Future governance

Phase 0 and the already-concrete Phase 1 trust boundary have detailed governance. Phase 2–10 remain direction-only `NOT_INTRODUCED` planning until real work earns detail. `FUTURE_PHASE_CARRY_FORWARD.md` preserves anticipation without turning it into specification.

0B completion does not automatically activate 0C. A host/process/composition boundary is introduced only when a real executable responsibility requires it.

## Versioning

`v0.0.20` marks the principles-first reset baseline and production-honest Phase-0A/0B qualification. Older document labels may remain as provenance where their decisions still stand; focused owners and current repository status govern conflicts.
