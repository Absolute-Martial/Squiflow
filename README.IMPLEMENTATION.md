# SquiFlow production-honest implementation baseline

**Baseline:** v0.0.20  
**State:** Phase 0A is qualified; Phase 0B is IN PROGRESS with the first capability-owned implementation slice. No application/service runtime or Foundation/ApplicationKernel project exists.

The previous Phase-0 implementation was intentionally purged. Its history remains available through Git as evidence/context, but it is not code or governance to recreate mechanically.

## Current implementation truth

The active 0B branch contains exactly:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
```

`SquiFlow.sln` contains those two projects and no executable host/service project.

The current capability claim is deliberately narrow:

```text
PartyKind
= Person | Organization
```

This comes from the accepted Party meaning in `docs/domain/BUSINESS_TERMS.md`. The slice does not claim Party identity encoding, lifecycle, contact/profile fields, Customer/Account/Commercial Relationship semantics, persistence, API, host, synchronization, authorization, or a complete Party capability.

Shared Foundation/ApplicationKernel remains `NOT_INTRODUCED` because current code has not earned shared product-wide semantics.

Current gate/evidence state is recorded in `docs/implementation/phases/phase-0/0B_STATUS.md`.

## Governing model

Read these first:

- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`;
- `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md`;
- `docs/implementation/phases/phase-0/0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md`;
- `docs/implementation/phases/phase-0/0B_STATUS.md`;
- `docs/domain/BUSINESS_TERMS.md`;
- `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`;
- `docs/architecture/ENGINEERING_PRINCIPLES.md`;
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`;
- `docs/decisions/CURRENT_DECISIONS.md` and `OPEN_DECISIONS.md`;
- the focused owner for the responsibility being implemented.

The phase state model is:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

Only genuinely absent scope is `NOT_INTRODUCED` and deferrable. An introduced responsibility that is not yet trustworthy/proven is `BLOCKED`; it must be finished or un-introduced before its gate passes.

## Implementation rule

For each real slice:

```text
real requirement / responsibility / workload
        ↓
focused owner + current/open decisions
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

## 0A permanence

0A's zero-project state was qualification-time reset evidence, not a forever-rule. Its enduring guarantees remain active: current authority must remain distinguishable from history, folders/diagrams do not prove implementation, boundaries are newly earned, future governance specificity is earned, and active claims require durable evidence/regression protection.

The first 0B projects are therefore an expected transition, not a regression of 0A.

## Why 0B is capability-first

0B does not start by rebuilding `SquiFlow.ApplicationKernel` or satisfying a reuse quota.

The first slice starts inside `modules/parties/` because `Party = person or organization` is accepted domain meaning while several stronger labels remain discovery-sensitive. The code intentionally stays Party-owned. If future real work creates shared product-wide pressure, a Foundation primitive may then be extracted under an explicit new scope claim.

No second module will be manufactured to justify reuse.

## Verification

The first test project exists because real code now exists. It uses xUnit v3 + Microsoft Testing Platform under the .NET 10 SDK.

Required evidence:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

Those commands have not been claimed as passing in the assistant environment because the .NET SDK is unavailable there. Until equivalent executable evidence passes, the introduced Party-kind and dependency-boundary claims remain `BLOCKED` and 0B cannot pass.

## Future governance

Phase 0 and the already-concrete Phase 1 trust boundary have detailed governance. Phase 2–10 remain direction-only `NOT_INTRODUCED` planning until real work earns detail. `FUTURE_PHASE_CARRY_FORWARD.md` preserves anticipation without turning it into specification.

## CI/CD direction

Repository CI/CD may be introduced when real verification needs it. GitHub/GitLab should remain thin orchestration over repository-owned commands. CI/tooling is not created merely to satisfy a phase label.
