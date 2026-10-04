# COM-012 — Approval decisions and human continuation

Task ID: COM-012
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-011, ADM-010
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Provide an attributable approval and in-product continuation path for the selected variation, including rejection, reassignment and recovery when the eligible person cannot act.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [WORKFLOW_DESIGN.md](../../workflow/WORKFLOW_DESIGN.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: capability-owned approval request/decision facts, bounded work inbox and recovery commands with protected API/persistence and focused tests. Exclude generic task management and notification-as-authority; external delivery belongs to OPS-010 if selected.

## Decisions/prerequisites

Define approval subject/revision, approver eligibility, self-approval policy, unavailable-actor recovery and no-action/deadline behavior for Owner plus one Staff. Cross-track authorization administration must support the required grants.

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

Test sole-approver outage/suspension, concurrent approve/reject, stale subject and duplicate command. Actual PostgreSQL proves durable discovery and decisions; schedule/notification failures are required only for introduced channels. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-012: Approval decisions and human continuation only.
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
