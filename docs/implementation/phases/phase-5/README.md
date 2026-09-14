# Phase 5 — Versioned Rules, Workflow, and Dynamic Forms

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

Phase 5 introduces bounded tenant-configurable behavior without creating arbitrary tenant code or a second application platform.

It does not require every capability to move into rules/workflow/forms. Strongly typed behavior remains preferable where tenant variation is not a real requirement. Earlier capabilities continue to evolve normally while selected variability gains versioned/configurable machinery.

## Subphases

```text
5A  Rule definitions, fact authority and deterministic evaluation
5B  Versioned workflow and continuation
5C  Bounded dynamic forms and publication
5D  Workstation snapshots and cross-version execution
5E  Integrated Phase-5 production-honesty gate
```

## Continuing development

Capabilities may continue ordinary strongly typed business development while selected variability moves into reviewed versioned definitions. Not every feature needs the rule/workflow engine, and new capabilities can be added during this phase without adopting dynamic forms.

If a real earlier capability needs versioned configurable behavior before Phase 5, pull the relevant foundation forward rather than introducing unversioned ad-hoc configuration.

## Phase-specific evidence and regression map

- **5A — rules/fact authority:** deterministic/property tests run `PER_MR` against accepted rule semantics, invalid/conflicting definitions, fact-authority classification and decision traces. A test of the interpreter implementation alone is not evidence of business-rule correctness.
- **5B — workflow:** state/transition/concurrency/version/continuation/recovery tests run `PER_MR`; active old instances remain explainable under new publications and permission revocation behavior is explicit.
- **5C — forms/publication:** schema/validation/publication/rollback tests permanently verify failed publication leaves the previous accepted version usable, fields remain bounded/typed, and no arbitrary code path appears. Security checks for rendered/input surfaces apply as soon as those surfaces exist.
- **5D — Workstation snapshots/cross-version:** compatibility fixtures and stale-fact authority tests run `PER_MR`; old compatible snapshots remain executable/explainable and incompatible snapshots fail explicitly.
- **5E — integration:** no configurable behavior is qualified without claim-to-definition-version/fact-authority/evidence traceability.

## Transitional contract

Draft/edit capability may exist before publication/execution is qualified, but draft definitions must not become active runtime authority. If publication/version/recovery semantics are not yet production-honest, the authoring UI must keep the definition non-active.

A Workstation may receive only snapshot versions whose compatibility and fact-authority semantics are qualified. Missing compatibility does not fall back to interpreting the newest definition optimistically.

## Hard invariants versus operational targets

Determinism for the same accepted inputs/revision, protected fact authority, publication atomicity, and historical explainability are hard invariants. Rule/workflow execution latency may receive measured SLOs only after representative workloads exist.