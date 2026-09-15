# Phase 0 — Architectural Development Foundation

**Status:** active after principles-first implementation reset  
**Current baseline:** v0.0.20  
**Completed/qualified subphases:** 0A and 0B under the production-honest model  
**Active work area:** 0E — second capability / Payments slice  
**Deferred work area:** 0C remains `NOT_INTRODUCED` until a real executable responsibility exists  
**0B qualification record:** `0B_STATUS.md`  
**0E active record:** `0E_STATUS.md`  
**Carry-forward/TODO record:** `docs/implementation/IMPLEMENTATION_TODO.md`  
**High-level owner:** `docs/implementation/PHASES_AND_GATES.md`  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

Phase 0 is developed as a sequence of **smallest production-honest scopes**, not as a checklist whose prewritten boxes authorize implementation.

## Current repository state

0A's reset snapshot contained no production/test projects. Qualified 0B introduced the first earned capability boundary. Active 0E has now introduced a second capability boundary:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
modules/payments/SquiFlow.Payments/SquiFlow.Payments.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
tests/unit/SquiFlow.Payments.Tests/SquiFlow.Payments.Tests.csproj
```

The qualified Parties scope is only the accepted `PartyKind = Person | Organization` semantic and its no-outward-dependency boundary.

The active Payments scope is only the documented payment-status vocabulary:

```text
NotStarted
Pending
Succeeded
Failed
OutcomeUnknown
PartiallyRefunded
Refunded
Reversed
```

No Foundation/ApplicationKernel project, application/service executable, persistence/provider adapter, sync/runtime security implementation, complete Party/Customer model, payment amount/currency/transition/persistence model, or payment runtime is claimed.

0B remains complete with `BLOCKED = none`. Its introduced semantic/dependency claims are executable-evidence-backed, while shared Foundation remains deliberately `NOT_INTRODUCED` because the real capability work has not earned a product-wide abstraction.

0E is **IN PROGRESS** and currently `BLOCKED` on executable evidence for the newly introduced Payments claims. See `0E_STATUS.md`.

Read first:

- `0A_ARCHITECTURE_BASELINE_RECONCILIATION.md`;
- `0A_BASELINE_STATUS.md`;
- `0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md`;
- `0B_STATUS.md`;
- `0E_ACTIVE_CAPABILITY_AND_TRACK_DEVELOPMENT.md`;
- `0E_STATUS.md`;
- `docs/implementation/IMPLEMENTATION_TODO.md`;
- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`;
- `docs/implementation/PHASES_AND_GATES.md`;
- `docs/domain/BUSINESS_TERMS.md`;
- `docs/domain/BUSINESS_MODEL.md`;
- `docs/architecture/ENGINEERING_PRINCIPLES.md`;
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`;
- `docs/architecture/REPOSITORY_STRUCTURE.md`;
- `docs/architecture/REPOSITORY_FOLDER_STRUCTURE.md`.

## Development rule

For every active slice:

```text
real responsibility / workload
        ↓
production intent
        ↓
exact declared scope
        ↓
NOT_INTRODUCED / PRODUCTION_HONEST / BLOCKED
        ↓
implementation
        ↓
falsifiable evidence at the owning layer
        ↓
permanent / recurring regression protection
        ↓
qualification with BLOCKED = none
```

A phase/subphase name is planning vocabulary. It does not authorize a project, provider, interface, process, technology, test matrix, or runtime behavior by itself.

## Rebuild rule

Do not rebuild by reproducing the previous folder/project list. Reintroduce a boundary only when a real declared scope needs it and the pressure/ownership is current.

A component may be narrow, but anything introduced on a claimed path must be production-honest for that exact claim. A known incomplete/unproven responsibility is `BLOCKED`; it cannot be labeled future hardening and carried forward.

Qualified 0B demonstrated this direction:

```text
accepted Party semantic
        ↓
capability-owned code first
        ↓
observe real reuse/change pressure
        ↓
no product-wide primitive earned yet
        ↓
Foundation remains NOT_INTRODUCED
```

Active 0E applies the same rule to a second capability:

```text
accepted Payment-status semantic
        ↓
independent Payments capability
        ↓
no Parties dependency
        ↓
no Foundation extraction unless a real shared semantic appears
```

Do not create ApplicationKernel/module-composition machinery merely because older plans expected it. Do not create an empty host merely because 0C is numerically before 0E.

## KISS / YAGNI

KISS means minimum accidental complexity and the **smallest production-honest scope**. It never means happy-path only, fewest files at any cost, skipped recovery/security/compatibility, fake durability/authority, or hidden edge cases.

YAGNI removes speculative breadth. It does not weaken the depth required by a responsibility already introduced.

## Scope states

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

Only `NOT_INTRODUCED` can be deferred. `BLOCKED` must be finished to `PRODUCTION_HONEST` or explicitly un-introduced before the owning gate can pass.

## Phase-0 work areas

```text
0A  Architecture/repository baseline reconciliation                         QUALIFIED
0B  Capability-first discovery of any genuinely shared kernel/foundation     QUALIFIED
0C  Host/process composition when a real executable earns the boundary       NOT INTRODUCED
0D  Engineering safety/observability/reproducibility as real code needs it    NOT INTRODUCED unless pulled forward
0E  Active capability/extensibility work through real product slices          IN PROGRESS
0F  Integrated Phase-0 qualification when enough real Phase-0 scope exists    NOT INTRODUCED
```

These labels describe useful responsibility areas. They are not permission to prebuild all listed concerns, and their internal scope may be reshaped as real work provides better facts.

## 0A permanence

0A's enduring guarantees remain active after later code appears:

- current authority must remain distinguishable from history;
- folder/diagram presence must not be mistaken for implementation;
- project/process/provider boundaries remain earned;
- the production-honest state model governs introduced scope;
- evidence must remain traceable and protected from silent regression;
- future governance specificity remains earned.

## 0B permanence

0B's enduring guarantees are:

- capability semantics remain owned by their capability;
- real compile-time dependency boundaries are mechanically protected;
- shared Foundation is extracted only from actual product-wide pressure rather than phase symmetry;
- absence of Foundation is a valid qualified result when real slices do not earn it;
- no executable/process topology is encoded into capability metadata;
- the Party semantic/dependency regression tests and repository verification contract remain active until deliberately superseded.

## 0E current intent

The current 0E slice exists to prove that another real capability can be added without changing the fundamental dependency direction and without turning two modules into an excuse for a shared framework.

The Payment status vocabulary is used because it is explicitly documented in `BUSINESS_MODEL.md`; discovery-sensitive Order/Job/Sale and Customer/Account distinctions are not hardened by this slice.

## Future-phase relationship

Phase 2–10 are direction-only `NOT_INTRODUCED` governance. Their retired detailed plans remain history; useful anticipation belongs in `FUTURE_PHASE_CARRY_FORWARD.md` and cannot become success criteria merely because it was written earlier.

If a current Phase-0 slice genuinely needs a later responsibility, pull it forward from current facts and qualify it now. Do not create a temporary unsafe substitute or resurrect a retired future subphase file.

## Verification

The repository-owned verification contract is:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

Qualified 0B has successful GitHub evidence for the then-current Parties-only solution: run `34916005915`, job `104213630182`.

Because 0E adds C#, projects, tests, and solution entries, the 0B run cannot qualify the new Payments claims. GitLab pipeline `#196` (`2849161905`) created `verify-dotnet` but failed before start with `ci_quota_exceeded`, no runner assigned. Therefore active 0E remains `BLOCKED` pending a new successful execution, normally through the maintainer-managed GitHub mirror while GitLab quota is unavailable.

Once a material claim qualifies, its regression guard remains active until the claim is retired or superseded deliberately.

## CI/CD

GitLab and GitHub remain two execution hosts for one repository-owned verification contract:

```text
GitLab: .gitlab-ci.yml
GitHub: .github/workflows/verify-dotnet.yml
             ↓
       same repository-owned
       restore/build/test commands
```

Do not create separate GitLab/GitHub build semantics, host-specific business tests, or custom tooling trees merely to support mirroring.

## TODO / carry-forward discipline

`docs/implementation/IMPLEMENTATION_TODO.md` records active blockers and trigger-dependent work that remains after a gate qualifies.

A TODO entry does not reopen a completed gate merely because a future trigger may require new work. Conversely, an active introduced responsibility cannot be moved into TODO to hide `BLOCKED` state.

## Handoff

Do not activate 0C until a real executable operation exists. Continue 0E only through real capability/product pressure and stop whenever an introduced responsibility becomes `BLOCKED` pending evidence.