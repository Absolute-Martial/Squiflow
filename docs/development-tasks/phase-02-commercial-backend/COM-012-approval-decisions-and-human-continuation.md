# COM-012 — Approval decisions and human continuation

Task ID: COM-012
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Model: GPT-6 Luna (high)
Dependencies: COM-011, ADM-010
Release requirement: REQUIRED

The owner-accepted [COM-012/013 contract](../../review/COM_012_013_DECISION_PROPOSAL.md) and focused [Order approval and admission owner](../../implementation/ORDER_APPROVAL_AND_ADMISSION.md) define the current scope. Planning status `VERIFY_EXISTING` means verify current source and evidence. Local runtime status remains `BLOCKED` pending receiving qualification; owner acceptance is not production acceptance. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Provide an attributable approval and in-product continuation path for the selected variation, including rejection, reassignment and recovery when the eligible person cannot act.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [WORKFLOW_DESIGN.md](../../workflow/WORKFLOW_DESIGN.md), the accepted [COM-012/013 contract](../../review/COM_012_013_DECISION_PROPOSAL.md), and the focused [Order approval and admission owner](../../implementation/ORDER_APPROVAL_AND_ADMISSION.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: read-only verification of the introduced Orders approval/admission source, its protected APIs, persistence and existing evidence against the accepted contract. Report concrete gaps and exact receiving checks; any implementation changes require a separate bounded assignment. Exclude generic task management and notification-as-authority; external delivery belongs to OPS-010 if selected.

## Decisions/prerequisites

Owner decisions for the approval subject/revision, approver eligibility, self-approval, unavailable-actor recovery and no-action behavior are accepted in the [COM-012/013 contract](../../review/COM_012_013_DECISION_PROPOSAL.md). Verify the implementation against that contract and the authorization catalog/model; do not reopen or silently vary accepted choices. The bounded runtime is locally qualified under the [COM-012/013 receiving receipt](../../review/COM_012_013_IMPLEMENTATION_RECEIPT.md); repeat checks on its requalification triggers. Separate live-provider and gate-owner claims remain unchanged.

## Acceptance and edge cases

- Approvals bind the exact operation and relevant price/form/policy revisions.
- Approve/reject/reassign races use expected revision and semantic receipts.
- Suspension or revoked authority is rechecked before decision and replay.
- Every nonterminal request is discoverable by its authorized continuation actor.
- An unavailable sole approver has an explicit safe recovery path without auto-approval.
- Notification failure does not lose pending work or manufacture a completed decision.

## Security/static review

Review self-approval, reassignment escalation, confidential inbox filters and actor/evidence integrity. OpenFGA grants rights while domain facts decide whether approval is applicable.

## Dynamic verification and unavailable-environment handling

Inspect existing evidence for sole-approver outage/suspension, concurrent approve/reject, stale subject, duplicate command and restart discovery. Only actual PostgreSQL/OpenFGA/CoreApi evidence can qualify durable discovery, current authority and decisions; fakes do not. Record the receiving run of `./eng/verify.sh` and any required affected-host checks. If SDK, Docker, provider access or receiving evidence is unavailable, report the exact missing evidence and unrun commands; do not implement or claim runtime qualification under this verification assignment.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Verify existing COM-012 approval decisions and human continuation against its accepted contract and focused owner.
Read current owners and applicable instructions.
Inspect current callers, source and evidence; preserve incoming work.
Review security, authority, durability, concurrency and disclosed data against the accepted contract.
Report exact gaps and receiving qualification checks still needed.
Do not edit runtime sources or claim production acceptance in this verification assignment.
Run only the explicitly assigned checks and name every unrun check.
Deliver an exact current-head source/evidence handoff.
No commit/push or unrun qualification claims.
```
