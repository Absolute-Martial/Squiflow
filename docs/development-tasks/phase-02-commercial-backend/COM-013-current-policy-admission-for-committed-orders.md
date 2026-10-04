# COM-013 — Current policy admission for committed orders

Task ID: COM-013
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-008, COM-012
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Extend current direct commitment with the selected policy and approval prerequisites while preserving its narrow frozen-order effect and current permission checks.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [ORDER_COMMITMENT_SLICE.md](../../implementation/ORDER_COMMITMENT_SLICE.md), [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: Orders-owned admission orchestration, versioned prerequisite evidence, protected guidance and transactional receipt compatibility. Reuse implemented commitment; exclude stock reservation, invoice posting and a new generic acceptance lifecycle.

## Decisions/prerequisites

Define exact policy/fact decision points and when unissued revisions require new approval or pricing. Existing commit is not rebuilt. Credit prerequisite is conditional COM-027 only for credit-exposure journeys.

## Acceptance and edge cases

- Unconditional direct orders still commit without an optional quotation.
- Applicable required information and approval cannot be skipped through a direct command.
- Admission rejects stale subject/policy evidence with an actionable result.
- Committed content and attribution remain frozen while fulfillment and billing stay separate.
- Current authority is checked on retry; same semantic key retains the original effect.
- Concurrent revision/commit and policy publication have declared outcomes rather than mixed evidence.

## Security/static review

Review authorization-before-parsing, fail-closed fact/provider failures, price permission scope and historical receipt upgrades. No revision evidence bypasses authorization.

## Dynamic verification and unavailable-environment handling

Extend existing commit race/replay/provider tests and actual PostgreSQL invariants. Prove both default admission and the mandatory-approval variation through owned APIs without mocking the approval property. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-013: Current policy admission for committed orders only.
Read current owners and applicable instructions.
Inspect existing callers and tests; preserve incoming work.
Close listed decisions before dependent contracts.
Implement the smallest complete scope and focused tests.
Review security, authority, durability and concurrency.
Update focused behavior/decision documentation.
Run available checks; name exact unrun checks.
Deliver source-only ZIP and evidence handoff.
No commit/push or unrun qualification claims.
```
