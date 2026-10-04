# ADM-001 — Qualify existing membership and tenant lifecycle

Task ID: ADM-001
Phase: 01-admin-platform
Status: VERIFY_EXISTING
Model: GPT-6.1 Sol
Dependencies: BAS-001
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Produce current real PostgreSQL/OpenFGA evidence for the existing narrow slice; correct only defects revealed by that qualification before permitting role-administration work.

## Current basis and canonical inputs

MembershipLifecycleEndpoint, TenantLifecycleEndpoint and Tenancy already implement revision checks, caller receipts, initial-Owner protection and suspension effects. User instructions retain a membership qualification block; the older 640-test record does not qualify this dirty checkout. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [ADMIN_API_MEMBERSHIP_LIFECYCLE.md](../../implementation/ADMIN_API_MEMBERSHIP_LIFECYCLE.md); [TenantMembershipLifecycle.cs](../../../modules/tenancy/Application.Tenancy/TenantMembershipLifecycle.cs); [MembershipLifecycleBoundaryTests.cs](../../../tests/integration/Application.AdminApi.Tests/MembershipLifecycleBoundaryTests.cs).

## Scope and exclusions

Allowed areas: Existing Tenancy core/PostgreSQL lifecycle paths, their tests, AdminApi lifecycle adapters, runtime grant artifact and focused membership owner only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Record exact Git revision and incoming dirty files first. Resolve conflicting historical/current gate wording through evidence in the authorized implementation handoff, without treating this assignment as gate clearance. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Concurrent initial-Owner requests preserve one designation.
- Same caller/key replays once; changed intent conflicts.
- Stale revisions cannot transition membership or tenant.
- Suspend blocks current tenant access; reactivate preserves individual membership states.
- Ordinary runtime credentials cannot rewrite identities, bootstrap markers or receipts.
- Provider/database outage and malformed JSON fail safely without a success receipt.

## Security/static review

Inspect transaction boundaries, lock order, least-privilege grants and every identity/tenant predicate. Retain actor/device audit without tokens or customer content. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run real TenantMembershipLifecyclePostgresTests and MembershipLifecycleBoundaryTests, then the normal parallel ./eng/verify.sh. Serialized success or fake provider checks cannot close this task. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-001: Qualify existing membership and tenant lifecycle.
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
