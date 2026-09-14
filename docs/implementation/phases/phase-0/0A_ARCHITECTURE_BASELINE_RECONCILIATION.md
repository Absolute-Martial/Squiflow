# Phase 0A — Architecture Baseline Reconciliation

**Status:** COMPLETE / QUALIFIED under the production-honest gate model  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Production intent

After 0A passes, an unfamiliar developer can start a real implementation slice from the repository without relying on purged implementation, speculative future governance, or ambiguous document authority. The developer can determine what is implemented at the qualification point, which owner governs a responsibility, which decisions are still open, and which repository locations are growth guidance rather than existing runtime.

That is the real developer-facing guarantee of 0A. It is not “the repository has enough documents” and it is not “the phase checklist is complete.”

## Declared scope

### WITHIN SCOPE — `PRODUCTION_HONEST`

0A qualifies the following repository/governance responsibilities:

1. **Source precedence is explicit.** Focused canonical owners and accepted decision records outrank historical review/planning material when they conflict.
2. **The principles-first reset is honest.** Purged implementation remains Git history/evidence rather than current implementation authority.
3. **Current implementation truth is distinguishable from ownership/growth structure.** A folder or architecture example does not imply a project, executable, provider, persistence path, or product responsibility exists.
4. **New physical boundaries must be earned.** Projects/processes/providers/interfaces are introduced from real current responsibility and pressure rather than from the deleted tree or diagram symmetry.
5. **Phase governance uses production-honest states.** Material active responsibilities are `NOT_INTRODUCED`, `PRODUCTION_HONEST`, or `BLOCKED`; only `NOT_INTRODUCED` may be deferred.
6. **Future governance detail is earned.** Phase 2–10 remain direction-only until real work activates them; anticipation lives in `FUTURE_PHASE_CARRY_FORWARD.md` and is not an implementation contract.
7. **The reset baseline is reproducible.** Version/toolchain markers and the qualification-time empty implementation container identify the clean starting point from which later implementation is earned.

### NOT YET IN SCOPE — `NOT_INTRODUCED`

At 0A qualification time, 0A did not introduce:

- product/runtime/foundation/capability/test implementation;
- application hosts, Workstation, Guard, Web, APIs, Worker, Admin or provider integrations;
- persistence, synchronization, identity, authorization, encryption, observability runtime or deployment runtime;
- Phase 1 runtime security behavior;
- exact Phase 2–10 subphase decomposition, evidence maps, cadences, failure inventories or exit criteria.

Behavior while these were absent was safe because no runtime/product path claimed they existed and no downstream implementation depended on them as implemented behavior.

These statements describe the **0A close/qualification snapshot**. Later gates may legitimately introduce these responsibilities without rewriting history; the enduring 0A requirement is that such introduction is newly earned and honestly recorded.

### BLOCKED

`BLOCKED = none` for the 0A scope.

Any contradiction that prevented a developer from identifying current authority/implementation truth would block 0A rather than being carried forward.

## Qualification evidence

The 0A reset snapshot was supported by repository evidence on the qualification branch:

- the repository tree contained no `*.csproj` production/test project;
- `SquiFlow.sln` contained no project entries;
- `VERSION` and `CURRENT_VERSION.txt` both identified `v0.0.20`;
- `global.json` identified the .NET 10 SDK baseline (`10.0.401` with feature-band roll-forward);
- root/scoped `AGENTS.md` files defined current ownership and implementation rules;
- `REPOSITORY_STRUCTURE.md` and `REPOSITORY_FOLDER_STRUCTURE.md` distinguished growth/ownership maps from implemented projects;
- `CURRENT_DECISIONS.md` and focused decision/architecture owners separated accepted direction from open/history material;
- `PHASE_GATE_PRODUCTION_HONESTY.md` owned scope-state/quality semantics;
- `PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` owned evidence permanence and requalification behavior;
- `EARNED_PHASE_GOVERNANCE_2026-09-14.md` and `PHASES_AND_GATES.md` demoted Phase 2–10 to direction-only governance;
- future phase folders retained direction READMEs rather than restoring old detailed A/B/C implementation plans.

No executable build/test claim was part of 0A because no executable product/test project was introduced by that gate.

## Permanent / recurring regression protection

The enduring 0A guarantees are protected by repository-owned governance rather than by a one-time checklist:

- root/scoped `AGENTS.md` enforce source precedence, current-vs-historical distinction, earned boundaries and no reconstruction-by-memory;
- the two global phase-gate owners remain canonical for every later active phase;
- architecture/file-structure owners continue to distinguish ownership/growth guidance from implementation truth;
- every material MR that changes source precedence, repository ownership, current implementation truth, phase governance, or the reset boundary must reconcile the affected owner/status in the same change;
- later implementation cannot cite an old phase file, historical MR, or diagram alone as authority for creating a project/process/provider.

The **zero-project inventory is qualification-time evidence, not a permanent repository invariant**. When 0B legitimately introduces the first implementation project, that is an expected transition rather than a regression of 0A. The permanent rule is that the new boundary must be newly earned and honestly documented.

## Requalification triggers

Re-evaluate affected 0A claims when a change materially alters any of the following:

- source-precedence rules or canonical-owner locations;
- the reset/implementation-status model;
- repository top-level ownership/structure semantics;
- the phase-gate state model or evidence/permanence contract;
- the earned-detail boundary for future phases;
- a claim that historical implementation has become current authority again.

Adding a legitimate first product project does not re-open the whole 0A gate; it activates later implementation governance and must preserve the enduring 0A guarantees above.

## Exit gate

0A passes when:

- the production intent is true and reproducible from repository-owned information;
- every within-scope responsibility above is `PRODUCTION_HONEST`;
- future/runtime responsibilities at the 0A qualification point are honestly `NOT_INTRODUCED` rather than implied by folders/roadmap prose;
- `BLOCKED = none`;
- evidence and permanent/requalification guards are recorded;
- no stale implementation or speculative future phase detail is required to begin the next real slice.

Detailed achieved status is recorded in `0A_BASELINE_STATUS.md`.
