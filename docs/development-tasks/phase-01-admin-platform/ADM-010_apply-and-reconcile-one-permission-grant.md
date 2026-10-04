# ADM-010 — Apply and reconcile one permission grant

Task ID: ADM-010
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-009
Conditional dependencies: OPS-003 when automatic background grant reconciliation is selected
Release requirement: REQUIRED
Cross-track prerequisites: OPS-003

## Outcome

Apply one accepted tenant grant, verify its authoritative effect and atomically record local completion so UI may truthfully show Applied.

## Current basis and canonical inputs

AdminBootstrap already demonstrates duplicate-safe pinned-model writes and higher-consistency confirmation for one initial platform relation. Tenant grant administration must re-earn its own accepted scope and cannot reuse bootstrap as a universal role writer. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [OpenFgaInitialPlatformAdministratorProvisioner.cs](../../../services/admin-bootstrap/Application.AdminBootstrap/OpenFgaInitialPlatformAdministratorProvisioner.cs); [1D_AUTHORIZATION_CHANGE_RECONCILIATION_AND_FAILURE.md](../../implementation/phases/phase-1/1D_AUTHORIZATION_CHANGE_RECONCILIATION_AND_FAILURE.md); [ADMIN_SURFACES.md](../../admin/ADMIN_SURFACES.md).

## Scope and exclusions

Allowed areas: One tenant permission grant handler/provider adapter, durable status completion and authorization revision/audit paths owned by ADM-009; no generic job scheduler. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Define current authority at execution, pinned model compatibility and completion observation. Depend on OPS durable executor for automatic retry; manual retry must preserve identity and bound attempts. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Duplicate reconciliation cannot double-advance revision.
- Tuple write succeeds but local completion fails: replay converges.
- Provider timeout stays uncertain until observed.
- Wrong model cannot be accepted as success.
- Revoked delegator before execution follows the accepted decision point.
- Provider grant alone cannot bypass membership/resource tenancy.

## Security/static review

Review tuple identifier construction, write ceilings, execution credential scope and completion lock order. Exclude stored bearer secrets and arbitrary tuple operations. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Use real OpenFGA and PostgreSQL fault injection at each durable step, duplicate executors and restart/response-loss replay; run ./eng/verify.sh and OPS interruption qualification. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-010: Apply and reconcile one permission grant.
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
