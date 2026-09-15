# SquiFlow production-honest implementation baseline

**Baseline:** v0.0.20  
**State:** Phase 0A and Phase 0B are qualified. The repository contains the first capability-owned implementation slice, but no application/service runtime or Foundation/ApplicationKernel project exists.

The previous Phase-0 implementation was intentionally purged. Its history remains available through Git as evidence/context, but it is not code or governance to recreate mechanically.

## Current implementation truth

Qualified 0B contains exactly:

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

Shared Foundation/ApplicationKernel remains `NOT_INTRODUCED` because real 0B capability work did not expose shared product-wide semantics that justified extraction.

The qualified gate/evidence record is `docs/implementation/phases/phase-0/0B_STATUS.md`.

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

## Qualified 0B outcome

0B did not rebuild `SquiFlow.ApplicationKernel` or satisfy a reuse quota.

The first slice started inside `modules/parties/` because `Party = person or organization` is accepted domain meaning while several stronger labels remain discovery-sensitive. The code remains Party-owned.

Real capability work did not expose a current product-wide primitive, so Foundation remains `NOT_INTRODUCED`. This is the qualified discovery result. If future real work creates shared product-wide pressure, a Foundation primitive may then be introduced under a new explicit scope/evidence claim.

No second module was manufactured to justify reuse.

## Verification

The first test project exists because real code exists. It uses xUnit v3 + Microsoft Testing Platform under the .NET 10 SDK.

The repository-owned contract is:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

The GitHub Actions workflow executed that contract successfully in maintainer-supplied run `34916005915`, job `104213630182`:

`https://github.com/Absolute-Martial/Squiflow/actions/runs/34916005915/job/104213630182`

GitLab CI uses root `.gitlab-ci.yml` and schedules the same contract. Its current hosted jobs are prevented from starting by `ci_quota_exceeded`; this is recorded as an infrastructure non-claim rather than hidden or misrepresented as a test failure.

The Party-kind and no-outward-dependency claims are therefore `PRODUCTION_HONEST`, and qualified 0B has `BLOCKED = none`.

## Future governance

Phase 0 and the already-concrete Phase 1 trust boundary have detailed governance. Phase 2–10 remain direction-only `NOT_INTRODUCED` planning until real work earns detail. `FUTURE_PHASE_CARRY_FORWARD.md` preserves anticipation without turning it into specification.

0B completion does not automatically authorize 0C. Start the next slice from real current product/capability/runtime pressure and activate the matching responsibility area only when earned.

## CI/CD direction

Dual-host CI remains an earned verification boundary because real executable code exists and GitLab hosted capacity is currently unavailable.

- `.gitlab-ci.yml` is the GitLab wrapper.
- `.github/workflows/verify-dotnet.yml` is the GitHub wrapper.
- both invoke the same repository-owned restore/build/test commands;
- `global.json` remains the SDK authority rather than duplicating the pinned SDK version in the GitHub workflow;
- both use change filters so a branch/review whose diff contains no executable/build input can skip this verification job.

Change filters are not a promise that every documentation-only commit is free: in an already code-changing MR/PR, the provider may compare the review against its target branch and retrigger verification after a documentation-only commit. The invariant is that GitLab and GitHub verify the same executable contract, not that CI will never rerun for documentation activity.

The two CI systems are redundant execution hosts, not separate build definitions. Do not move build meaning into host-specific scripts or let GitLab and GitHub verify different contracts.
