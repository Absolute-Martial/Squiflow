# GATE-004 — Accept Admin Web qualification

Task ID: GATE-004
Phase: 06-qualification
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: GATE-003, UIA-009
Conditional dependencies: none
Release requirement: REQUIRED

## Outcome

Accept the separate private Platform Admin Web using UIA-009 evidence and the configured registered-device identity boundary.

## Current basis and canonical inputs

Read [current truth](../../../README.IMPLEMENTATION.md), [production-honest gate](../../implementation/PHASE_GATE_PRODUCTION_HONESTY.md), [evidence permanence](../../implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md), [verification strategy](../../testing/VERIFICATION_STRATEGY.md), [shared rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Focused owners take precedence; historical tests do not qualify incoming edits.

## Scope and exclusions

Allowed areas: Admin Web acceptance matrix and qualification evidence; inspect UIA-009 handoff and named gate-owner updates. No new administration capability or duplicated browser suite. No feature implementation, unrelated refactor, Git mutation or product-version change.

## Decisions/prerequisites

Confirm tenant Web gate acceptance first, matching the selected delivery sequence. Admin UI consumes private AdminApi authority directly. Device/certificate ingress, exact provider identity and recent/step-up semantics must be proven for the selected topology rather than invented by UI controls.

## Acceptance and edge cases

- Inspect real identity plus registered-device happy path and forged/missing/revoked device denials.
- Tenant provisioning/import, memberships and profile/settings activation expose only accepted backend effects.
- No automatic tenant-data access or ordinary CoreApi proxy is implied by platform identity.
- High-risk recovery/step-up controls preserve the accepted ceremony and last-administrator/device protections.
- Admin Web independently builds/publishes while a deliberately broken unreferenced tenant host remains isolated.
- Private target browser/proxy/session evidence and owner acceptance match actual deployed ingress.

## Security/static review

Review forwarded certificate trust, device/account binding, session revocation, CSRF/CSP, cached protected pages and safe audit exports. Denied and unavailable authority remains fail-closed. Browser or telemetry artifacts cannot disclose provider tokens or private certificate material.

## Dynamic verification and unavailable-environment handling

Reuse inspected UIA-009 browser/provider/proxy evidence; rerun changed/unresolved boundary cases. Independently build/publish Admin Web and inspect normal ./eng/verify.sh after integration. A local test certificate is not qualification of production certificate issuance or recovery. Inspect actual commands, versions, exit codes, totals, failures and skips. Unavailable environments leave named evidence pending and corresponding introduced claims BLOCKED; static review cannot substitute for real runtime boundaries.

## Handoff

Return the reviewed changed-file list, safe evidence, source-only ZIP with SHA-256 and unresolved blockers. Map each material claim to its owner, permanent/recurring guard and requalification trigger. No commit/push. Requalification is required when relevant code, contract, provider/model, runtime, host topology or operating configuration changes.

## Assignable prompt

```text
Inspect UIA-009 and GATE-003 accepted handoffs.
Confirm private Admin Web topology and target configuration.
Review exact identity and registered-device browser evidence.
Inspect high-risk step-up, denial and recovery failures.
Verify profile/settings and membership outcomes use owned APIs.
Confirm independent Admin host build/publication.
Get owner acceptance of selected administrative cases.
Return gaps and permanent security/requalification guards.
```
