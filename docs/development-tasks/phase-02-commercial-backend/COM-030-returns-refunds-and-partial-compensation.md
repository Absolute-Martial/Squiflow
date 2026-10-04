# COM-030 — Returns refunds and partial compensation

Task ID: COM-030
Phase: 02-commercial-backend
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: COM-029
Conditional dependencies: COM-016 when supplier returns affect payables; COM-018 when returned stock is tracked; COM-026 when refunds execute through a payment provider
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Record actual returned quantities/disposition and authorized manual refunds, linking each partial compensation to the effect it corrects instead of claiming one universal reversal.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [WORKFLOW_DESIGN.md](../../workflow/WORKFLOW_DESIGN.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: fulfillment return facts and Payment-owned refund eligibility/confirmation, explicit cross-effect links and operator continuation reads with focused tests. Supplier-return/payable consequences use enabled COM-016; inventory disposition uses COM-018; provider refund execution depends on enabled COM-026.

## Decisions/prerequisites

Decide returnable quantities, usable/damaged disposition, refund amount/rounding, approval and reconciliation evidence. A return, credit, stock restoration and refund are distinct; document which supported scenarios require each.

## Acceptance and edge cases

- Partial return cannot exceed actual handed-off quantity and retains discrepancy evidence.
- Return alone neither credits the receivable nor proves money refunded.
- Refund eligibility considers prior confirmed payment/refunds and applied credits.
- Concurrent refund requests cannot over-refund a payment.
- Manual refund confirmation is retained; provider ambiguity remains OutcomeUnknown when enabled.
- Partial failure leaves specific remaining compensation work discoverable and safe to retry.

## Security/static review

Review refund approval, recipient/payment reference privacy, returned-stock grants and effect correlation. Never compensate an external effect by changing only a local status.

## Dynamic verification and unavailable-environment handling

Qualify partial return/credit/manual refund through actual PostgreSQL and owned APIs, including retries and interruption between distinct effects. Enabled providers require exact sandbox reconciliation checks; omitted provider refunds remain explicit safe absence. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-030: Returns refunds and partial compensation only.
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
