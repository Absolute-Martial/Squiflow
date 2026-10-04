# ADM-007 — Decide account suspension and identity recovery lifecycle

Task ID: ADM-007
Phase: 01-admin-platform
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: ADM-004, ADM-006, WEB-001
Release requirement: REQUIRED
Cross-track prerequisites: WEB-001

## Outcome

Select supported local account suspension/reactivation and identity recovery operations, their actor scope, irreversible exclusions and exact effects across tenants/sessions.

## Current basis and canonical inputs

IdentityAccess stores account availability and unique issuer/subject bindings but exposes only onboarding/linking and reads. Membership lifecycle does not constitute account suspension, unlinking or provider user lifecycle. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [AccountBinding.cs](../../../modules/identity-access/Application.IdentityAccess/AccountBinding.cs); [AccountOnboarding.cs](../../../modules/identity-access/Application.IdentityAccess/AccountOnboarding.cs); [IDENTITY_AND_SESSIONS.md](../../security/IDENTITY_AND_SESSIONS.md).

## Scope and exclusions

Allowed areas: Focused IdentityAccess lifecycle decision, threat review and executable acceptance specification; no broad provider mutation or account-deletion implementation. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Obtain product/security decisions on local-versus-provider suspension, last usable identity, re-link ownership evidence and retention. Turn each accepted mutation into a separate bounded successor assignment. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Local account suspension blocks fresh tenant admission independently of token expiry.
- One tenant administrator cannot suspend a cross-tenant account globally.
- Removing last usable identity cannot silently lock out the account.
- Email/name similarity cannot authorize account merging or identity transfer.
- Provider outage or identity ambiguity fails closed.
- Historical receipts preserve actor identity after lifecycle changes.

## Security/static review

Review takeover, enumeration, cross-tenant privilege, ownership evidence and session-revocation dependencies; avoid arbitrary provider-delete or silent merge controls. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Specify reproducible live-provider and PostgreSQL race cases for each selected operation; decision completion requires reviewed evidence/specification, not unimplemented checks reported passed. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-007: Decide account suspension and identity recovery lifecycle.
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
