# ADM-018 — Admit a real feature catalog and permission metadata

Task ID: ADM-018
Phase: 01-admin-platform
Status: VERIFY_EXISTING
Model: GPT-6 Luna (high)
Dependencies: ADM-008, GATE-001
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Select a smallest useful shipped feature vocabulary and stable permission metadata that Admin/tenant editors can consume without creating a general module kernel.

## Current basis and canonical inputs

FeatureCatalog already validates bounded definitions, dependency cycles and deterministic fingerprints. The admitted first-consumer catalog fixes four always-enabled nonselectable feature IDs; current permission checks belong to real capability operations. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [FeatureCatalog.cs](../../../modules/application-profiles/Application.Profiles/FeatureCatalog.cs); [APPLICATION_KERNEL_AND_MODULES.md](../../architecture/APPLICATION_KERNEL_AND_MODULES.md); [FEATURE_RELEASE_AND_EXPERIMENTS.md](../../architecture/FEATURE_RELEASE_AND_EXPERIMENTS.md).

## Scope and exclusions

Allowed areas: Existing Application.Profiles definitions/tests, capability-owned descriptors for already implemented operations and reviewed catalog read contract only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Owner accepted the [COM-011/profile contract](../../review/COM_011_PROFILE_PREREQUISITE_DECISION_PROPOSAL.md) on 2026-10-08. The bounded source now has local qualification in the [implementation receipt](../../review/COM_011_IMPLEMENTATION_RECEIPT.md). The [focused consumer owner](../../implementation/ORDER_PROGRAM_REFERENCE_POLICY.md) defines exact scope, retained legacy policy, authority separation and regression obligations. Planning status `VERIFY_EXISTING` means review current source/evidence, not production acceptance.

Approve capability identifiers, dependencies, platform ceiling, dormant-grant behavior and one real configuration use case. Release channels/experiments remain separate unless selected here. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Catalog identifiers are stable across reordered definitions.
- Missing dependency/cycle is rejected by the existing compiler.
- Feature enablement never grants permission.
- Unknown permission strings cannot become grants.
- Nonselectable/always-enabled behavior obeys accepted platform policy.
- Disabling a feature preserves historical data and explains dormant grants.

## Security/static review

Review descriptor data boundaries, metadata encoding and selection size/depth. No assembly upload, arbitrary CLR type name or executable-name permission filtering. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Reuse FeatureCatalogTests and add tests only for admitted metadata/ceiling behavior; compare fingerprints with accepted contract and test the bounded authorized catalog endpoint if exposed. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-018: Admit a real feature catalog and permission metadata.
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
