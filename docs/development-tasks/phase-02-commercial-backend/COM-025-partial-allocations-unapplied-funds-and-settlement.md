# COM-025 — Partial allocations unapplied funds and settlement

Task ID: COM-025
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-024
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Allocate reconciled manual payment amounts to exact receivables, retain unapplied money and explain remaining balances without over-allocation under concurrent actors.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: payment/receivable-owned allocation and explicit deallocation/correction facts, balance reads and settlement result with focused tests. Reuse frozen debtor and currency identity; exclude automatic cross-debtor transfers and mixed-currency accounting.

## Decisions/prerequisites

Decide permitted payer/debtor relationships, explicit cross-invoice allocation authority, rounding and reversal after reconciliation. Cross-debtor use requires an accepted rule; do not invent organization-wide pooling for independent program/individual debts.

## Acceptance and edge cases

- One payment can partially cover one or several explicitly eligible invoices.
- Unapplied remainder is visible and is neither missing money nor settled debt.
- Concurrent allocations cannot exceed confirmed payment or receivable remaining amount.
- Allocation and updated balance/receipt commit atomically with consistent reads.
- Retry preserves the same allocation; changed amounts/targets conflict.
- Reversal/deallocation is attributable and does not erase original payment evidence.

## Security/static review

Review allocation grants, debtor/currency confusion, lock ordering and negative amount boundaries. Every financial correction remains linked to its actual prior effect.

## Dynamic verification and unavailable-environment handling

Actual PostgreSQL tests prove competing allocations, partial funds, restart/replay and correction races. Demonstrate NPR organization/program/individual cases through owned APIs; preserve pending/unknown outcomes as ineligible for confirmed allocation. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-025: Partial allocations unapplied funds and settlement only.
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
