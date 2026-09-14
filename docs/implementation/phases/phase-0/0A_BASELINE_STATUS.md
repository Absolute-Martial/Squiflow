# Phase 0A Baseline Status

**Status:** COMPLETE / QUALIFIED under the production-honest gate model  
**Qualification type:** developer-facing repository/architecture baseline  
**Next active implementation gate:** Phase 0B, derived from real current work rather than the superseded 0B branch

## Production intent achieved

At 0A qualification, an unfamiliar developer can determine the repository's reset implementation truth, governing owner/decision precedence, safe repository placement rules, and what remains future-only without relying on deleted Phase-0 code or speculative roadmap detail.

That developer guarantee is the qualified 0A result.

## Qualified scope record

| Claim | State | Exact guarantee | Evidence | Permanent / recurring guard |
|---|---|---|---|---|
| `0A-SOURCE-PRECEDENCE` | `PRODUCTION_HONEST` | Focused canonical owner → accepted/current decision → active implementation/gate record → historical review/planning material. Open decisions are not silently closed by implementation. | Root `AGENTS.md`, `CURRENT_DECISIONS.md`, focused owner documents, `OPEN_DECISIONS.md`. | Root/scoped agent rules plus MR reconciliation whenever authority/owner locations change. |
| `0A-RESET-BOUNDARY` | `PRODUCTION_HONEST` | The v0.0.20 0A starting snapshot contains no surviving production/test project from the purged implementation, and old code is history rather than current authority. | Qualification-time repository tree contained no `*.csproj`; `SquiFlow.sln` had no project entries; reset decision/history remain in Git/docs. | Later projects must be newly earned from current responsibility; historical tree/branch/MR/diagram alone cannot authorize recreation. |
| `0A-STRUCTURE-TRUTH` | `PRODUCTION_HONEST` | Folder presence and architecture growth maps do not claim a project/runtime/responsibility exists. | `REPOSITORY_STRUCTURE.md`, `REPOSITORY_FOLDER_STRUCTURE.md`, root/scoped `AGENTS.md`. | Per-MR review/reconciliation when repository ownership or physical project boundaries change. |
| `0A-GATE-MODEL` | `PRODUCTION_HONEST` | Active responsibilities use `NOT_INTRODUCED / PRODUCTION_HONEST / BLOCKED`; a known untrustworthy introduced responsibility cannot be carried forward as future hardening. | `PHASE_GATE_PRODUCTION_HONESTY.md`. | Canonical global gate owner inherited by all active phase work. |
| `0A-EVIDENCE-PERMANENCE` | `PRODUCTION_HONEST` | Material active claims require falsifiable evidence, a permanent/recurring regression guard, and requalification triggers. | `PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`. | Canonical evidence owner inherited by active phase work; material changes invalidate stale evidence where applicable. |
| `0A-EARNED-GOVERNANCE` | `PRODUCTION_HONEST` | Future phase specificity is not prebuilt. Phase 2–10 are direction-only until real work earns active detail. | `EARNED_PHASE_GOVERNANCE_2026-09-14.md`, `PHASES_AND_GATES.md`, `FUTURE_PHASE_CARRY_FORWARD.md`, future phase README-only structure. | Any promotion of future detail must be re-derived from current facts and reviewed against the earned-governance decision. |
| `0A-BASELINE-MARKERS` | `PRODUCTION_HONEST` | The reset baseline is consistently identified as `v0.0.20` with the declared .NET 10 toolchain marker. | `VERSION = v0.0.20`, `CURRENT_VERSION.txt = v0.0.20`, `global.json` SDK `10.0.401`. | Version/toolchain changes must update their focused baseline/status references in the same reviewed change. |

## Focused-owner contradiction audit

The production-honest review did not stop at the Phase-0 status files. It checked higher-precedence owners for claims that would contradict the reset.

That audit found stale pre-reset/current-runtime wording and reconciled it before 0A qualification:

- `docs/architecture/APPLICATION_KERNEL_AND_MODULES.md` no longer claims a current Customers project or current CoreApi. Kernel/module/feature/settings/permission/composition mechanisms are explicitly activation-time responsibilities rather than a Phase-0 checklist.
- `docs/architecture/WEB_AND_SYNC_INGRESS.md` states that CoreApi/WebApi/SyncApi/Worker are accepted future ownership/topology directions and not current reset runtimes.
- `docs/server/CORE_API_AND_WORKER.md` states that CoreApi/AdminApi/Worker responsibilities become active only when a real host/workload earns them.
- `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md` treats Web/Workstation/API/Admin/Worker diagrams as accepted future responsibility/topology rather than current runtime.
- `MASTER_IMPLEMENTATION_PLAN.md` no longer declares the retired v0.0.18 fixed Phase-0-to-10 checklist as current source of truth. It is a small index/high-level direction that defers to the canonical production-honest/earned-governance owners.

Post-reconciliation searches showed no active current-owner occurrence of the purged `modules/customers/SquiFlow.Customers` path and no active owner claiming an existing/current compact CoreApi. Remaining older wording occurs only in explicitly historical decision/review records, where preserving the historical statement is intentional.

This audit is material evidence for `0A-SOURCE-PRECEDENCE`, `0A-RESET-BOUNDARY`, `0A-STRUCTURE-TRUTH`, and `0A-EARNED-GOVERNANCE`.

## Qualification-time evidence versus permanent invariants

The empty solution/project inventory proves that the principles-first reset actually produced a clean implementation starting point. It is **not** a rule that the repository must remain empty forever.

Expected transition:

```text
0A qualified reset baseline
        ↓
real current responsibility earns implementation
        ↓
0B or another pulled-forward active gate introduces the boundary
        ↓
new project is reviewed/proven under the production-honest contracts
```

Therefore adding a legitimate project later does not invalidate 0A. Recreating a project because an old tree/diagram/branch once contained it would violate the enduring 0A boundary rule.

## `NOT_INTRODUCED` at 0A close

At the 0A qualification point, the following were explicitly outside 0A's claimed implementation scope:

- ApplicationKernel/Foundation implementation;
- capability/business modules;
- Workstation, Guard, Web, API, Worker and Admin executables;
- PostgreSQL/SQLite/provider adapters;
- sync/local durability/runtime authorization/identity/key-management behavior;
- production observability runtime and deployment runtime;
- future Phase 2–10 active subphase/evidence design.

Their absence was safe because no product/runtime path claimed them and no active implementation depended on them.

This list is a **qualification snapshot**, not a permanent prohibition. Later active gates may move an item out of `NOT_INTRODUCED` when real current work earns it and applies the production-honest/evidence contracts. Such a transition does not rewrite the historical 0A close state.

## `BLOCKED`

`BLOCKED = none` at 0A qualification.

The focused-owner contradiction audit above identified and resolved the stale current-runtime claims before qualification. No known contradiction remained inside the declared 0A developer/repository guarantee.

## Requalification triggers

Requalify the affected enduring claim, not automatically the entire phase, when a material change alters:

- canonical owner/source precedence;
- repository ownership/structure semantics;
- reset/history/current-authority semantics;
- gate-state or evidence-permanence governance;
- the earned-detail boundary for future phases;
- baseline version/toolchain markers.

A later implementation MR that adds an earned project uses the later gate's evidence rather than pretending the 0A zero-project snapshot must remain true.

## Handoff to 0B

0B is derived from current real capability/application pressure under the new governance model.

Do not revive the closed historical 0B branch as active authority. Useful ideas/code from history may be reconsidered only after the current responsibility independently earns them.

0B must declare its production intent, exact introduced/non-introduced scope, evidence, permanent regression guards, and `BLOCKED = none` before qualification. It must not manufacture consumers or restore ApplicationKernel merely because an older branch already implemented it.
