# ADM-012 — Add one tenant custom role lifecycle

Task ID: ADM-012
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-011, ADM-008
Conditional dependencies: OPS-003 when automatic background role reconciliation is selected
Release requirement: REQUIRED
Cross-track prerequisites: OPS-003

## Outcome

Introduce bounded custom role creation/revision/retirement and one accepted assignment operation using the qualified reconciliation protocol.

## Current basis and canonical inputs

Accepted architecture supports tenant-defined roles through relationships; current source has no custom-role authority or metadata. Stable capability permissions exist as host checks, not a general user-editable role catalog. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [1B_TENANT_AUTHORIZATION_ROLES_AND_DEVICES.md](../../implementation/phases/phase-1/1B_TENANT_AUTHORIZATION_ROLES_AND_DEVICES.md); [ADMIN_SURFACES.md](../../admin/ADMIN_SURFACES.md); [APPLICATION_KERNEL_AND_MODULES.md](../../architecture/APPLICATION_KERNEL_AND_MODULES.md).

## Scope and exclusions

Allowed areas: Authorization-owned custom-role definition/assignment contracts, PostgreSQL metadata, OpenFGA relationships and supported tenant endpoint adapters only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Require exact defaults/ceilings from ADM-008 and split definition versus assignment handoffs if one task would exceed a focused change. Do not deploy an authorization model per tenant role. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Role identity and assignment cannot cross tenants.
- Unknown/retired permission identifiers fail without widening authority.
- Editing a role cannot bypass the delegator ceiling.
- Concurrent role revisions conflict without partial tuple replacement.
- Retirement preserves history and defined assignment consequences.
- Dormant feature permissions stay explainable and require accepted reactivation confirmation.

## Security/static review

Review role-count/permission-count bounds, labels as plain text, relationship cycles and orphan cleanup; no arbitrary OpenFGA model/tuple editor exposed. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run real PostgreSQL/OpenFGA custom-role propagation and failure/retry tests; assert unrelated tenant/custom roles are unchanged and include hostile AdminApi/tenant endpoint checks. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-012: Add one tenant custom role lifecycle.
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
