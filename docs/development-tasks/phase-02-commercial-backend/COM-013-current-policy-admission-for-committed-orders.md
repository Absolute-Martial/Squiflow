# COM-013 — Current policy admission for committed orders

Task ID: COM-013
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Model: GPT-6 Luna (high)
Dependencies: COM-008, COM-012
Release requirement: REQUIRED

The owner-accepted [COM-012/013 contract](../../review/COM_012_013_DECISION_PROPOSAL.md) and focused [Order approval and admission owner](../../implementation/ORDER_APPROVAL_AND_ADMISSION.md) define the current scope. Planning status `VERIFY_EXISTING` means verify current source and evidence. Local runtime status remains `BLOCKED` pending receiving qualification; owner acceptance is not production acceptance. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Extend current direct commitment with the selected policy and approval prerequisites while preserving its narrow frozen-order effect and current permission checks.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), [ORDER_COMMITMENT_SLICE.md](../../implementation/ORDER_COMMITMENT_SLICE.md), [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), the accepted [COM-012/013 contract](../../review/COM_012_013_DECISION_PROPOSAL.md), and the focused [Order approval and admission owner](../../implementation/ORDER_APPROVAL_AND_ADMISSION.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: read-only verification of current Orders admission, retained evidence, protected routes, transaction/receipt compatibility and supported historical profiles against the accepted contract. Report concrete gaps and exact receiving checks; any implementation changes require a separate bounded assignment. Exclude stock reservation, invoice posting and a new generic acceptance lifecycle.

## Decisions/prerequisites

Owner decisions for approval applicability, exact-subject admission, pinned policy compatibility and current commercial facts are accepted in the [COM-012/013 contract](../../review/COM_012_013_DECISION_PROPOSAL.md). Verify that existing commitment applies them atomically and preserves supported receipts/profiles; do not rebuild commitment or vary the accepted choices. Credit remains conditional COM-027 only for credit-exposure journeys. The bounded runtime is locally qualified under the [COM-012/013 receiving receipt](../../review/COM_012_013_IMPLEMENTATION_RECEIPT.md); repeat checks on its requalification triggers. Separate live-provider and gate-owner claims remain unchanged.

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

Inspect existing source and evidence for default admission, mandatory-approval admission through owned APIs, stale policy/subject, commitment races, replay and retained receipts. Only actual PostgreSQL/OpenFGA/CoreApi evidence can qualify the approval property and transaction boundary; mocks do not. Record the receiving run of `./eng/verify.sh` and affected-host build/publish checks. If SDK, Docker, provider access or receiving evidence is unavailable, report the exact missing evidence and unrun commands; do not implement or claim runtime qualification under this verification assignment.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Verify existing COM-013 current-policy admission against its accepted contract and focused owner.
Read current owners and applicable instructions.
Inspect current callers, source and evidence; preserve incoming work.
Review current authority, compatibility, transaction, concurrency and stale-evidence handling against the accepted contract.
Report exact gaps and receiving qualification checks still needed.
Do not edit runtime sources or claim production acceptance in this verification assignment.
Run only the explicitly assigned checks and name every unrun check.
Deliver an exact current-head source/evidence handoff.
No commit/push or unrun qualification claims.
```
