# Phase 0A — Architecture Baseline Reconciliation

**Status:** reset baseline established; implementation qualification starts with the rebuild  
**Current evidence ledger:** `0A_BASELINE_STATUS.md`

## Purpose

0A establishes a truthful relationship between accepted architecture, engineering principles, open decisions, Git history, and the repository that actually exists before new implementation is introduced.

## Source precedence

```text
focused canonical owner
        ↓
CURRENT_DECISIONS / accepted focused decision record
        ↓
implementation phase package
        ↓
master planning synthesis
        ↓
historical review / source-study / chat / old branch or MR material
```

Git history preserves why earlier code/boundaries existed; it does not make deleted implementation current.

## Current baseline to preserve

The reset preserves accepted architecture including:

- .NET 10/C# application direction;
- Avalonia Workstation, Blazor tenant Web, ASP.NET Core server directions;
- modular-monolith business ownership and in-process ordinary module communication;
- `Foundation → capability business meaning → host/provider adapter → executable composition root` dependency direction;
- one source implementation of business meaning per capability;
- PostgreSQL central authority and SQLite/WAL Workstation local/provisional persistence directions;
- server authority for protected shared/security-sensitive decisions;
- ZITADEL, OpenFGA, and OpenBao/Vault-style provider directions;
- Guard as an external supervision/recovery boundary when implemented;
- private Platform Admin control-plane direction;
- schema/contract compatibility and workload-driven transport/process/provider adoption.

The reset additionally makes `docs/architecture/ENGINEERING_PRINCIPLES.md` and `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md` mandatory development owners.

## Current repository reality

At v0.0.20 there are no production/test `.csproj` projects. `SquiFlow.sln` is an empty rebuild container. Architecture, decisions, requirements, reviews, file-structure samples, and phase packages remain.

Exact evidence is in `0A_BASELINE_STATUS.md`.

## Reset rule

Do not recreate the old project tree automatically. Every new responsibility must be introduced from:

```text
accepted requirement/decision
→ explicit owner / authority / state boundary
→ applicable edge/failure/recovery/security/compatibility analysis
→ simplest complete design
→ only earned project/folder/interface/process boundaries
→ verification alongside implementation
```

KISS means complete simplicity, not happy-path minimalism. YAGNI prevents speculative breadth, not necessary behavior of a responsibility already introduced.

## Exit questions

A developer must be able to answer without guessing:

1. Which document owns the architecture topic?
2. What is accepted versus open?
3. What implementation actually exists now?
4. Why does each introduced boundary exist?
5. What current behavior/failure/security/compatibility does it claim?
6. What evidence proves those claims?
7. Which later gate owns deferred maturity without excusing current incompleteness?

0A does not freeze the architecture. It establishes the rules under which implementation may restart.
