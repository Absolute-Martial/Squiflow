# ADM-019 — Persist one typed setting revision

Task ID: ADM-019
Phase: 01-admin-platform
Status: VERIFY_EXISTING
Model: GPT-6.1 Sol
Dependencies: ADM-018
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Introduce one real nonsecret tenant setting with bounded validation, explicit override precedence, platform ceiling and revision-checked publication input.

## Current basis and canonical inputs

Current configuration is deployment-owned and FeatureCatalog compiles selections only. The continuation introduces only `RequireReferenceForProgramOrders` and its immutable published history; other settings remain absent. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [APPLICATION_KERNEL_AND_MODULES.md](../../architecture/APPLICATION_KERNEL_AND_MODULES.md); [TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md](../../architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md); [CompiledFeatureSelection.cs](../../../modules/application-profiles/Application.Profiles/CompiledFeatureSelection.cs).

## Scope and exclusions

Allowed areas: One accepted capability-owned typed setting definition, durable revision/history adapter, authorized edit/read contract and its tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Owner accepted the [COM-011/profile contract](../../review/COM_011_PROFILE_PREREQUISITE_DECISION_PROPOSAL.md) on 2026-10-08. The bounded source now has local qualification in the [implementation receipt](../../review/COM_011_IMPLEMENTATION_RECEIPT.md). The [focused consumer owner](../../implementation/ORDER_PROGRAM_REFERENCE_POLICY.md) defines exact scope, retained legacy policy, authority separation and regression obligations. Planning status `VERIFY_EXISTING` means review current source/evidence, not production acceptance.

Choose the concrete consumer and whether edits remain draft until profile publication. Do not build a universal key/value settings bag, blanket property editor or secret store. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Invalid type/range/size cannot be saved.
- Concurrent edits use expected revision and do not overwrite silently.
- Tenant overrides cannot exceed platform-owned ceilings.
- Sensitive values remain secret references outside general setting reads.
- Historical revision remains interpretable after definition evolution.
- A single operation consumes one selected setting revision consistently.

## Security/static review

Inspect value validation, tenancy, defaults, output encoding, receipt content and access separation. Generic editors may handle only safe metadata-authorized fields. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run real PostgreSQL revision/history/isolation tests and consumer tests proving one effective revision; inject unavailable-setting storage and verify declared fail-safe behavior. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-019: Persist one typed setting revision.
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
