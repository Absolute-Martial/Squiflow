# ADM-011 — Reconcile revocation and guarded Owner handoff

Task ID: ADM-011
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-010, ADM-034
Conditional dependencies: OPS-003 when automatic background revocation reconciliation is selected
Release requirement: REQUIRED
Cross-track prerequisites: ADM-034, OPS-003

## Outcome

Remove one accepted grant with observed provider outcome and durable revision/audit. Establish an Owner transfer primitive only under ADM-008 policy before loosening bootstrap protection.

## Current basis and canonical inputs

Role revocation and TenantAuthorizationRevision are absent; the initial Owner cannot currently be suspended/removed. A qualified grant flow is the reusable prerequisite, not proof that revocation or Owner transfer is safe. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [IDENTITY_AND_SESSIONS.md](../../security/IDENTITY_AND_SESSIONS.md); [ADMIN_API_MEMBERSHIP_LIFECYCLE.md](../../implementation/ADMIN_API_MEMBERSHIP_LIFECYCLE.md); [1D_AUTHORIZATION_CHANGE_RECONCILIATION_AND_FAILURE.md](../../implementation/phases/phase-1/1D_AUTHORIZATION_CHANGE_RECONCILIATION_AND_FAILURE.md).

## Scope and exclusions

Allowed areas: One grant-revoke flow and revision enforcement in its owning authorization capability, plus explicitly accepted Owner handoff guard; impacted tenant admission tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

ADM-034 supplies a backend-verified freshness/guarded-transport contract without waiting for a Web runtime. Deliver revocation and Owner handoff as separate bounded slices when their scope differs; never weaken initial-Owner protection provisionally. The later WEB-012 screen consumes the same guarded primitive. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Unknown delete outcome stays pending/uncertain.
- Replay removes one semantic relation and advances revision once.
- Revoke immediately before fresh admission denies stale permission.
- Already committed operations retain historical truth.
- Last recoverable Owner cannot be removed by a racing transfer.
- Membership suspension independently denies even if tuples remain.

## Security/static review

Review revocation decision points, long-running checkpoint policy, authorization-cache freshness and Owner recovery ceiling. Success cannot precede known OpenFGA state. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Inject stale-consistency observation and provider/local failures with real systems; synchronize admission/revocation races deterministically. Include protected Owner transfer evidence if implemented. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-011: Reconcile revocation and guarded Owner handoff.
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
