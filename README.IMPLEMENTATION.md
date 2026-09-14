# SquiFlow production-honest implementation baseline

**Baseline:** v0.0.20  
**State:** Phase 0A is qualified under the production-honest/earned-governance model. This qualification branch itself remains implementation-empty; subsequent active work may legitimately introduce the first projects when current responsibility earns them.

The previous Phase-0 implementation was intentionally purged. Its history remains available through Git as evidence/context, but it is not code or governance to recreate mechanically.

## Phase-0A implementation truth

There are no `*.csproj` production/test projects on this qualification branch. `SquiFlow.sln` is an empty implementation container. Source-area folders and architecture examples reserve ownership/growth direction only.

The closed historical 0B branch/MR is not active implementation authority. Any useful idea from it must be independently re-earned from current requirements/owners before reuse.

The zero-project state is qualification evidence, **not** a permanent invariant. A later stacked/merged 0B branch may legitimately add projects without invalidating 0A, provided those boundaries are newly earned and the current-state owners are reconciled.

## Governing model

Read these first:

- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`;
- `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md`;
- `docs/implementation/phases/phase-0/0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md`;
- `docs/architecture/ENGINEERING_PRINCIPLES.md`;
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`;
- `docs/architecture/REPOSITORY_STRUCTURE.md`;
- `docs/decisions/CURRENT_DECISIONS.md`;
- the focused owner for the real responsibility being implemented.

The phase state model is:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

Only genuinely absent scope is `NOT_INTRODUCED` and deferrable. An introduced responsibility that is not yet trustworthy is `BLOCKED`; it must be finished or un-introduced before its gate passes.

## Rebuild rule

Do not restore the old tree or an old phase implementation by memory. For each real slice:

```text
real requirement / responsibility / workload
        ↓
focused owner + authority/state boundary
        ↓
production intent
        ↓
exact scope / non-scope
        ↓
material correctness / failure / recovery / security / compatibility / resource obligations
        ↓
simplest production-honest design
        ↓
project/folder/interface/process/provider only if earned
        ↓
falsifiable evidence at the owning layer
        ↓
permanent / recurring regression guard + requalification trigger
        ↓
qualify only with BLOCKED = none
```

KISS means the smallest production-honest scope, not prototype-grade behavior. YAGNI prevents speculative breadth, not necessary depth for a responsibility already introduced.

## 0A qualification

0A is a developer/repository gate. Its production intent is that an unfamiliar developer can identify the qualification-time implementation truth, governing owner/decision precedence, repository placement rules, and future-only scope without depending on purged code or speculative governance.

Qualification evidence includes:

- zero production/test `*.csproj` projects in the reset snapshot;
- an empty `SquiFlow.sln` implementation container;
- `VERSION` and `CURRENT_VERSION.txt` aligned at `v0.0.20`;
- .NET 10 SDK marker in `global.json`;
- explicit source precedence and scoped repository instructions;
- structure docs that distinguish folders/growth maps from implementation;
- canonical production-honesty and evidence-permanence owners;
- Phase 2–10 demoted to direction-only governance with future anticipation separated from current contracts;
- higher-precedence architecture/server/master-plan owners reconciled so they no longer present deleted Customers/CoreApi/Worker/Admin/Web/Sync projects as current runtime.

## Starting 0B under the new model

0B does not start by rebuilding `SquiFlow.ApplicationKernel` or by satisfying a reuse quota.

Start from a real capability/application responsibility. If that work creates real current pressure for a product-wide module/kernel primitive, declare the exact shared scope and prove it production-honest. If the pressure does not exist, keep the semantics capability-owned and do not create Foundation merely because an older branch did.

A shared abstraction is justified by real current reuse/change/ownership pressure, not by manufactured consumers or phase symmetry.

## CI/CD and verification

0A itself has no executable product/test claim and therefore no `dotnet test` gate. Executable verification begins when an executable/project responsibility is actually introduced by later active work.

When that happens, local and CI verification should exercise the same repository-owned commands. GitHub/GitLab remain thin orchestration layers; CI/tooling is not created merely to make a phase look complete.
