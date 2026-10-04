# ADM-022 — Acquire tenant runtime only for a proven variant

Task ID: ADM-022
Phase: 01-admin-platform
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: ADM-021
Conditional dependencies: OPS-003 when the selected variant is consumed by Worker
Release requirement: CONDITIONAL
Cross-track prerequisites: OPS-003

## Outcome

Prove the first trusted implementation variant for the selected tenant-specific cached Autofac architecture, then connect bounded acquisition after verified tenant context and current profile resolution.

## Current basis and canonical inputs

CoreApi already has internal ProfileRuntimeRegistry, leases, bounds and isolated tests. ProfileRuntimeKey contains tenant and implementation fingerprint/revision; no production endpoint acquires it. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [ProfileRuntimeRegistry.cs](../../../services/core-api/Application.CoreApi/Composition/ProfileRuntimeRegistry.cs); [ProfileRuntimeKey.cs](../../../services/core-api/Application.CoreApi/Composition/ProfileRuntimeKey.cs); [AUTOFAC_TENANT_PROFILE_RUNTIME_IMPLEMENTATION_PLAN.md](../../implementation/AUTOFAC_TENANT_PROFILE_RUNTIME_IMPLEMENTATION_PLAN.md).

## Scope and exclusions

Allowed areas: Existing CoreApi profile composition/resolver boundary, one admitted trusted implementation variant and its runtime tests; capability contracts stay Autofac-neutral. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Require a documented real implementation-graph variation, measured value and compatible durable profile key. If no variant is earned yet, leave acquisition absent and return the unmet personalization/runtime requirement to the owner; do not close full personalization or discard the requested cached Autofac design. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Equal fingerprints in two tenants never share a retained lifetime scope.
- Published config affecting retained state changes the runtime key.
- Concurrent cold access creates one usable runtime.
- Capacity/cancellation failure never leaks a lease or another tenant’s service.
- Activation drains old leases without half-reconfigured work.
- Restart reconstructs matching graph from authority without serialized containers.

## Security/static review

Review secret-bearing retained clients, ambient tenant candidate use, mutable request/DbContext retention and disposal logs. DI scopes do not claim memory/CPU/process isolation. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run existing ProfileRuntimeRegistryTests plus actual protected consumer integration and cold/warm capacity measurements. Add non-HTTP acquisition evidence when a real OPS consumer exists. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-022: Acquire tenant runtime only for a proven variant.
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
