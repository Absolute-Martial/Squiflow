# SquiFlow

**Current architecture/development baseline: `v0.0.20`**  
**Implementation state:** Phase 0A and Phase 0B are **Complete / Qualified**. Phase 0E is **IN PROGRESS** with the second real capability slice. Phase 0C remains `NOT_INTRODUCED` until a real executable responsibility earns it.

SquiFlow is rebuilding from accepted architecture and current responsibility rather than carrying premature implementation or speculative phase structure forward. Earlier code and retired plans remain in Git history as evidence/context, not current authority.

Reset decision: `docs/decisions/PRINCIPLES_FIRST_RESET_2026-09-14.md`.  
Gate model: `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`.  
Evidence/permanence model: `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`.  
Active/carry-forward work: `docs/implementation/IMPLEMENTATION_TODO.md`.

## Read in this order

1. `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md` — canonical scope/quality contract.
2. `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` — evidence permanence/requalification contract.
3. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` — qualified reset baseline and enduring 0A guarantees.
4. `docs/implementation/phases/phase-0/0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md` and `0B_STATUS.md` — qualified 0B gate and evidence record.
5. `docs/implementation/phases/phase-0/0E_ACTIVE_CAPABILITY_AND_TRACK_DEVELOPMENT.md` and `0E_STATUS.md` — active second-capability slice.
6. `docs/implementation/IMPLEMENTATION_TODO.md` — active blockers and trigger-only carry-forward.
7. `docs/domain/BUSINESS_TERMS.md` and `BUSINESS_MODEL.md` — accepted versus discovery-sensitive terminology and practical domain semantics.
8. `docs/architecture/ENGINEERING_PRINCIPLES.md` and `EXPLICIT_BOUNDARIES_AND_SOLID.md` — engineering/dependency rules.
9. `docs/decisions/CURRENT_DECISIONS.md` and `OPEN_DECISIONS.md` — accepted versus unresolved direction.
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

The active 0E branch contains four projects:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
modules/payments/SquiFlow.Payments/SquiFlow.Payments.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
tests/unit/SquiFlow.Payments.Tests/SquiFlow.Payments.Tests.csproj
```

There is no Foundation/ApplicationKernel project and no application/service executable.

Qualified production semantics currently include only:

```text
PartyKind
├── Person
└── Organization
```

Active 0E introduces only this documented Payments vocabulary:

```text
PaymentStatus
├── NotStarted
├── Pending
├── Succeeded
├── Failed
├── OutcomeUnknown
├── PartiallyRefunded
├── Refunded
└── Reversed
```

The Payment slice does **not** claim payment identity, amount/currency/rounding, transition rules, retry/idempotency, reconciliation, settlement, persistence, API, authorization, or runtime behavior. Those remain `NOT_INTRODUCED`.

The existence of two capabilities does not itself justify shared Foundation. Parties and Payments remain independently owned and currently have no project/package dependency.

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

## Phase-0 progression

0A established the clean repository/authority baseline.

0B proved the capability-first rule: `PartyKind` remains Party-owned, its compile-time dependency boundary is mechanically protected, and `SquiFlow.ApplicationKernel` remains absent because no real current shared product-wide primitive was earned.

0C is deliberately not active because there is still no executable with a real current job to perform. Creating an empty CoreApi/Workstation/Web/Guard process would violate the earned-boundary rule.

0E is active because `BUSINESS_MODEL.md` already defines a concrete Payment status vocabulary. That provides a real second capability without hardening discovery-sensitive `Order/Job/Sale` or `Customer/Account` semantics.

## Current verification status

Repository-owned verification is:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

Qualified 0B has successful GitHub Actions evidence for its then-current Parties solution: run `34916005915`, job `104213630182`.

The 0E slice changes C#, projects, tests, and the solution, so a new execution is required. GitLab pipeline `#196` (`2849161905`) created `verify-dotnet` but failed before runner start with `ci_quota_exceeded`; no `dotnet` command executed. Therefore the active Payment semantic/dependency claims remain `BLOCKED` pending successful executable evidence, normally through the maintainer-managed GitHub mirror while GitLab quota remains unavailable.

## TODO and future governance

`docs/implementation/IMPLEMENTATION_TODO.md` records:

- active introduced work that must be finished or un-introduced;
- operational follow-up that does not reopen a qualified phase;
- trigger-only future responsibilities that remain `NOT_INTRODUCED` until earned.

Phase 2–10 remain direction-only planning until real work earns detail. `FUTURE_PHASE_CARRY_FORWARD.md` preserves anticipation without turning it into specification.

## Versioning

`v0.0.20` marks the principles-first reset baseline and qualified 0A/0B foundation. The active 0E branch is subsequent earned implementation work under that baseline; focused owners and current repository status govern conflicts.