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

## Promotion rule

When future work becomes real, write/rewrite the detailed phase/subphase from current facts. The carry-forward anticipation is an input, not a success criterion.

A new detailed file is created only when a real responsibility can name:

- production intent;
- exact scope/non-scope;
- falsifiable claim;
- evidence at the correct layer;
- permanent/recurring regression protection;
- transition/safe-absence behavior where actually relevant.

## No loss of useful thinking

The retired future-phase documents remain in Git history. Their useful architectural questions have been consolidated into `FUTURE_PHASE_CARRY_FORWARD.md` so the reasoning is available without being mistaken for a current contract.

## Relationship to roadmap

`PHASES_AND_GATES.md` remains a high-level maturity roadmap but is now direction-only after the current earned-detail boundary.

Phase numbering remains planning guidance, not a requirement that responsibilities arrive in exactly that order. Pull-forward remains allowed when a real workload needs a future responsibility earlier.