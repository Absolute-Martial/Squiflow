# Phase 0 — Architectural Development Foundation

**Status:** active after principles-first implementation reset

**Current product version:** v0.1.0, locked until the complete production-capable product gate

**Current qualified subphase:** 0A

**Retired historical subphase:** former 0B Parties implementation, purged 2026-09-17

**Next work:** derived from a useful real responsibility; no later Phase-0 work area is automatically active

Phase 0 is developed as a sequence of **smallest useful production-honest scopes**, not as a checklist whose labels authorize implementation.

## Current repository state

```text
production projects: 8
test projects:       7
executable hosts:    2
solution/build/test contract: present
active runtime responsibilities: public application bootstrap; JWT validation; account and tenant-membership queries; workspace/order OpenFGA checks; IdentityAccess/Tenancy/Orders PostgreSQL persistence; narrow Order draft create/read; ordered one-shot migration; Autofac root composition and isolated profile-runtime mechanics
active host-neutral responsibilities: bounded feature graph validation and deterministic effective-selection compilation
BLOCKED: none
```

Phase 0A remains the qualified reset baseline. The former 0B enum-only implementation and all scaffolding created solely for it were purged by `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md`. Its phase files remain historical evidence. ApplicationProfiles, Branding, IdentityAccess, Tenancy, CoreApi and DbMigrator were subsequently earned as independent production-honest slices; `README.IMPLEMENTATION.md` owns their current inventory.

Read first:

- `../../../decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md`;
- `0A_BASELINE_STATUS.md`;
- `../../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md`;
- `../../PHASE_GATE_PRODUCTION_HONESTY.md`;
- `../../PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`;
- `../../PHASES_AND_GATES.md`;
- the focused architecture, domain, and decision owner for the active responsibility.

## Development rule

```text
real useful responsibility / workload
        ↓
inspect applicable proven implementations
        ↓
production intent and exact declared scope
        ↓
NOT_INTRODUCED / PRODUCTION_HONEST / BLOCKED
        ↓
implementation
        ↓
falsifiable evidence at the owning layer
        ↓
permanent or recurring regression protection
        ↓
qualification with BLOCKED = none
```

A phase or subphase name is planning vocabulary. It does not authorize a project, provider, interface, process, technology, test matrix, or runtime behavior.

## Source-first rebuild rule

Do not recreate the purged folder/project list. Before custom infrastructure, use `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` to select a focused package, a bounded source adaptation when its license permits the intended use and distribution, reusable upstream tests/algorithms, or a reference-only outcome with a concrete rejection reason. License does not exclude a source from internal research.

Record the upstream revision, source inspected, license, entry mode, retained SquiFlow authority, framework assumptions, gaps, exit path, and SquiFlow-owned evidence. References route engineering work; they do not automatically authorize dependencies or transfer product authority.

## Scope states

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

Only `NOT_INTRODUCED` can be deferred. `BLOCKED` must become `PRODUCTION_HONEST` or be explicitly un-introduced before qualification.

KISS means minimum accidental complexity and the smallest useful production-honest scope. YAGNI removes speculative breadth. Neither permits missing correctness, durability, security, recovery, compatibility, resource bounds, or observability for an introduced responsibility.

## Phase-0 work areas

```text
0A  Architecture/repository baseline reconciliation                         QUALIFIED
0B  Capability-first shared-kernel/foundation discovery                     RETIRED HISTORY
0C  Host/process composition when a real executable earns the boundary      NOT ACTIVE
0D  Safety/observability/reproducibility when real code needs them           NOT ACTIVE
0E  Capability/extensibility work through real product slices               NOT ACTIVE
0F  Integrated Phase-0 qualification after enough real scope exists         NOT ACTIVE
```

These labels describe possible responsibility areas. They are not permission to prebuild their imagined contents.

## 0A permanence

- current authority remains distinguishable from history;
- folder and diagram presence are not implementation;
- project, process, and provider boundaries are earned;
- introduced claims use the production-honest state model;
- evidence remains traceable and protected from silent regression;
- future governance detail is earned.

The current zero-project inventory is again the real repository state. Future code is an earned transition only when it implements a useful declared responsibility.

## Historical 0B record

`0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md` and `0B_STATUS.md` record what the deleted slice proved at the time. They are retained to explain the decision path and prevent accidental reinterpretation. Their old executable evidence, CI result, and permanence claims are retired with the implementation.

## Verification and handoff

There is no current executable verification contract. Do not claim restore, build, test, or CI success after the purge.

The next slice starts from current product pressure and proven-source review. It introduces solution, build, tests, and CI only when real executable code earns them, and it must define the resulting evidence and requalification triggers in the same work.
