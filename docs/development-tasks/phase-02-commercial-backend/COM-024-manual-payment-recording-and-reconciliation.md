# COM-024 — Manual payment recording and reconciliation

Task ID: COM-024
Phase: 02-commercial-backend
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: COM-023
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Record actual manual cash/bank payment evidence and reconcile its outcome as durable facts, preserving uncertain or disputed state without pretending an order is simply paid.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: payment-owned manual record/reconciliation commands, currency/amount/evidence, receipt reads and persistence with focused tests. No provider adapter or receivable allocation yet; exclude cashbook/general-ledger and SaaS subscription billing.

## Decisions/prerequisites

Decide manual channels, exact received/confirmed evidence, correction versus reversal rules, monetary precision, reconciliation authority and reference uniqueness. Partial payment is required; advances may remain unapplied under COM-025.

## Acceptance and edge cases

- A manual entry identifies actual payer/evidence without deriving debtor from operator login.
- Received, pending confirmation, rejected and disputed/uncertain outcomes stay explicit where applicable.
- Partial received amount does not mark every linked invoice settled.
- Same-key retries preserve one payment; duplicate external references follow the selected policy.
- Reconciliation cannot overwrite historical received evidence silently.
- Cross-currency input is rejected in the first NPR-only supported settlement case.

## Security/static review

Review payment-versus-reconciliation privileges, sensitive bank/reference fields, correction evidence and decimal bounds. A UI checkbox cannot prove confirmed money.

## Dynamic verification and unavailable-environment handling

Test partial, mistaken, disputed and reconciled manual records, duplicate races and revoked replay through API and actual PostgreSQL. Report exact unanswered arithmetic decisions before persistence contracts depend on them. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-024: Manual payment recording and reconciliation only.
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
