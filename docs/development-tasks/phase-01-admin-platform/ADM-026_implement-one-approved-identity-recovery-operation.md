# ADM-026 — Implement one approved identity recovery operation

Task ID: ADM-026
Phase: 01-admin-platform
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: ADM-007, ADM-025, ADM-004, ADM-034, OPS-018
Release requirement: CONDITIONAL
Cross-track prerequisites: ADM-034, OPS-018

## Outcome

If the accepted ceremony requires it, implement one proof-backed identity recovery binding operation without account merging, provider account deletion or silent transfer of another account’s identity.

## Current basis and canonical inputs

Existing verified identity link accepts an active local account and preserves global issuer/subject uniqueness. It does not establish takeover-resistant recovery, removal of the last usable identity or transfer of an existing binding. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [IDENTITY_AND_SESSIONS.md](../../security/IDENTITY_AND_SESSIONS.md); [AccountOnboarding.cs](../../../modules/identity-access/Application.IdentityAccess/AccountOnboarding.cs); [PostgresAccountOnboardingStore.cs](../../../modules/identity-access/Application.IdentityAccess.Postgres/Persistence/PostgresAccountOnboardingStore.cs).

## Scope and exclusions

Allowed areas: One explicitly approved IdentityAccess recovery operation/proof contract, PostgreSQL binding/receipt effects, protected recovery endpoint and adversarial recovery tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Require approved recovery evidence, risk/approval/expiry policy, last-identity protection and session effects from ADM-007. When no safe ceremony is accepted, retain the capability gap and return it to the owner rather than inventing recovery defaults. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Email or display-name equality alone cannot prove ownership.
- Replacement provider human identity is verified for exact issuer/subject.
- Binding already owned by another account cannot be reassigned silently.
- Expired or changed recovery proposal invalidates prior approval.
- Concurrent recovery/link/suspend preserves one account and unique binding.
- Replay after response loss retains one recovery effect and auditable actor chain.

## Security/static review

Review identity takeover, approver independence, proof replay, recovery token redaction and account existence leakage. Last-identity removal remains excluded unless separately accepted and qualified. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Use approved ZITADEL sandbox identity evidence and real PostgreSQL concurrency/crash tests; run hostile AdminApi recovery requests with missing/expired factors and session-effect verification. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-026: Implement one approved identity recovery operation.
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
