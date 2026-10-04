# ADM-005 — Implement selected provider onboarding operation

Task ID: ADM-005
Phase: 01-admin-platform
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: ADM-004, OPS-003, OPS-018
Release requirement: CONDITIONAL
Cross-track prerequisites: OPS-003, OPS-018

## Outcome

If selected, add exactly one provider human-create or invitation operation whose outcome can be reconciled into the existing stable local account; otherwise document import-only support as the accepted scope.

## Current basis and canonical inputs

AccountOnboardingEndpoint and PostgresAccountOnboardingStore already import/link existing verified humans atomically. Provider-side creation and reconciliation are absent; their necessity depends on ADM-004. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md](../../implementation/ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md); [AccountOnboardingEndpoint.cs](../../../services/admin-api/Application.AdminApi/AccountOnboardingEndpoint.cs); [PostgresAccountOnboardingStore.cs](../../../modules/identity-access/Application.IdentityAccess.Postgres/Persistence/PostgresAccountOnboardingStore.cs).

## Scope and exclusions

Allowed areas: One accepted ZITADEL provisioning adapter/use case, identity import reuse, AdminApi contract, durable operation state and focused provider-onboarding tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Require accepted provider topology, external/local completion condition and recovery identity. Reuse the OPS durable-operation executor if background retries are required; do not implement Worker infrastructure here. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Provider success followed by local persistence failure is recoverable.
- Timeout with unknown provider outcome never reports onboarded.
- Retry after response loss binds the same semantic operation.
- Duplicate provider identity cannot create two stable local accounts.
- Provider suspension/missing human has an explicit safe outcome.
- No invitation delivery claim exists without accepted transport/evidence.

## Security/static review

Review provider administration scope, outbound origin, response bounds, PII, invitation secrets and durable receipt redaction. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Inject provider/local failure boundaries against real PostgreSQL and approved ZITADEL sandbox; use the OPS executor interruption checks when asynchronous execution is introduced. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-005: Implement selected provider onboarding operation.
Read AGENTS.md and linked focused owners.
Use the declared model and shared rules.
Verify dependency handoffs against current evidence.
Preserve incoming files and restrict edits to the allowed areas.
Report unresolved policy; never invent defaults.
Reuse existing boundaries and framework mechanisms.
Implement or verify the exact outcome and acceptance cases above.
Perform static security review and the named real-boundary checks.
List exact unrun checks; do not claim success.
Return a source-only ZIP; no commit/push.
```
