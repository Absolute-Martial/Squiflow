# Independent Admin Web build and browser qualification

Task ID: UIA-009
Phase: 05-admin-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: UIA-002, UIA-003, UIA-004, UIA-005, UIA-006, UIA-007, UIA-008, GATE-002, GATE-003, OPS-017, OPS-021
Release requirement: REQUIRED

## Outcome

Qualify the selected separate Platform Admin Web with independent build/publication and real identity/device/browser evidence across all required control journeys.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/admin/ADMIN_SURFACES.md`; `docs/implementation/INDEPENDENT_HOST_BUILDS.md`; `docs/testing/VERIFICATION_STRATEGY.md`; `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write Admin browser/journey regressions, earned independent host build/verification integration and focused qualification evidence. Do not implement additional controls, edit tenant business authority or raise product version.

## Decisions/prerequisites

Backend prerequisites: all earlier backend gates qualified, every required UIA task completed and selected conditional controls qualified. Tenant Web completion is sequencing context, not an Admin runtime dependency. No introduced BLOCKED responsibility can qualify. Implementation waits GATE-002; decisions may be prepared earlier. Admin host delivery also waits GATE-003.

## Acceptance and edge cases

- Real identity plus registered device permits only current platform actions.
- Onboard tenant/account and manage selected lifecycle through retained receipts.
- Revoked human/device, forged headers and stale permissions deny access.
- CoreApi/tenant Web outage does not block supported Admin operations.
- Admin outage/restart does not require tenant host restart or fail open.
- Independent Admin build succeeds while an unreferenced tenant UI fails in isolation.
- Accessible controls, safe failures and durable audit remain usable after interruptions.
- Every claim has a permanent/recurring guard, trigger and exact non-claims.

## Security/static review

Review actual Admin route/session/certificate/header inventory and emitted assets/logs. Independently inspect tenant/platform separation; no customer-data browsing authority is implied by successful platform access.

## Dynamic verification and unavailable-environment handling

Run .NET 10 locked restore/build, earned Admin host verification, normal ./eng/verify.sh and actual Playwright/ZITADEL/mTLS/OpenFGA/PostgreSQL qualification. Inspect each log; no static-only gate or substituted provider qualifies deployment. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Inspect all required Admin backend/UI evidence.
Run actual admin identity/device/control journeys.
Prove denial and uncertain-response outcomes.
Stop tenant/CoreApi and recheck supported operations.
Restart Admin and inspect tenant independence.
Prove host build/publication isolation.
Review accessibility, artifacts and secret disclosure.
Map claims to recurring guards and triggers.
Record unavailable checks and remaining BLOCKED claims.
Return source ZIP and exact qualification report.
```
