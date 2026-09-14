# Phase 0 — Architectural Development Foundation

**Status:** active after principles-first implementation reset  
**Current baseline:** v0.0.20  
**Completed/qualified subphase:** 0A under the production-honest model  
**Active subphase:** 0B — capability-first foundation discovery  
**Active status record:** `0B_STATUS.md`  
**High-level owner:** `docs/implementation/PHASES_AND_GATES.md`  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

Phase 0 is developed as a sequence of **smallest production-honest scopes**, not as a checklist whose prewritten boxes authorize implementation.

## Current repository state

0A's reset snapshot contained no production/test projects. The active 0B branch now contains the first earned implementation boundary:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
```

The current production-code scope is only the accepted Party structural distinction `Person | Organization`. No Foundation/ApplicationKernel project, application/service executable, persistence/provider adapter, sync/runtime security implementation, or complete Party/Customer model is claimed.

The introduced 0B code claims and CI execution path remain `BLOCKED` until executable restore/build/test evidence passes. See `0B_STATUS.md`.

Read first:

- `0A_ARCHITECTURE_BASELINE_RECONCILIATION.md`;
- `0A_BASELINE_STATUS.md`;
- `0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md`;
- `0B_STATUS.md`;
- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`;
- `docs/implementation/PHASES_AND_GATES.md`;
- `docs/domain/BUSINESS_TERMS.md`;
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

The active 0B slice follows this direction deliberately:

```text
accepted Party semantic
        ↓
capability-owned code first
        ↓
observe real reuse/change pressure
        ↓
extract shared Foundation only if earned
```

Do not create ApplicationKernel/module-composition machinery merely because the subphase title contains “Application Kernel and Module Foundation.”

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
0B  Capability-first discovery of any genuinely shared kernel/foundation     IN PROGRESS
0C  Host/process composition when a real executable earns the boundary
0D  Engineering safety/observability/reproducibility as real code needs it
0E  Active capability/extensibility work through real product slices
0F  Integrated Phase-0 qualification when enough real Phase-0 scope exists
```

These labels describe currently useful responsibility areas. They are not permission to prebuild all listed concerns, and their internal scope may be reshaped as real work provides better facts.

## 0A permanence

0A's enduring guarantees remain active after later code appears:

- current authority must remain distinguishable from history;
- folder/diagram presence must not be mistaken for implementation;
- project/process/provider boundaries remain earned;
- the production-honest state model governs introduced scope;
- evidence must remain traceable and protected from silent regression;
- future governance specificity remains earned.

The zero-project inventory itself was only the qualification snapshot of the reset. The first Parties/test projects are an expected earned transition, not a regression of 0A.

## Future-phase relationship

Phase 2–10 are direction-only `NOT_INTRODUCED` governance. Their retired detailed plans remain history; useful anticipation belongs in `FUTURE_PHASE_CARRY_FORWARD.md` and cannot become success criteria merely because it was written earlier.

If a current Phase-0 slice genuinely needs a later responsibility, pull it forward from current facts and qualify it now. Do not create a temporary unsafe substitute or resurrect a retired future subphase file.

## Verification

0A used repository/document evidence because no executable implementation was part of its declared scope.

0B now has real executable projects, so its introduced claims require executable evidence:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

The current tests cover the Party-kind semantic and the initial Parties dependency boundary. They do not imply that runtime/provider/persistence/security behavior exists.

The root `.gitlab-ci.yml` invokes those same commands for executable/build-input changes. Pipeline `#174` was created successfully, but its job failed before start with `ci_quota_exceeded`; no runner executed the commands. That is a verification-infrastructure blocker, not a code/test failure.

Once a material claim qualifies, its regression guard remains active until the claim is retired or superseded deliberately.

## CI/CD

0B has now earned a single root `.gitlab-ci.yml` as its recurring executable verification boundary. No `eng/`, `.github/`, or other CI/tooling folder was introduced.

The job is deliberately a thin wrapper over repository-owned commands and is restricted to executable/build-input changes. Hosted GitLab quota is currently exhausted; the preferred operational path is a self-managed runner using this same job definition rather than creating a separate verification mechanism.
