# GATE-003 — Accept tenant Web qualification

Task ID: GATE-003
Phase: 06-qualification
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: GATE-002, WEB-014
Conditional dependencies: none
Release requirement: REQUIRED

## Outcome

Accept the tenant operator Web evidence already produced by WEB-014 before beginning separate Admin Web runtime implementation.

## Current basis and canonical inputs

Read [current truth](../../../README.IMPLEMENTATION.md), [production-honest gate](../../implementation/PHASE_GATE_PRODUCTION_HONESTY.md), [evidence permanence](../../implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md), [verification strategy](../../testing/VERIFICATION_STRATEGY.md), [shared rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Focused owners take precedence; historical tests do not qualify incoming edits.

## Scope and exclusions

Allowed areas: Tenant Web acceptance matrix and qualification evidence; inspect WEB-014 results and named gate-owner updates. Do not duplicate WEB-014’s feature or test implementation. No feature implementation, unrelated refactor, Git mutation or product-version change.

## Decisions/prerequisites

Tenant Web means authenticated tenant operators. WEB-013 decides external customer portal separately; inclusion earns additional authority cases. Required screens consume qualified backend contracts and remain online-only. Missing browser/provider/deployment evidence is unresolved, not a documentation pass.

## Acceptance and edge cases

- Inspect WEB-014 browser evidence for direct order and quotation, real fulfillment, the three debtor cases and manual settlement.
- Selected purchasing/stock/portal branches have accepted extra cases, or remain explicitly absent.
- Session expiry/logout/revocation, tenant switches, stale revisions and response loss display honest recoverable outcomes.
- Keyboard/focus/labels/errors and output encoding meet the declared accessible UI scope.
- Tenant host independently restores/builds/publishes; an unrelated broken Admin host cannot prevent it.
- Accountable owner accepts declared scope and every introduced material responsibility has a regression guard.

## Security/static review

Review cookies/session storage, CSRF/CSP, authorization on replay, encoded customer content and client assets/logs. Platform administration cannot appear through tenant privileges; browser screenshots must contain no secrets.

## Dynamic verification and unavailable-environment handling

Reuse inspected WEB-014 logs/screenshots, rerun only changed or unresolved cases, independently build/publish the tenant host and inspect the integrated normal repository gate. Actual provider/browser journeys remain required; do not claim formal accessibility certification or offline continuity. Inspect actual commands, versions, exit codes, totals, failures and skips. Unavailable environments leave named evidence pending and corresponding introduced claims BLOCKED; static review cannot substitute for real runtime boundaries.

## Handoff

Return the reviewed changed-file list, safe evidence, source-only ZIP with SHA-256 and unresolved blockers. Map each material claim to its owner, permanent/recurring guard and requalification trigger. No commit/push. Requalification is required when relevant code, contract, provider/model, runtime, host topology or operating configuration changes.

## Assignable prompt

```text
Inspect WEB-014 evidence rather than rebuild its features.
Confirm tenant operator scope and selected optional branches.
Review actual session and forbidden-authority browser cases.
Verify retained commercial facts after policy changes.
Inspect accessibility and safe emitted client assets.
Confirm tenant host independence and integrated gate evidence.
Get accountable owner acceptance for the declared scope.
Return accepted cases, pending evidence and requalification triggers.
```
