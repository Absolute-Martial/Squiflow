# SquiFlow

**Current architecture/development baseline: `v0.0.20`**  
**Phase-0A qualification branch:** Complete / Qualified under the production-honest governance model. This branch itself remains implementation-empty; later active work may legitimately introduce the first projects when current responsibility earns them.

SquiFlow is rebuilding from accepted architecture and current responsibility rather than carrying premature implementation or speculative phase structure forward. Earlier code and retired plans remain in Git history as evidence/context, not current authority.

Reset decision: `docs/decisions/PRINCIPLES_FIRST_RESET_2026-09-14.md`.  
Gate model: `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`.  
Evidence/permanence model: `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`.

## Read in this order

1. `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md` — canonical scope/quality contract.
2. `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` — evidence permanence/requalification contract.
3. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` — qualified reset baseline and enduring 0A guarantees.
4. `docs/architecture/ENGINEERING_PRINCIPLES.md` — engineering principles.
5. `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md` — dependency/ownership rules.
6. `docs/decisions/CURRENT_DECISIONS.md` and `OPEN_DECISIONS.md` — accepted versus unresolved direction.
7. `docs/architecture/REPOSITORY_STRUCTURE.md` and `REPOSITORY_FOLDER_STRUCTURE.md` — 0A snapshot/growth placement maps.
8. The focused owner for the responsibility being changed.

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

## 0A qualification snapshot

On this branch there are **no application, service, foundation-library, capability-module, or test projects**. `SquiFlow.sln` is an empty implementation container.

That empty state proves the principles-first reset produced a clean starting point. It is **not** a permanent architecture invariant or a rule that later `v0.0.20` work must stay empty.

The enduring rules are:

- later projects/boundaries are newly earned from current responsibility;
- historical project trees/branches/diagrams do not authorize recreation;
- current runtime truth is reconciled when later implementation appears;
- future governance specificity remains earned rather than pre-written.

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

## 0A → next real implementation

0A guarantees that a developer can identify authority, the qualification-time reset truth, repository ownership, and future-only scope without relying on deleted code or speculative governance.

The next implementation does **not** automatically rebuild the historical ApplicationKernel or any other deleted project. Begin with a real capability/application responsibility. Introduce shared Foundation only when current reuse/change/ownership pressure actually justifies shared semantics.

The closed historical 0B MR remains history. Useful ideas may be reconsidered, but its old completion model and project choices are not current authority.

## Future governance

Phase 0 and the already-concrete Phase 1 trust boundary have detailed governance. Phase 2–10 remain direction-only `NOT_INTRODUCED` planning until real work earns detail. `FUTURE_PHASE_CARRY_FORWARD.md` preserves anticipation without turning it into specification.

## CI/CD direction

0A has no executable code claim, so it does not manufacture a build/test project merely to create a green pipeline. Repository CI/CD may be introduced with real executable/repository verification in later active work. GitHub/GitLab should remain thin orchestration over repository-owned commands.

## Versioning

`v0.0.20` marks the principles-first reset baseline and production-honest 0A qualification. Older document labels may remain as provenance where their decisions still stand; focused owners and current repository status govern conflicts.
