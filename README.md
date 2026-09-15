# SquiFlow

**Current architecture/development baseline: `v0.0.20`**  
**Implementation state:** Phase 0 is **COMPLETE / QUALIFIED** for the responsibilities actually introduced. The repository currently has two qualified capability projects and their tests; no Foundation/ApplicationKernel project or application/service executable exists.

SquiFlow is rebuilding from accepted architecture and current responsibility rather than carrying premature implementation or speculative phase structure forward. Earlier code and retired plans remain in Git history as evidence/context, not current authority.

Reset decision: `docs/decisions/PRINCIPLES_FIRST_RESET_2026-09-14.md`.  
Gate model: `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`.  
Evidence/permanence model: `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`.  
Carry-forward work: `docs/implementation/IMPLEMENTATION_TODO.md`.

## Read in this order

1. `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md` — canonical scope/quality contract.
2. `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` — evidence permanence/requalification contract.
3. `docs/implementation/phases/phase-0/0F_STATUS.md` — integrated qualified Phase-0 state.
4. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md`, `0B_STATUS.md`, `0D_STATUS.md`, and `0E_STATUS.md` — detailed qualified Phase-0 evidence.
5. `docs/implementation/IMPLEMENTATION_TODO.md` — operational follow-up and trigger-dependent future work.
6. `docs/domain/BUSINESS_TERMS.md` and `BUSINESS_MODEL.md` — accepted versus discovery-sensitive terminology and practical domain semantics.
7. `docs/architecture/ENGINEERING_PRINCIPLES.md` and `EXPLICIT_BOUNDARIES_AND_SOLID.md` — engineering/dependency rules.
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

The qualified Phase-0 branch contains four projects:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
modules/payments/SquiFlow.Payments/SquiFlow.Payments.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
tests/unit/SquiFlow.Payments.Tests/SquiFlow.Payments.Tests.csproj
```

Qualified capability semantics are deliberately narrow:

```text
PartyKind
├── Person
└── Organization

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

The Payments capability does **not** claim payment identity, amount/currency/rounding, transition rules, retry/idempotency, reconciliation, settlement, persistence, API, authorization, or runtime behavior. The Parties capability does not claim complete Party/Customer/Account behavior. Those remain `NOT_INTRODUCED` until real current work earns them.

Parties and Payments remain independently owned and currently have no project/package dependency. Their coexistence does not itself justify shared Foundation.

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

> **Scope is a choice; honesty is not.**

A phase label is not authority to build its imagined contents. Work proceeds from a real current responsibility:

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

## Qualified Phase-0 result

- 0A qualified the repository/authority/reset baseline.
- 0B qualified capability-first ownership and proved that shared Foundation can remain absent when no shared semantic is earned.
- 0C remains `NOT_INTRODUCED` because no executable has a real current job to perform.
- 0D qualified the build/verification/architecture-safety responsibilities that current project-only implementation actually introduced; runtime observability/resource/deployment responsibilities remain absent.
- 0E qualified the second real capability and cross-capability dependency direction.
- 0F integrated those claims with `BLOCKED = none`.

This is a valid Phase-0 completion. Phase 0 does not require empty hosts, speculative providers, or an ApplicationKernel project.

## Verification

Repository-owned verification is:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

Qualified current four-project evidence is maintainer-supplied GitHub Actions run `34922277237`, job `104232827231`:

`https://github.com/Absolute-Martial/Squiflow/actions/runs/34922277237/job/104232827231`

Earlier qualified 0B evidence is run `34916005915`, job `104213630182` for the then-current Parties-only solution.

GitLab schedules the same repository-owned job but hosted quota currently prevents runner start. That operational limitation is tracked in `IMPLEMENTATION_TODO.md`; it is not represented as a code failure or GitLab runner-success claim.

## Next work

Phase completion does not automatically activate the next numeric phase. `IMPLEMENTATION_TODO.md` records the triggers for future Foundation, host/process, Party, Payments, and Phase-1 trust/security work.

Phase-1 runtime implementation begins only when a real reachable user/tenant responsibility requires authenticated identity, authorization/session behavior, or another concrete trust boundary. Until then, accepted security architecture remains direction/governance rather than runtime scaffolding.

## Versioning

`v0.0.20` remains the principles-first reset baseline. The qualified Phase-0 capability and engineering-safety work is subsequent earned implementation under that baseline; focused owners and current repository status govern conflicts.
