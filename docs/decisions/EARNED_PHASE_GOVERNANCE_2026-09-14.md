# Decision — Future Phase Governance Is Earned, Not Pre-Specified

**Date:** 2026-09-14  
**Status:** Accepted implementation-governance correction

## Decision

Apply the production-honesty rule to governance documents themselves.

A future phase may state direction, likely responsibility class, known dependencies, current preservation constraints and activation trigger. It must not pre-specify exact subphase decomposition, evidence types, regression cadences, transitional contracts, hostile/failure scenarios or exit criteria before real work earns that knowledge.

## Why

The earlier Phase 0–10 package correctly introduced production-honesty and permanent regression protection, but it then pre-wrote detailed evidence maps for responsibilities that do not yet exist.

That inverted the intended dependency:

```text
wrong:
phase label
→ pre-written governance
→ later responsibility forced to fit it

correct:
real responsibility
→ actual claim
→ evidence
→ permanence guard
```

A governance document that claims knowledge we do not yet have is itself not production-honest.

## Pressures that produced the superseded model

The over-specification did not arise because rigor was ignored. It arose from pressures that can easily look like rigor:

- a complete Phase 0–10 roadmap feels more credible than an intentionally partial one;
- symmetric phase folders look orderly and can be mistaken for architectural discipline;
- detailed future subphases create a feeling of preparedness and completeness;
- writing likely tests/failure cases early feels safer than admitting that the real evidence cannot yet be known;
- once one detailed phase exists, structural imitation creates pressure for later phases to look equally detailed;
- review pressure can reward visible completeness even when the detail is speculative.

These pressures are specifically dangerous because each individual addition may appear reasonable. The failure emerges cumulatively when anticipation hardens into a canonical shape that later implementation is expected to obey.

Therefore reviewers should treat these signals as prompts for skepticism rather than as proof of maturity:

```text
all future phases have matching A/B/C/... files
all phase folders have similar depth
all future phases already have evidence/cadence tables
future provider/process failure matrices exist before the provider/process exists
roadmap completeness is cited as the reason for adding detail
```

The correct response is not to make the future document more complete. It is to ask what current responsibility makes the additional specificity knowable.

## Resulting structure

```text
global production-honesty owner              canonical
global evidence/regression/transition owner canonical

Phase 0 detailed package                     retained
Phase 1 detailed package                     retained

Phase 2–10 README direction stubs            retained
Phase 2–10 detailed A/B/C/... files          retired

future detailed thinking
→ FUTURE_PHASE_CARRY_FORWARD.md
→ explicitly non-authoritative anticipation
```

The current earned-detail boundary is therefore after Phase 1. This boundary can move forward as real implementation earns more specificity.

## Promotion rule

When future work becomes real, write/rewrite the detailed phase/subphase from current facts. The carry-forward anticipation is an input, not a success criterion.

A new detailed file is created only when a real responsibility can name:

- production intent;
- exact scope/non-scope;
- falsifiable claim;
- evidence at the correct layer;
- permanent/recurring regression protection;
- transition/safe-absence behavior where actually relevant.

The implementation is free to use, modify, merge, split, rename, reorder or reject the former anticipated subphase decomposition.

## Carry-forward is anticipation, not specification

The future ledger may preserve likely questions and current preservation constraints because those can influence present architecture honestly.

It does **not** preserve as current requirements:

- old evidence matrices;
- exact test lists;
- exact cadence assignments;
- exact provider/runtime choices;
- exact subphase names/numbers;
- exact gate completion conditions.

Those belong to Git history until real work makes them current again.

The carry-forward entry is successful if it prevents current work from accidentally closing off a likely future need without pretending the future design is already known.

## Activation-time anti-drift review

Immediately before promoting a future ledger item into active governance, review the ledger entry itself before using it as design input.

Ask:

1. Has this entry accumulated detail that could not have been known without the real implementation/workload now in front of us?
2. Does it imply a subphase decomposition, provider/runtime choice, evidence class, cadence, failure matrix or exit gate that was added before activation?
3. Is any item being retained only because it has existed in the roadmap for a long time?
4. Would a developer reasonably read any anticipation as mandatory rather than as a question/preservation constraint?
5. If the ledger were hidden, would current requirements independently lead us to the same responsibility/shape?

Premature detail is removed or demoted back to a question before the active phase/subphase is written. Activation is a fresh derivation from current facts, not a conversion of the ledger into specification.

## No loss of useful thinking

The retired future-phase documents remain in Git history. Their useful architectural questions have been consolidated into `FUTURE_PHASE_CARRY_FORWARD.md` so the reasoning is available without being mistaken for a current contract.

## Relationship to roadmap

`PHASES_AND_GATES.md` remains a high-level maturity roadmap but is direction-only after the current earned-detail boundary.

Phase numbering remains planning guidance, not a requirement that responsibilities arrive in exactly that order. Pull-forward remains allowed when a real workload needs a future responsibility earlier.

## Governance review question

Before adding future phase detail, ask:

> What real current responsibility makes this level of specificity knowable now?

If the answer is only `because the roadmap says this phase will exist`, keep the detail in carry-forward anticipation rather than canonical phase governance.