# SquiFlow production-honest implementation baseline

**Baseline:** v0.0.20  
**State:** Phase 0 is **COMPLETE / QUALIFIED** for the responsibilities actually introduced. No application/service runtime or Foundation/ApplicationKernel project exists.

The previous Phase-0 implementation was intentionally purged. Its history remains evidence/context, not code or governance to recreate mechanically.

## Current implementation truth

The qualified Phase-0 branch contains exactly:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
modules/payments/SquiFlow.Payments/SquiFlow.Payments.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
tests/unit/SquiFlow.Payments.Tests/SquiFlow.Payments.Tests.csproj
```

`SquiFlow.sln` contains those four projects and no executable host/service project.

Qualified capability scope remains narrow:

```text
PartyKind
= Person | Organization

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

The Payments slice comes directly from `docs/domain/BUSINESS_MODEL.md`. It does not claim payment identity encoding, amount/currency/rounding, transition rules, retry/idempotency, outcome-unknown reconciliation, settlement, persistence, API, host, authorization, or a complete payment aggregate.

Shared Foundation/ApplicationKernel remains `NOT_INTRODUCED` because current capability work has not exposed shared product-wide semantics that justify extraction.

Integrated Phase-0 evidence is recorded in `docs/implementation/phases/phase-0/0F_STATUS.md`. Carry-forward work is recorded in `docs/implementation/IMPLEMENTATION_TODO.md`.

## Governing model

Read first:

- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`;
- `docs/implementation/phases/phase-0/0F_STATUS.md`;
- `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md`;
- `docs/implementation/phases/phase-0/0B_STATUS.md`;
- `docs/implementation/phases/phase-0/0D_STATUS.md`;
- `docs/implementation/phases/phase-0/0E_STATUS.md`;
- `docs/implementation/IMPLEMENTATION_TODO.md`;
- the focused owner for the responsibility being implemented.

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
NOT_INTRODUCED / PRODUCTION_HONEST / BLOCKED
        ↓
simplest production-honest implementation
        ↓
falsifiable evidence
        ↓
permanent / recurring regression guard
        ↓
qualify only with BLOCKED = none
```

## Qualified Phase-0 outcome

0B did not rebuild `SquiFlow.ApplicationKernel` or satisfy a reuse quota. Party meaning remains Party-owned and no product-wide primitive was earned.

0E introduced Payments as a second independent capability from an already-documented semantic. Parties and Payments remain uncoupled and have mechanical no-outward-dependency guards. A second capability still did not create evidence for shared Foundation.

0D is qualified only for responsibilities current code actually introduced: deterministic/analyzed build defaults, repository-owned restore/build/test, architecture guards, and thin CI orchestration. Runtime observability, runtime secret/configuration mechanisms, queues/retries/resource bounds, and deployment remain `NOT_INTRODUCED` because no runtime exists.

0C remains `NOT_INTRODUCED`; Phase 0 completion does not require an empty executable.

0F records the integrated result: every introduced Phase-0 responsibility is `PRODUCTION_HONEST` and `BLOCKED = none`.

## Verification

Repository-owned contract:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

Current qualified four-project evidence:

```text
GitHub Actions run 34922277237
job 104232827231
```

`https://github.com/Absolute-Martial/Squiflow/actions/runs/34922277237/job/104232827231`

GitLab CI uses the same commands. Hosted quota currently prevents its jobs from reaching a runner, so no GitLab runner-success claim is made. Restoring GitLab execution capacity remains operational follow-up, not a Phase-0 blocker.

## TODO / carry-forward

`docs/implementation/IMPLEMENTATION_TODO.md` has no active blocker. It retains only operational follow-up and trigger-dependent `NOT_INTRODUCED` responsibilities.

A TODO entry never permits an introduced `BLOCKED` responsibility to be carried forward as future hardening.

## Next implementation

Do not automatically scaffold Phase 1 merely because Phase 0 is complete. Phase-1 runtime trust/security work activates when a real protected/reachable user or tenant operation needs identity, authorization, session, or provider-backed trust guarantees.

Likewise, Foundation, host composition, persistence, sync, Workstation, Web, Worker and other runtime boundaries remain trigger-driven.
