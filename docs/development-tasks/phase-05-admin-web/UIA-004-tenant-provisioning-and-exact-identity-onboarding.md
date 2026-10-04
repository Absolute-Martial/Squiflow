# Tenant provisioning and exact identity onboarding

Task ID: UIA-004
Phase: 05-admin-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: UIA-003, ADM-006
Conditional dependencies: ADM-005 when provider-side onboarding is selected; ADM-026 when application identity recovery is selected
Release requirement: REQUIRED

## Outcome

Expose qualified tenant provisioning and stable-account import/link operations with reviewable intent, retained operation outcomes and exact external identity semantics.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md`; `docs/admin/ADMIN_SURFACES.md`; `docs/security/IDENTITY_AND_SESSIONS.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write bounded Admin tenant/account/import/link forms and operation-result views plus browser checks. Exclude provider-side user creation/reconciliation, bulk imports or email-based account matching unless separately qualified.

## Decisions/prerequisites

Backend prerequisites: qualified tenant/account/identity commands, safe provider validation, caller-scoped idempotency and receipts; broader provider creation is a separate accepted capability before UI exposure. Implementation waits GATE-002; decisions may be prepared earlier. Admin host delivery also waits GATE-003.

## Acceptance and edge cases

- Review exact issuer/subject and stable local account target before link submission.
- Email/display name are descriptive, never identity keys or automatic linking evidence.
- Provision tenant and onboard/link account remain distinct explicit commands.
- Response loss/retry reuses intent and shows authoritative retained results.
- Changed intent under the same key produces understandable conflict.
- Provider outage/inactive account/device revocation never appears as successful onboarding.
- Platform actor/device receipts remain discoverable without exposing tokens or raw provider responses.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review identity enumeration, target/account substitution, sensitive profile fields and confirmation text. No provider credentials or direct ZITADEL management calls originate in the browser.

## Dynamic verification and unavailable-environment handling

Run actual AdminApi/PostgreSQL/OpenFGA/ZITADEL browser cases for exact existing identities, duplicate links and lost responses. Keep identity-linking and operation-result regressions independent from fake provider fixtures. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Read the exact provisioning/import/link contracts.
Implement small reviewable forms and result views.
Use issuer/subject and stable account identity.
Never auto-link by email or display name.
Preserve semantic keys through uncertain responses.
Render safe provider conflicts and unavailable states.
Exercise wrong targets, duplicates and revoked device.
Inspect durable receipts from actual APIs.
Keep provider-side creation absent unless qualified.
Return source ZIP and onboarding evidence.
```
