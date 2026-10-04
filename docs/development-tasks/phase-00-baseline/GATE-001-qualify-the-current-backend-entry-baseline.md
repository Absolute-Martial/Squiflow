# GATE-001 — Qualify the current backend entry baseline

Task ID: GATE-001
Phase: 00-baseline
Status: VERIFY_EXISTING
Model: GPT-6.1 Sol
Dependencies: BAS-001, ADM-001, ADM-002, OPS-022
Conditional dependencies: ADM-003 when an existing public-route authentication gap is reproduced on the receiving baseline
Release requirement: REQUIRED

## Outcome

Accept a fresh trustworthy baseline after existing membership lifecycle, incoming protected request budgets and the bounded incoming host-neutral invoice behavior have current evidence for their declared scopes. Future invoice allocation, numbering and business-date decisions are not prerequisites for verifying the already present, deliberately partial library.

## Current basis and canonical inputs

Read [current truth](../../../README.IMPLEMENTATION.md), [production-honest gate](../../implementation/PHASE_GATE_PRODUCTION_HONESTY.md), [evidence permanence](../../implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md), [verification strategy](../../testing/VERIFICATION_STRATEGY.md), [shared rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Focused owners take precedence; historical tests do not qualify incoming edits.

## Scope and exclusions

Allowed areas: Named baseline qualification evidence and focused current-state corrections explicitly approved for the assigned qualification; inspect existing source without introducing features. No feature implementation, unrelated refactor, Git mutation or product-version change.

## Decisions/prerequisites

Compare the root agent statement, current README and incoming diff. Resolve claims from current focused owners and observed tests, never from earlier totals. Declare the exact baseline cases and get accountable owner acceptance; a remaining introduced blocker prevents further slice qualification.

The ADM-003 condition was activated by the reproduced bearer-bearing public-liveness authentication gap. Commit `6735370` contains the correction and [ADM-002 / ADM-003 current-head evidence](../../production-completion-review/evidence/ADM-002-ADM-003.md) records the focused post-fix behavior: explicitly public routes skip bearer validation only through trusted endpoint metadata, while protected routes retain the complete authority path. That focused evidence does not substitute for the independent post-fix ADM-003 acceptance receipt required by this gate.

This source archive also does not retain the historical gate-named handoff artifacts for ADM-001/ADM-002/OPS-022. In particular, the previously referenced `artifacts/verification/adm-002/reviewer/REVIEW.md` and `artifacts/verification/ops-022/root-final-review.json` are absent, as is the historical `artifacts/orchestration/assignments/ADM-003-public-auth.md`. Tracked summaries preserve inspected outcomes, but the gate must not silently redefine them as the missing handoffs. Before dispatching final GATE-001, recover/retain the required handoffs or make an accountable gate-owner decision that explicitly replaces those paths, and complete the bounded [ADM-003 independent review assignment](../../production-completion-review/assignments/ADM-003-INDEPENDENT-REVIEW.md) with a retained reviewer receipt. [COM-020 current-head evidence](../../production-completion-review/evidence/COM-020.md) records the deliberately partial invoice checks. After those prerequisite receipts are complete, GATE-001 still requires one complete normal repository gate on the exact integrated source.

## Acceptance and edge cases

- Membership invitation/activation/suspension/removal, protected initial Owner and tenant lifecycle satisfy their scoped real PostgreSQL/OpenFGA claims.
- Protected Admin route budgets, cancellation, capacity release, no-store errors and replay after response loss are inspected.
- Explicitly public routes remain independent of bearer authentication; the observed ADM-003 public-auth slice preserves every protected route's complete authority checks and is independently verified.
- Incoming Invoice domain/architecture guards pass, including explicit unsupported-decision outcomes, without claiming persistence, numbering or a durable invoice runtime. COM-019 and COM-020 later close and implement the remaining accepted contract deltas.
- Normal parallel full repository gate passes on the exact integrated dirty baseline.
- Failure, skip or unavailable provider evidence is recorded and not hidden with retries/serialization.
- Inventory references and source-only baseline manifest match the actual checkout.

## Security/static review

Review tenant/account/device checks, pinned authorization model, sensitive diagnostics and denied/replay paths. Preserve immutable initial-Owner restrictions and bootstrap authority.

## Dynamic verification and unavailable-environment handling

Inspect ADM-001/002 actual logs and run normal ./eng/verify.sh once with no overlapping shared-tree build. Run independent existing host build/publish checks from their focused owner where required. No remote CI/live-provider/deployment claim follows from local Testcontainers. Inspect actual commands, versions, exit codes, totals, failures and skips. Unavailable environments leave named evidence pending and corresponding introduced claims BLOCKED; static review cannot substitute for real runtime boundaries.

## Handoff

Return the reviewed changed-file list, safe evidence, source-only ZIP with SHA-256 and unresolved blockers. Map each material claim to its owner, permanent/recurring guard and requalification trigger. No commit/push. Requalification is required when relevant code, contract, provider/model, runtime, host topology or operating configuration changes.

## Assignable prompt

```text
Do not dispatch final qualification until ADM-001/ADM-002/OPS-022 handoffs are retained and ADM-003 has an independent post-fix reviewer receipt.
Inspect BAS-001 and every retained prerequisite handoff against current source.
Confirm the exact baseline claims and owner acceptance.
Review the incoming diff and canonical blocked state.
Inspect real PostgreSQL/OpenFGA and host budget evidence.
Run one fresh normal repository gate without overlapping builds.
Preserve denied/replay semantics and safe diagnostics.
Record every failure, skip and missing environment.
Return a baseline qualification decision and recurring guards.
```
