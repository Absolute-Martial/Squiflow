# COM-016 — Practical purchasing receipts and supplier payables

Task ID: COM-016
Phase: 02-commercial-backend
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: COM-005
Release requirement: CONDITIONAL

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Record actual requested/received supplier goods or services, cost and an optional outstanding payable with partial payment when the supported workload needs purchasing.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: purchasing-owned supplier identity/contact, informal request, partial receipt, actual cost/payable and supplier payment facts with focused tests. Include useful order links; exclude mandatory formal PO approval, general ledger, tax and generic procurement infrastructure.

## Decisions/prerequisites

Activation needs a real purchase case, cost rounding/currency rules, receipt tolerances, supplier payment/allocation, erroneous receipt/return/cost adjustment meaning and permissions. Inventory effects require COM-018 only for tracked items; informal/phone ordering remains valid.

Omission from full completion requires an explicit owner disposition, safe absence behavior and an activation trigger.

## Acceptance and edge cases

- Receipt records only supplied items/services and exact received quantity.
- Partial receipt shows remaining request without pretending complete delivery.
- Supplier cost stays distinct from independently authorized customer resale price.
- Payable, partial supplier payment and explicit receipt/cost corrections retain attributable amounts and balance.
- Retry and concurrent receipt/payment cannot duplicate or over-allocate effects.
- A customer payment does not automatically settle the supplier payable.

## Security/static review

Review supplier identity spoofing, purchase/payment privilege separation, parameterized SQL, tenant isolation and immutable posted-cost evidence.

## Dynamic verification and unavailable-environment handling

Use actual PostgreSQL receipt/payable/payment transaction and race tests plus protected API cases. Include supplier balance/readback and a phone-order path without forcing a formal PO. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-016: Practical purchasing receipts and supplier payables only.
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
