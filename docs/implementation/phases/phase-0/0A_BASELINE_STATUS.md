# Phase 0A — Principles-First Reset Baseline Status

**Status:** reset baseline established; implementation rebuild not yet qualified  
**Date:** 2026-09-14  
**Baseline:** v0.0.20  
**Working branch:** `rewrite/principles-first-reset`

This ledger records current repository truth after the deliberate implementation purge. Earlier Phase-0 implementation remains in Git history; it is no longer current implementation authority.

## 1. Authority order

```text
1. focused canonical owner
2. CURRENT_DECISIONS / accepted focused decision record
3. implementation phase package
4. master implementation-plan synthesis
5. historical review/source-study/chat/old branch/MR material
```

Repository state proves implementation; architecture docs may define future ownership without claiming a project exists.

## 2. Why the reset happened

The previous code introduced useful ideas but also allowed phase-driven/demo-shaped implementation and shared abstractions that could encode deployment topology prematurely. The rebuild starts from explicit boundaries and development principles instead of preserving code merely because it already exists.

Chesterton's Fence is satisfied by retaining Git history, architecture/decision documents, reviews, and phase records before removing implementation.

Detailed decision: `docs/decisions/PRINCIPLES_FIRST_RESET_2026-09-14.md`.

## 3. Current implementation inventory

Current `*.csproj` count: **0**.

Current production/test project tree: **none**.

Retained root/tooling:

```text
docs/
deploy/README.md
global.json
Directory.Build.props
Directory.Packages.props   # central management enabled, no unused versions
SquiFlow.sln               # empty solution container
VERSION                     # v0.0.20
CURRENT_VERSION.txt         # v0.0.20
```

No application/service/foundation/capability/test project is considered current until it is intentionally reintroduced.

## 4. Architecture and file-structure knowledge retained

The reset does not discard the accepted architecture. In particular, preserve and use:

- `docs/architecture/REPOSITORY_STRUCTURE.md` — target/current ownership map and sample structures;
- `docs/architecture/ENGINEERING_PRINCIPLES.md` — development rules and complete KISS;
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md` — ownership/dependency/SOLID boundary rules;
- capability/host/persistence/sync/security/workstation/server focused owners;
- `docs/decisions/CURRENT_DECISIONS.md`;
- requirements, review evidence, and phase packages.

Sample file trees are guidance for placement and future decomposition. They are **not mandatory scaffolding**.

## 5. Development rules for the rebuild

A phase is a minimum maturity/verification floor, not an implementation ceiling.

KISS means the simplest complete design, including the material edge/failure/recovery/security/concurrency/compatibility/resource/observability cases of the responsibility being introduced.

YAGNI forbids speculative projects/providers/processes/interfaces. It does not permit omission of required behavior for a responsibility that exists now.

SOLID is applied as change-safety and boundary guidance, not interface/class count.

New code must make ownership, authority/state, allowed dependencies, forbidden crossings, failure semantics, compatibility, security, resource bounds, diagnostics, and verification explicit at the appropriate level.

## 6. Git/MR lineage

- MR !54 is the merged documentation-first rewrite on `main` and remains historical baseline context.
- MR !55 (`phase0/0a-baseline-reconciliation`) captured useful v0.0.19 architecture corrections but is **superseded by the v0.0.20 principles-first reset before merge**.
- `rewrite/principles-first-reset` starts from !55's useful architecture corrections, then removes the implementation/test projects and reconciles the docs to the zero-code rebuild baseline.
- The replacement reset MR is the only review path that should be merged for this change set.

A branch/MR is never current `main` implementation merely because it exists.

## 7. CI/CD decision

The earlier no-CI directive is superseded. Repository CI/CD is allowed and expected to be thin orchestration over repository-owned local verification. GitHub/GitLab may both point to the same Git history/workflow, while self-hosted/self-managed runners are preferred to reduce hosted build-minute consumption.

No hosted pipeline has been triggered by this reset.

## 8. What is not implemented

Everything application-facing is currently NOT INTRODUCED, including Workstation, Guard, Web, CoreApi, ApplicationKernel, Observability library, Customers, tests, PostgreSQL/SQLite integration, identity/authorization/key-management providers, sync, Worker, Admin, object storage, backup, and production deployment.

Accepted technology/architecture directions remain documented; implementation will be re-earned slice by slice.

## 9. Verification state

The reset itself is verified structurally by repository inventory and commit history. There is currently no production/test project to claim as compiled or passing.

When the first project is reintroduced, restore/build/tests/architecture checks become part of that change rather than being postponed to a later phase.

## 10. Next implementation condition

Do not begin by recreating the old tree. The first implementation slice must:

```text
accepted requirement/decision
→ explicit responsibility and boundary
→ material edge/failure/security/compatibility analysis
→ simplest complete design
→ only earned project/folder/interface/process boundaries
→ verification alongside implementation
```

This is the baseline from which Phase 0 development restarts.
