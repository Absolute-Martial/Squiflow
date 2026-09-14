# SquiFlow Detailed Implementation Phase Packages

**Status:** earned-detail implementation structure  
**High-level roadmap:** `docs/implementation/PHASES_AND_GATES.md`  
**Production-honesty owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/regression owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`  
**Future anticipation ledger:** `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md`

## Purpose

The phase hierarchy distinguishes **earned implementation detail** from **future architectural direction**.

The governance rule applies to governance documents themselves:

> A phase document may specify evidence, cadence, transitional restrictions, failure cases, and exact subphase gates only to the degree that real current responsibilities have earned that specificity.

Do not prebuild governance structure and then force future implementation to conform to it.

## Current earned-detail status

```text
Phase 0  DETAILED / ACTIVE-EARNED
Phase 1  DETAILED / EARNED TRUST-BOUNDARY CONTRACTS
Phase 2  DIRECTION ONLY — NOT_INTRODUCED
Phase 3  DIRECTION ONLY — NOT_INTRODUCED
Phase 4  DIRECTION ONLY — NOT_INTRODUCED
Phase 5  DIRECTION ONLY — NOT_INTRODUCED
Phase 6  DIRECTION ONLY — NOT_INTRODUCED
Phase 7  DIRECTION ONLY — NOT_INTRODUCED
Phase 8  DIRECTION ONLY — NOT_INTRODUCED
Phase 9  DIRECTION ONLY — NOT_INTRODUCED
Phase 10 DIRECTION ONLY — NOT_INTRODUCED
```

Phase 0 and Phase 1 retain detailed subphase documents because the current architecture/rebuild and trust-boundary decisions have earned concrete claims and constraints.

Phase 2–10 contain README direction stubs only. Their former detailed A/B/C/... decomposition has been retired from the canonical roadmap. Useful ideas from that planning are preserved in `FUTURE_PHASE_CARRY_FORWARD.md` as non-authoritative anticipation and remain available in Git history.

## Activation rule for a future phase

When real work reaches a future phase:

```text
real responsibility/workload arrives
        ↓
read global gate owners
        ↓
read current architecture/requirements
        ↓
review carry-forward anticipation
        ↓
write or rewrite the phase/subphase from current facts
        ↓
derive exact evidence and regression protection
        ↓
qualify
```

The anticipation may be useful, wrong, incomplete, or obsolete. It is input, not a contract.

A historical `2A`, `3B`, `6C`, etc. name is not reserved structure. Future work may keep, rename, merge, split, reorder or discard it.

## Breadth versus depth

The production-honesty state model remains:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

Future phase responsibilities are normally `NOT_INTRODUCED`; their absence behavior and current preservation constraints belong in the carry-forward ledger.

Once a responsibility is introduced, the global production-honesty and evidence/regression contracts apply immediately. A developer must not defer required depth merely because the parent phase README is still a direction stub.

## Pull-forward rule

If a real requirement needs a future responsibility early, do not wait for its nominal phase number and do not use a temporary unsafe substitute.

Instead:

```text
identify the real responsibility
→ promote it from NOT_INTRODUCED
→ create/update its active owner/subphase from current evidence
→ satisfy the global production-honesty contract
→ add permanent/recurring regression protection
→ continue
```

The roadmap orders likely maturity; it does not prohibit earlier earned implementation.

## Repository structure rule

A future phase folder contains only its README direction stub until real work earns more detail. Do not create empty A/B/C subphase documents, evidence templates, test matrices, or cadence tables for symmetry.

Phase-specific documents are implementation artifacts, not speculative architecture ornaments.