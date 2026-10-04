# ADM-014 — Introduce narrowly scoped platform operator lifecycle

Task ID: ADM-014
Phase: 01-admin-platform
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: ADM-002, ADM-010, ADM-034
Conditional dependencies: OPS-003 when automatic operator authorization reconciliation is selected
Release requirement: REQUIRED
Cross-track prerequisites: ADM-034, OPS-003

## Outcome

Decide separate operator capabilities and recovery ceiling, then add one supported operator lifecycle operation with current principal/device authority and observed platform relation state.

## Current basis and canonical inputs

PlatformAdministration persists bootstrap principals and active availability; PostgresPlatformAdminAccessDirectory resolves them per request. No normal operator enrollment/suspension/authority lifecycle exists. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [PostgresPlatformAdminAccessDirectory.cs](../../../modules/platform-administration/Application.PlatformAdministration.Postgres/Persistence/PostgresPlatformAdminAccessDirectory.cs); [ADMIN_SURFACES.md](../../admin/ADMIN_SURFACES.md); [ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md](../../security/ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md).

## Scope and exclusions

Allowed areas: PlatformAdministration operator policy and one enrollment/suspension use case, durable receipts/reconciliation, AdminApi endpoints and operator tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Approve risk controls and exact identity proof first. Split enrollment and suspension into successive reviewable handoffs; never create a universal SuperAdmin role or reopen bootstrap. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Tenant Owner cannot become platform operator.
- Imported platform identity is exact configured issuer/subject.
- Suspended principal denies despite valid token and active device.
- Provider grant failure leaves explicit pending operator state.
- Last usable operator/device cannot be silently stranded.
- Duplicate enrollment/suspension replay retains one receipt and immutable bootstrap history.

## Security/static review

Review self-escalation, permission separation, dangerous identity replacement and audit retention. Cross-tenant support permission remains separate from operator access. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Use real PostgreSQL/OpenFGA principal lifecycle and request-time denial tests, crash/reconciliation cases and proof that CoreApi outage does not block the AdminApi operation. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-014: Introduce narrowly scoped platform operator lifecycle.
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
