# Qualify the complete tenant-operator journey

Task ID: WEB-014
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: WEB-002, WEB-003, WEB-004, WEB-005, WEB-006, WEB-007, WEB-010, WEB-011, WEB-012, WEB-015, WEB-016, GATE-002, OPS-017, OPS-021
Conditional dependencies: WEB-008 when purchasing is selected; WEB-009 when tracked stock is selected; WEB-013 when an external customer portal is selected
Release requirement: REQUIRED

## Outcome

Qualify the selected tenant Web as an usable online operator application, with honest failures and recurring browser evidence across the complete supported commercial journey.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/BUSINESS_OPERATION_END_TO_END.md`; `docs/testing/VERIFICATION_STRATEGY.md`; `docs/implementation/INDEPENDENT_HOST_BUILDS.md`; `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write tenant Web journey/browser regressions, earned independent host build/verification integration and focused evidence owners. No new features, unrelated refactors, product-version change or Workstation/sync/Guard implementation.

## Decisions/prerequisites

Backend prerequisites: GATE-001 and GATE-002 qualified, required quotation/actual fulfillment and selected conditional cases accepted, deployment identity/proxy readiness and all required screens including catalog/policy authoring. Every conditional task has an explicit release disposition, including WEB-013. Any introduced BLOCKED responsibility prevents qualification.

## Acceptance and edge cases

- Complete customer→price→draft→commit→supported work→invoice→allocation→settlement.
- Cover organization, independent program and assigned individual debtor cases.
- Exercise denied authority, stale versions, response loss and repeated commands.
- Expiry/logout/revocation/network/circuit restart show truthful pending or recoverable state.
- Keyboard, focus, labels, validation announcements and contrast cover accessibility basics.
- Tenant Web builds/publishes independently while unreferenced Admin Web fails in isolation.
- Every claim maps to an owned permanent/recurring guard and requalification trigger.

## Security/static review

Review actual routes, cookies, CSP/CSRF, encoded output, storage, tenant switches and generated assets/logs. Record non-claims including formal accessibility certification, offline operation and transparent failover.

## Dynamic verification and unavailable-environment handling

Run .NET 10 locked restore/build, earned tenant host checks, normal ./eng/verify.sh and actual Playwright/provider journeys. Capture exact results; unavailable browser/provider/deployment evidence leaves the corresponding claim BLOCKED. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Confirm selected required and conditional scope.
Inspect backend gates before browser qualification.
Run the real tenant commercial journey end to end.
Inject authority, concurrency and uncertain-response failures.
Check keyboard/accessibility and session lifecycle basics.
Prove independent host build/publication boundaries.
Review emitted client assets and logs for leakage.
Map claims to lasting regressions and triggers.
Do not convert static review into runtime evidence.
Return source ZIP and an exact qualification report.
```
