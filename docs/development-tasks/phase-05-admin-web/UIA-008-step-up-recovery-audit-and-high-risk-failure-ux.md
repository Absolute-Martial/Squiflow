# Step-up, recovery, audit and high-risk failure UX

Task ID: UIA-008
Phase: 05-admin-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: UIA-002, UIA-007, ADM-034, ADM-023, OPS-013, OPS-015
Conditional dependencies: ADM-026 when application identity recovery is selected; ADM-030 when tenant support access is selected
Release requirement: REQUIRED

## Outcome

Present qualified recent-authentication, JIT/approval/recovery and authoritative audit flows for the selected risky platform operations, with honest interrupted/unknown outcomes.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/admin/ADMIN_SURFACES.md`; `docs/security/IDENTITY_AND_SESSIONS.md`; `docs/security/ENCRYPTION_POLICY_KEY_LIFECYCLE_AND_PRIVILEGED_ACCESS.md`; `docs/security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write risk confirmation/step-up/operation-status/audit and accepted recovery views plus browser tests. Infrastructure unseal/node/DB repair remains a separate private runbook; no raw root keys, shares or generic break-glass account.

## Decisions/prerequisites

Backend prerequisites: accepted operation-specific risk classes, ZITADEL acr/recent-auth mapping, qualified elevation/independent approval/recovery commands, durable audit projections and provider reconciliation. Do not invent factors or four-eyes requirements for low-risk actions. Implementation waits GATE-002; decisions may be prepared earlier. Admin host delivery also waits GATE-003.

## Acceptance and edge cases

- Show material target/diff and exact scope before high-risk execution.
- Step-up strengthens authentication without replacing current device/platform authority.
- Expired elevation, changed intent and stale approvals require backend revalidation.
- Independent approver cannot silently become the proposing actor.
- Recovery is narrow, time-bound and audited; it never reopens first-admin bootstrap.
- Unknown/partially completed provider effects have explicit safe continuation.
- Audit distinguishes authoritative records from lossy operational telemetry.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review secret exposure in forms, URLs, screenshots/logs and downloaded audit evidence. Support access requires its separately qualified tenant/resource scope and never follows from encryption administration.

## Dynamic verification and unavailable-environment handling

Run real ZITADEL step-up/TLS/provider/browser tests for expired approvals, authority loss and ambiguous high-risk effects. Keep permanent risk-flow/audit regressions; unavailable recovery environment blocks that claim. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Identify the selected high-risk operations.
Read their exact step-up, approval and recovery contracts.
Build material-diff and authentication-continuation UX.
Keep human/device/current authority checks independent.
Never display root material or recovery shares.
Show durable audit and uncertain outcomes safely.
Exercise expired elevation and interrupted provider effects.
Keep infrastructure recovery outside the application UI.
Do not invent high-risk flows without backend scope.
Return source ZIP and risk/audit evidence.
```
