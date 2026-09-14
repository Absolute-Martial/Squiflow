# Phase 0 — Architectural Development Foundation

**Status:** active after principles-first implementation reset  
**Current baseline:** v0.0.20  
**Completed/qualified subphase:** 0A under the production-honest model  
**Next active work:** 0B derived from real current responsibility on a later/stacked implementation branch  
**High-level owner:** `docs/implementation/PHASES_AND_GATES.md`  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

Phase 0 is developed as a sequence of **smallest production-honest scopes**, not as a checklist whose prewritten boxes authorize implementation.

## 0A qualification branch state

This 0A qualification branch contains no production/test `.csproj` projects and an empty `SquiFlow.sln`. The previous Phase-0 code was deliberately purged while architecture, decisions, requirements, reviews, phase records, and structure samples were retained as evidence/context.

0A's zero-project state is qualification-time reset evidence; later earned projects are expected and do not represent a regression when introduced under current owners/gates.

Read first:

- `0A_ARCHITECTURE_BASELINE_RECONCILIATION.md`;
- `0A_BASELINE_STATUS.md`;
- `0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md`;
- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`;
- `docs/implementation/PHASES_AND_GATES.md`;
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

A component may be narrow, but anything introduced on a claimed path must be production-honest for that exact claim. A known incomplete responsibility is `BLOCKED`; it cannot be labeled future hardening and carried forward.

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
0B  Application-kernel/module foundation if real work earns shared scope     NEXT — derive capability-first
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

The zero-project inventory itself is only the qualification snapshot of the reset.

## Future-phase relationship

Phase 2–10 are direction-only `NOT_INTRODUCED` governance. Their retired detailed plans remain history; useful anticipation belongs in `FUTURE_PHASE_CARRY_FORWARD.md` and cannot become success criteria merely because it was written earlier.

If a current Phase-0 slice genuinely needs a later responsibility, pull it forward from current facts and qualify it now. Do not create a temporary unsafe substitute or resurrect a retired future subphase file.

## Verification handoff

0A uses repository/document evidence because no executable implementation is part of its declared scope.

From the first real implementation boundary onward, verification must target the actual claim at the layer that owns it. Tests do not redefine shortcuts as correct. Once a material claim qualifies, its regression guard remains active until the claim is retired or superseded deliberately.

## CI/CD

CI/CD may be introduced when real executable/repository verification earns it. GitHub/GitLab should remain thin orchestration over repository-owned verification. Do not create CI/tooling merely to make a phase look implemented.
