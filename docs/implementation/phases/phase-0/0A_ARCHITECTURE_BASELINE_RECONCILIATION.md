# Phase 0A — Architecture Baseline Reconciliation

## Purpose

0A reconciles the repository/document baseline before rebuilding implementation. It ensures development starts from accepted current decisions rather than a stale project tree or old implementation artifact.

## Core rule

The architecture source-of-truth order is:

```text
focused canonical owner
    ↓
current decision summary
    ↓
active implementation/gate records
    ↓
historical review/planning material
```

When lower-level material conflicts with a newer focused owner, the focused owner governs until the summary/roadmap is reconciled.

## Principles-first reset

Current implementation was deliberately purged while retaining architecture, decisions, requirements, reviews, phase records and structure samples.

The reset means:

- previous implementation is historical evidence rather than code to recreate mechanically;
- future projects/processes/providers are reintroduced only when real responsibilities earn them;
- file/folder samples preserve ownership direction rather than mandate empty scaffolding;
- verification is added with real claims rather than manufactured merely to satisfy a phase checklist.

## Production-honesty interpretation

The phase/gate model uses:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

A narrow current scope is acceptable. An introduced responsibility that is known to be untrustworthy for its claim is not valid carry-forward work.

## Governance self-honesty

The same rule applies to roadmap documents.

Do not treat a future phase name as enough evidence to specify its detailed implementation, evidence map, cadence, transition contract or exact subphase split.

Current earned-detail boundary:

```text
Phase 0  detailed
Phase 1  detailed trust-boundary governance
Phase 2–10 direction-only until real work activates them
```

Future planning ideas belong in `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md` as non-authoritative anticipation.

## Reconciliation responsibilities

Before implementation continues, verify that active owners agree on the responsibilities the current slice can actually encounter, including as applicable:

- capability/business ownership;
- host/process boundaries;
- persistence/authority distinctions;
- security/tenant/authorization ownership;
- Workstation local-first semantics;
- API/sync/Worker/Admin directions;
- provider boundaries;
- recovery/observability/deployment constraints;
- currently open decisions.

Do not resolve unrelated future OPEN_DECISION items solely to make the architecture look complete.

## Exit gate

0A is complete when:

- the current repository is intentionally implementation-empty after the principles-first reset;
- active architecture/decision owners are identified and current enough to guide 0B;
- file/folder samples are explicitly non-scaffolding growth maps;
- stale implementation artifacts are not mistaken for current authority;
- the phase model uses production-honesty vocabulary;
- future Phase 2–10 detail is not treated as already earned;
- 0B can start from real capability/application work rather than from reconstructing deleted projects.

Detailed achieved status is recorded in `0A_BASELINE_STATUS.md`.