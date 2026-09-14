# Phase 0 — Architectural Development Foundation

**Status:** active after principles-first implementation reset  
**Current baseline:** v0.0.20  
**Completed subphase:** 0A  
**Next subphase:** 0B  
**High-level owner:** `docs/implementation/PHASES_AND_GATES.md`

Phase 0 remains a cumulative maturity foundation, but its descriptions are **minimum gates, not maximum implementation scope or quality limits**.

## Current repository state

There are currently no production/test `.csproj` projects. The previous Phase-0 code was deliberately purged while architecture, decisions, requirements, reviews, phase records, and file-structure samples were retained.

0A is now complete as a documentation/repository-reconciliation gate. It intentionally did not create Foundation or test projects merely to qualify itself.

Read first:

- `0A_ARCHITECTURE_BASELINE_RECONCILIATION.md`;
- `0A_BASELINE_STATUS.md`;
- `docs/architecture/ENGINEERING_PRINCIPLES.md`;
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`;
- `docs/architecture/REPOSITORY_STRUCTURE.md`;
- `docs/architecture/REPOSITORY_FOLDER_STRUCTURE.md`.

## Rebuild rule

Do not rebuild by reproducing the previous folder/project list. Reintroduce a project only when a current responsibility needs it and the boundary is earned.

A component introduced during any Phase-0 subphase must be coherent for its **current accepted responsibility**, including applicable edge cases, failure/recovery/security/concurrency/compatibility/resource/observability behavior. Do not intentionally leave disposable phase-only code for a later phase to repair.

## KISS rule

KISS means minimum accidental complexity with complete current behavior. It never means:

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

## File-structure samples

Architecture docs contain target/sample structures for applications, services, capabilities, adapters, Foundation, infrastructure, and tests. Use them to preserve ownership and placement. They are growth maps, not instructions to scaffold empty symmetry projects.

## Phase-0 subphases

```text
0A  Architecture/repository baseline and principles-first reset             COMPLETE
0B  Application-kernel/module foundation when real consumers earn it        NEXT
0C  Host/process composition foundation when actual hosts are reintroduced
0D  Engineering safety/observability/reproducibility developed with code
0E  Active capabilities/extensibility proof through real product slices
0F  Integrated Phase-0 gate and carry-forward ledger
```

## Verification handoff

0A required repository/document reconciliation, not executable tests.

From 0B onward, every real implementation slice introduces the narrowest useful verification for the boundary it creates. Architecture specs are added when concrete project/dependency boundaries exist to enforce; they are not used as a reason to create Foundation prematurely.

## CI/CD

Repository CI/CD is allowed again. The preferred model is repository-owned verification commands used locally and by thin GitHub/GitLab workflows, primarily on self-hosted/self-managed runners under current hosted-minute constraints. CI is introduced with executable implementation rather than used to manufacture an 0A tooling project.
