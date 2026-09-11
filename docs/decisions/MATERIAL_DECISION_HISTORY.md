# Material Decision History and Supersession

**Version:** v0.0.17

**Status:** Accepted convention for preserving the history of material SquiFlow decisions.  
**Authority boundary:** Current authoritative semantics remain in focused owner documents and `docs/decisions/CURRENT_DECISIONS.md`; unresolved choices remain in `docs/decisions/OPEN_DECISIONS.md`. This document owns how material decision rationale is preserved when accepted direction changes. It does not replace owner documents, the decision audit, Git history, or ordinary review records.

## 1. Why this document exists

SquiFlow intentionally changes decisions when stronger evidence shows that an earlier choice is incomplete, too broad, too narrow, or wrong.

Examples already exist in the repository: a boundary can move from rejected/deferred to accepted when its real responsibility becomes clearer, or an accepted mechanism can later be replaced when a provider, workload, product promise, or invariant changes.

Git history shows that text changed. It does not by itself explain which prior decision governed the project, why that decision was reasonable at the time, what evidence changed, or which later decision superseded it.

For material decisions, preserve this chain explicitly:

```text
Decision A accepted for context/evidence at time A
→ new evidence / changed constraint / discovered failure
→ Decision B accepted
→ Decision B supersedes Decision A
→ current owner reflects Decision B
→ Decision A remains readable as historical rationale
```

Do not rewrite history to make the current decision look inevitable.

## 2. What counts as a material decision

Use this convention when losing the historical rationale would make future product/domain/architecture work materially harder, especially for decisions that affect one or more of:

- product promise or customer scope;
- domain identity, lifecycle, authority, or historical semantics;
- tenant/security/authorization/isolation boundaries;
- local/server authority and sync behavior;
- financial/stock/credit correctness;
- quality attributes that materially drive architecture/cost;
- runtime/process/deployment boundaries;
- externally visible APIs/protocols/contracts;
- provider choices with migration/operational consequences;
- persistent data shape or irreversible migration;
- long-lived configuration/workflow/rule compatibility;
- production recovery/operability obligations.

Do **not** create a material decision record for every naming choice, local refactor, implementation detail, dependency version, trivial UI adjustment, or easily reversible choice that has no lasting rationale value.

## 3. Minimal material decision record

A material record may live in the decision audit, a focused review/decision record, or another durable repository artifact. It does not require a separate ADR file when the existing focused record already preserves the needed history.

Preserve the smallest useful subset of:

```text
Decision identity / short name
Status: Proposed / Accepted / Superseded / Rejected / Deferred
Context / problem
Decision
Why now
Evidence / assumptions
Alternatives considered
Consequences / trade-offs
Affected owners
Verification / proof obligation
Revisit trigger
Supersedes
Superseded by
Date / repository reference where useful
```

The purpose is traceability, not template completion.

## 4. Supersession rule

When an accepted material decision changes materially:

1. update the current authoritative owner to the newly accepted semantics;
2. create or update a durable rationale record for the new decision;
3. mark the prior material decision **Superseded** rather than silently editing it into the new rationale;
4. link forward from the old record to the new one where practical;
5. link backward from the new record to what it supersedes;
6. propagate the accepted change to materially dependent owner documents under the cross-owner consistency rule;
7. update implementation/verification obligations when the change affects them.

A superseded record is historical evidence, not current authority.

## 5. Current owner versus historical rationale

Use this hierarchy:

```text
focused owner / CURRENT_DECISIONS
= what SquiFlow currently accepts

OPEN_DECISIONS
= unresolved choice that still needs closure

material decision history / audit / decision record
= why a consequential choice was made or changed

Git history
= exact textual/code changes
```

If these appear inconsistent, the focused current owner governs current semantics, and the inconsistency must be corrected rather than treating the historical record as an alternate authority.

## 6. Decision changes must name what changed

A later decision should distinguish the reason for change where useful:

- new customer/product evidence;
- discovered domain invariant or semantic distinction;
- failed implementation/POC/measurement;
- changed operational/provider constraint;
- changed security/privacy/legal obligation;
- changed product promise;
- correction of an earlier mistaken assumption;
- deliberate scope reduction or expansion.

This helps future reviewers decide whether the old decision could become relevant again under a different context.

## 7. Rejected and deferred are not erased

A rejected or deferred mechanism can remain useful historical information when it records why SquiFlow did not adopt it.

If later evidence reopens it, create a new decision that explains what materially changed. Do not simply delete the old rejection and replace it with acceptance.

For example:

```text
DEFERRED because no real boundary/workload existed
→ later real boundary/workload appears
→ ACCEPTED under new evidence
```

is different from pretending the mechanism was always required.

## 8. Relationship to existing decision artifacts

- `docs/decisions/CURRENT_DECISIONS.md` remains the accepted-direction catalogue.
- `docs/decisions/OPEN_DECISIONS.md` remains the unresolved-decision catalogue.
- `docs/review/DECISION_AUDIT.md` remains an important reasoning/correction record and may contain material decision history.
- focused source-review records may preserve evidence that led to a decision but do not override the current owner.
- `docs/product/PRODUCT_FOUNDATION.md` owns evidence and product-decision discipline upstream of material product decisions.

Existing historical corrections in `DECISION_AUDIT.md` do not need to be rewritten retroactively into one-record-per-decision ADRs. Apply this convention prospectively and when a future material change would otherwise make the rationale ambiguous.

## 9. Restraint

This convention does **not** require:

- an ADR for every change;
- a decision ID bureaucracy for trivial choices;
- architecture review boards;
- immutable files that can never receive status/link corrections;
- copying the same current rule into history files and owner documents;
- retaining obsolete rationale as current behavior.

The target is simple:

> Current semantics stay easy to find, while consequential changes remain explainable.