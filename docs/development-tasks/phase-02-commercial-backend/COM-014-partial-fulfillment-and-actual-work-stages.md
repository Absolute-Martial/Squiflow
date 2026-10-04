# COM-014 — Partial fulfillment and actual work stages

Task ID: COM-014
Phase: 02-commercial-backend
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: COM-013
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Record actual production/service progress and partial fulfilled quantities for the first supported journey while distinguishing work stage, handoff, billing and settlement.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [ORDER_COMMITMENT_SLICE.md](../../implementation/ORDER_COMMITMENT_SLICE.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: fulfillment-owned quantity/stage facts, protected transitions and bounded remaining-work reads linked to committed orders with focused tests. Reuse compatible typed guidance; exclude MRP, blanket stock reservations and automatic paid/invoiced flags.

## Decisions/prerequisites

Choose real steps, quantity/unit identity, accepted tolerance, actor, deadlines and discrepancy handling. Decide multiple handoffs and whether deposits can gate work; money remains owned by payment/credit tasks.

## Acceptance and edge cases

- Partial completion identifies exact lines/quantities and leaves explainable remaining work.
- Repeated or concurrent completions cannot overfulfill or duplicate one semantic effect.
- Tenant workflow stages do not manufacture committed fulfillment facts.
- Progress cannot edit frozen priced order content or debtor choice.
- Stopped, disputed or defective work remains actionable without pretending complete delivery.
- Orders may be invoiced or paid at explicitly selected points independently of stage.

## Security/static review

Review assigned-worker scope, quantity overflow/precision, effect ownership and personal delivery-detail exposure. Stock effects require conditional COM-018 rather than ad-hoc SQL.

## Dynamic verification and unavailable-environment handling

Test partial, zero/excess, split and concurrent quantities plus restart/replay in actual PostgreSQL. Exercise API authority denial and retained progress after workflow changes. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-014: Partial fulfillment and actual work stages only.
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
