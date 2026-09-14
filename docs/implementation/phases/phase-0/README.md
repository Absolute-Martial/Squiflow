# Phase 0 — Architectural Development Foundation

**Status:** active after principles-first implementation reset  
**Current baseline:** v0.0.20  
**Completed subphase:** 0A  
**Next subphase:** 0B  
**High-level owner:** `docs/implementation/PHASES_AND_GATES.md`  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

Phase 0 remains a cumulative maturity foundation, but its descriptions are **minimum scope/maturity gates, not quality ceilings**.

## Current repository state

There are currently no production/test `.csproj` projects. The previous Phase-0 code was deliberately purged while architecture, decisions, requirements, reviews, phase records, and file-structure samples were retained.

0A is now complete as a documentation/repository-reconciliation gate. It intentionally did not create Foundation or test projects merely to qualify itself.

Read first:

- `0A_ARCHITECTURE_BASELINE_RECONCILIATION.md`;
- `0A_BASELINE_STATUS.md`;
- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`;
- `docs/architecture/ENGINEERING_PRINCIPLES.md`;
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`;
- `docs/architecture/REPOSITORY_STRUCTURE.md`;
- `docs/architecture/REPOSITORY_FOLDER_STRUCTURE.md`.

## Rebuild rule

Do not rebuild by reproducing the previous folder/project list. Reintroduce a project only when a real declared scope needs it and the boundary is earned.

A component introduced during any Phase-0 subphase must be **production-honest within its declared scope**, including the applicable edge/failure/recovery/security/concurrency/compatibility/resource/observability behavior needed to make that scope trustworthy.

A component that is introduced but not yet production-honest is `BLOCKED`; it is not deferred debt that a later phase may silently repair.

## KISS rule

KISS means minimum accidental complexity and the **smallest production-honest scope**. It never means:

```text
happy path only
fewest files at any cost
skip failure/recovery
collapse security/authority boundaries
fake persistence/authority
create unused abstractions/tests just to satisfy a phase gate
hide edge cases because they are inconvenient
```

YAGNI still applies: future-only projects/providers/processes/interfaces are not created until required.

## Scope states

For Phase-0 sign-off, material responsibilities are classified as:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

Only `NOT_INTRODUCED` may be honestly carried forward. `BLOCKED` must be fixed or un-introduced before the relevant gate closes.

## File-structure samples

Architecture docs contain target/sample structures for applications, services, capabilities, adapters, Foundation, infrastructure, and tests. Use them to preserve ownership and placement. They are growth maps, not instructions to scaffold empty symmetry projects.

## Phase-0 subphases

```text
0A  Architecture/repository baseline and principles-first reset             COMPLETE
0B  Application-kernel/module foundation when real consumers earn it        NEXT
0C  Host/process composition foundation when actual hosts are reintroduced
0D  Engineering safety/observability/reproducibility developed with code
0E  Active capabilities/extensibility proof through real product slices
0F  Integrated Phase-0 production-honesty gate and carry-forward ledger
```

## Verification handoff

0A required repository/document reconciliation, not executable tests.

From 0B onward, every real implementation slice introduces the narrowest useful verification that can actually falsify its declared claims. Architecture specs are added when concrete project/dependency boundaries exist to enforce; they are not used as a reason to create Foundation prematurely.

Tests do not get to redefine a shortcut as the contract. The claim comes from requirements/owners/declared scope; verification proves or falsifies it.

## CI/CD

Repository CI/CD is allowed again. The preferred model is repository-owned verification commands used locally and by thin GitHub/GitLab workflows, primarily on self-hosted/self-managed runners under current hosted-minute constraints. CI is introduced with executable implementation rather than used to manufacture an 0A tooling project.

## Phase-specific evidence and regression map

Every subphase exit inherits the evidence/permanence contract.

- **0A — baseline reconciliation:** evidence is repository/document/decision truth, not invented runtime tests. Requalification is required when source precedence, reset status, or canonical ownership changes materially. The permanent guard is review/static consistency of current repository truth; 0A may not be cited as evidence for runtime properties.
- **0B — Foundation/module boundaries:** deterministic primitive/invariant tests plus mechanical architecture/dependency tests run `PER_MR` once real assemblies exist. NetArchTest or an equivalent may implement those rules, but the property—not the library—is authoritative. Forbidden provider/host leakage and dependency direction become permanent regression checks.
- **0C — host/process composition:** configuration/startup/shutdown/cancellation/lifecycle tests run for every introduced executable. Cheap lifecycle cases run `PER_MR`; process termination/restart/version/IPC failure cases run `PER_MR` where practical and `SCHEDULED`/`PRE_RELEASE` where broader fault injection is required.
- **0D — engineering safety:** repository-owned build/test/architecture/secret checks become blocking CI/local checks. Reproducibility and failure-injection claims receive recurring evidence rather than one-time sign-off.
- **0E — real capability slices:** business invariants and scope/authority claims are protected by capability tests and architecture rules on every relevant change. A new capability may not silently bypass an already-qualified boundary.
- **0F — integration:** sign-off records named evidence and the permanent guard for every introduced Phase-0 claim; `BLOCKED = none`.

## Transitional rule

A Phase-0 component may exist before later identity/persistence/sync/etc. foundations, but it must not claim the guarantees of those future boundaries. Transitional restrictions must be explicit and mechanically enforced where risk is material.

Examples:

```text
no persistence foundation
→ do not label process-memory state durable

no production identity/authorization
→ do not expose protected production mutation as if trusted

no Sync authority
→ do not present local state as centrally accepted
```

## Carry-forward absence behavior

Every material Phase-0 deferral records what the repository/runtime does while that item is absent. “Nothing yet” is acceptable only when no reachable path relies on the missing responsibility. An accidental framework/provider default is not an accepted policy.