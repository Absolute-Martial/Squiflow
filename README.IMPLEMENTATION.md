# SquiFlow production-honest implementation baseline

**Baseline:** v0.0.20  
**State:** Phase 0A and Phase 0B are qualified. Phase 0E is **IN PROGRESS** with a second real capability slice. Phase 0C remains `NOT_INTRODUCED` because no executable host has a real current responsibility.

The previous Phase-0 implementation was intentionally purged. Its history remains available through Git as evidence/context, but it is not code or governance to recreate mechanically.

## Current implementation truth

The active branch contains exactly:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
modules/payments/SquiFlow.Payments/SquiFlow.Payments.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
tests/unit/SquiFlow.Payments.Tests/SquiFlow.Payments.Tests.csproj
```

`SquiFlow.sln` contains those four projects and no executable host/service project.

Qualified capability scope:

```text
PartyKind
= Person | Organization
```

Active 0E capability scope:

```text
PaymentStatus
= NotStarted
| Pending
| Succeeded
| Failed
| OutcomeUnknown
| PartiallyRefunded
| Refunded
| Reversed
```

The Payments slice comes directly from the documented status vocabulary in `docs/domain/BUSINESS_MODEL.md`. It does not claim payment identity encoding, amount/currency/rounding, transition rules, retry/idempotency, outcome-unknown reconciliation, settlement, persistence, API, host, authorization, or a complete payment aggregate.

Shared Foundation/ApplicationKernel remains `NOT_INTRODUCED` because current capability work has not exposed shared product-wide semantics that justify extraction.

Qualified 0B evidence is recorded in `docs/implementation/phases/phase-0/0B_STATUS.md`. Active 0E scope/evidence/blockers are recorded in `docs/implementation/phases/phase-0/0E_STATUS.md`. Carry-forward work is recorded in `docs/implementation/IMPLEMENTATION_TODO.md`.

## Governing model

Read these first:

- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`;
- `docs/implementation/IMPLEMENTATION_TODO.md`;
- `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md`;
- `docs/implementation/phases/phase-0/0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md`;
- `docs/implementation/phases/phase-0/0B_STATUS.md`;
- `docs/implementation/phases/phase-0/0E_ACTIVE_CAPABILITY_AND_TRACK_DEVELOPMENT.md`;
- `docs/implementation/phases/phase-0/0E_STATUS.md`;
- `docs/domain/BUSINESS_TERMS.md`;
- `docs/domain/BUSINESS_MODEL.md`;
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

## Qualified 0B outcome

0B did not rebuild `SquiFlow.ApplicationKernel` or satisfy a reuse quota.

The first slice started inside `modules/parties/` because `Party = person or organization` is accepted domain meaning while several stronger labels remain discovery-sensitive. The code remains Party-owned.

Real capability work did not expose a current product-wide primitive, so Foundation remains `NOT_INTRODUCED`. This is the qualified discovery result.

## Why 0E is active before 0C

0C requires a real executable responsibility. None exists yet, so creating an empty host would be phase-driven scaffolding.

0E permits real capability work during Phase 0. `BUSINESS_MODEL.md` already defines the Payment status vocabulary, so a second host-neutral capability can be introduced without inventing a customer journey or resolving discovery-sensitive Order/Job/Sale terminology.

This is intentional non-linear Phase-0 progression, not a skipped responsibility.

## Current 0E scope

The Payment slice is deliberately limited to the documented status vocabulary and an independent no-outward-dependency boundary.

It does **not** select:

- payment identity representation;
- amount/currency/rounding rules;
- legal transition graph;
- retry/idempotency semantics;
- outcome-unknown reconciliation;
- settlement/allocation behavior;
- persistence/database/provider behavior;
- API/host/sync/authorization behavior.

Those remain `NOT_INTRODUCED` until a real current operation needs them and the relevant focused/open decisions are resolved.

## Verification

The repository-owned contract is:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

Qualified 0B has successful GitHub Actions evidence for the then-current Parties solution: run `34916005915`, job `104213630182`.

That run cannot qualify 0E because 0E changes C#, projects, tests, and the solution. GitLab pipeline `#196` (`2849161905`) created the `verify-dotnet` job for the first 0E code commit, but the job failed before start with `ci_quota_exceeded`, `runner = null`, and no `dotnet` command executed.

Therefore the active 0E Payment-status and Payments-dependency claims remain `BLOCKED` pending a new successful executable run, normally through the maintainer-managed GitHub mirror while GitLab quota remains unavailable.

## TODO / carry-forward

`docs/implementation/IMPLEMENTATION_TODO.md` separates:

- current active blockers that must be finished or un-introduced;
- operational follow-up that does not reopen qualified 0B;
- future trigger-only responsibilities that remain `NOT_INTRODUCED`.

A TODO entry never permits an introduced `BLOCKED` responsibility to be carried into later work as if it were future scope.

## CI/CD direction

GitLab and GitHub remain redundant execution hosts over one repository-owned verification contract:

- `.gitlab-ci.yml` is the GitLab wrapper;
- `.github/workflows/verify-dotnet.yml` is the GitHub wrapper;
- `global.json` remains the SDK authority;
- both invoke the same restore/build/test commands.

Do not move build meaning into host-specific scripts or let the two CI systems verify different contracts.
