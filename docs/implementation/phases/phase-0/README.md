# Phase 0 — Architectural Development Foundation

**Status:** COMPLETE / QUALIFIED  
**Current baseline:** v0.0.20  
**Integrated qualification record:** `0F_STATUS.md`  
**Carry-forward/TODO record:** `docs/implementation/IMPLEMENTATION_TODO.md`  
**High-level owner:** `docs/implementation/PHASES_AND_GATES.md`  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

Phase 0 was developed as a sequence of **smallest production-honest scopes**, not as a checklist whose labels authorize implementation.

## Qualified repository state

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
modules/payments/SquiFlow.Payments/SquiFlow.Payments.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
tests/unit/SquiFlow.Payments.Tests/SquiFlow.Payments.Tests.csproj
```

Qualified semantics are intentionally narrow:

```text
PartyKind = Person | Organization
PaymentStatus = NotStarted | Pending | Succeeded | Failed | OutcomeUnknown |
                PartiallyRefunded | Refunded | Reversed
```

There is no Foundation/ApplicationKernel project, application/service executable, persistence/provider adapter, sync/runtime security implementation, complete Party/Customer model, or complete payment aggregate.

## Phase-0 work-area result

```text
0A  Architecture/repository baseline reconciliation                         QUALIFIED
0B  Capability-first discovery of any genuinely shared kernel/foundation     QUALIFIED
0C  Host/process composition when a real executable earns the boundary       NOT INTRODUCED
0D  Engineering safety/observability/reproducibility as real code needs it    QUALIFIED for introduced subset
0E  Active capability/extensibility work through real product slices          QUALIFIED
0F  Integrated Phase-0 qualification                                           QUALIFIED
```

0C is intentionally absent. 0F explicitly permits Phase 0 to close with future hosts/providers/runtime responsibilities `NOT_INTRODUCED`; it does not require empty executables for checklist symmetry.

## What Phase 0 proved

- current authority/history/implementation truth remain distinguishable;
- capability semantics remain capability-owned;
- Parties and Payments have mechanical no-outward-dependency guards;
- a second real capability can be introduced without creating cross-capability coupling;
- shared Foundation remains absent when no real shared semantic exists;
- repository build/test behavior is reproducible through one provider-neutral command contract;
- deterministic/analyzed build defaults and CI verification accompany real code rather than being postponed;
- runtime observability/resource/security/deployment mechanisms are not manufactured before runtime exists;
- every introduced Phase-0 responsibility is `PRODUCTION_HONEST` and `BLOCKED = none`.

## Verification

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

Current qualified evidence is maintainer-supplied GitHub Actions run `34922277237`, job `104232827231`:

`https://github.com/Absolute-Martial/Squiflow/actions/runs/34922277237/job/104232827231`

GitLab accepts/schedules the same job but hosted quota currently prevents runner start. That is tracked as operational follow-up and is not a Phase-0 blocker.

## Permanent guards

0A–0F qualified claims keep their owning regression protections after Phase 0:

- source precedence and production-honesty governance;
- Parties semantic/dependency tests;
- Payments semantic/dependency tests;
- repository-owned .NET toolchain/build settings;
- one restore/build/test contract across GitHub/GitLab wrappers;
- explicit requalification when qualified dependency/toolchain/CI boundaries materially change.

## Carry-forward

`docs/implementation/IMPLEMENTATION_TODO.md` has no active blocker. It records only operational follow-up and trigger-dependent `NOT_INTRODUCED` work, including Foundation, host/process, Party, Payments and Phase-1 runtime trust/security triggers.

Phase 2–10 remain direction-only planning until real work earns detail. `FUTURE_PHASE_CARRY_FORWARD.md` preserves anticipation without turning it into specification.

## Handoff

Phase 0 completion does not automatically authorize Phase-1 provider/runtime scaffolding. The next implementation slice again starts from a real current product/runtime responsibility.

Phase-1 runtime trust/security activates when a real protected/reachable user or tenant operation needs identity, authorization, session or provider-backed trust guarantees. If a different responsibility becomes real first, activate its owner and production-honesty obligations instead.
