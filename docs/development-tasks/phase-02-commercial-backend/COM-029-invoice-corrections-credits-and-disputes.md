# COM-029 — Invoice corrections credits and disputes

Task ID: COM-029
Phase: 02-commercial-backend
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: COM-025, COM-028
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Correct mistaken issued financial facts through linked attributable credit/adjustment records and explain disputes while keeping original prices and debtor snapshots immutable.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [CROSS_CUTTING_BUSINESS_PRIMITIVES.md](../../domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: invoice/receivable-owned correction eligibility, partial credit and dispute facts, protected commands and balance effects with focused tests. Exclude destructive issued edits, automatic refunds, tax credit-note compliance and general accounting claims.

## Decisions/prerequisites

Decide credit amount/quantity limits, currency/rounding, correction numbering, wrong-debtor remedy, disputed balance behavior and required approval. A correction must not silently move an issued debt to another customer.

## Acceptance and edge cases

- Original invoice remains readable with original frozen debtor and prices.
- Partial credit references exact source lines/amounts and cannot over-credit.
- Credit updates receivable balance atomically with its own retry receipt.
- Paid or allocated invoices expose unapplied/refund consequences explicitly.
- Dispute does not falsely erase debt or prove returned goods/refunded money.
- Concurrent credit/allocate/cancel resolves without negative unsupported balances.

## Security/static review

Review correction versus issue/payment permissions, reason redaction and runtime grants preserving immutable facts. State explicitly that no initial tax or fiscal compliance is introduced.

## Dynamic verification and unavailable-environment handling

Actual PostgreSQL must prove credit ceilings, allocation races and replay/rollback; API tests cover wrong-debtor correction and unavailable approval. Historical documents follow OPS-009 only when corrective bytes are required. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-029: Invoice corrections credits and disputes only.
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
