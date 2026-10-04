# ADM-025 — Implement local account availability commands

Task ID: ADM-025
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-007, ADM-006, ADM-002, GATE-001
Release requirement: REQUIRED
Cross-track prerequisites: none
Qualification consumers: WEB-002 must consume the local availability/session-invalidating semantics defined by ADM-007.

## Outcome

Implement revision-checked local account suspend/reactivate under ADM-007 policy, preserving stable account identity and preventing suspended accounts from fresh protected admission.

## Current basis and canonical inputs

AccountAvailability and current active-account checks exist, but IdentityAccess has no supported account suspension/reactivation command. Existing identity-link serialization already locks account availability and must remain consistent with the new lifecycle. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [AccountBinding.cs](../../../modules/identity-access/Application.IdentityAccess/AccountBinding.cs); [PostgresAccountOnboardingStore.cs](../../../modules/identity-access/Application.IdentityAccess.Postgres/Persistence/PostgresAccountOnboardingStore.cs); [IDENTITY_AND_SESSIONS.md](../../security/IDENTITY_AND_SESSIONS.md).

## Scope and exclusions

Allowed areas: IdentityAccess local availability core/PostgreSQL transitions and receipts, narrowly authorized AdminApi commands, runtime grant changes and focused account/admission tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Require accepted global-account actor ceiling and session effects. Reuse the existing account lock for link/lifecycle races; provider suspension, deletion and identity recovery remain separate operations. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- A tenant Owner cannot globally suspend a multi-tenant account.
- Suspension blocks fresh CoreApi admission despite a valid token.
- Reactivation restores only still-active tenant memberships.
- Concurrent link/suspend follows the accepted serialized account decision.
- Stale revision or changed replay content returns conflict without partial effects.
- State transition and actor/device receipt commit atomically.

## Security/static review

Review restricted lifecycle update grants, principal/account separation, account lookup disclosure and suspension-versus-in-flight decision points. No global provider mutation or identity merge occurs. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run real PostgreSQL account/link concurrency and least-privilege tests plus AdminApi-to-CoreApi fresh-admission suspension/reactivation journeys; verify client response loss is replayable. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-025: Implement local account availability commands.
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
