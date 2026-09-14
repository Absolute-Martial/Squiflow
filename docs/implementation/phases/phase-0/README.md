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

0A is complete as a documentation/repository-reconciliation gate. It intentionally did not create Foundation or test projects merely to qualify itself.

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

These detailed subphases exist because the current rebuild work makes their responsibilities concrete enough to govern.

## Future-phase relationship

Phase 0 does **not** need to implement or pre-govern the future roadmap in advance.

Phase 2–10 are currently direction-only `NOT_INTRODUCED` stubs. Their former detailed subphase/evidence plans were retired because future governance must be earned from real responsibilities. Useful future questions are preserved in `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md` as non-authoritative anticipation.

If current Phase-0 work genuinely needs a future responsibility early, pull that responsibility forward, create/update the active owner from current facts, and apply the global production-honesty/evidence contracts immediately. Do not restore an old future subphase file merely because it once existed.

## Verification handoff

0A required repository/document reconciliation, not executable tests.

From 0B onward, every real implementation slice introduces the narrowest useful verification that can actually falsify its declared claims. Architecture specs are added when concrete project/dependency boundaries exist to enforce; they are not used as a reason to create Foundation prematurely.

Tests do not get to redefine a shortcut as the contract. The claim comes from requirements/owners/declared scope; verification proves or falsifies it.

Once a claim is qualified, its applicable regression check remains active or receives an explicit recurring/release/operator cadence according to the evidence/permanence owner.

## CI/CD

Repository CI/CD is allowed again. The preferred model is repository-owned verification commands used locally and by thin GitHub/GitLab workflows, primarily on self-hosted/self-managed runners under current hosted-minute constraints. CI is introduced with executable implementation rather than used to manufacture an 0A tooling project.