# COM-023 — Receivable posting balances and debtor-scoped reads

Task ID: COM-023
Phase: 02-commercial-backend
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: COM-022
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Introduce durable invoice-backed receivables and trustworthy outstanding balances by frozen debtor identity and invoice, with bounded consistent reads suitable for payment selection and operator explanation.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: receivable-owned posting integrated atomically with new invoice issue, unique invoice linkage, consistent projections and bounded protected reads with focused tests. Include organization/program/individual context without collapsing independently owing debtors; exclude general ledger and automatic collections.

## Decisions/prerequisites

Decide receivable recognition, historical issued-invoice backfill/reconciliation and balance derivation. Prior issue qualification did not introduce accounting; extend its owner deliberately and prove exactly one receivable per invoice. Business date/aging semantics follow COM-019.

## Acceptance and edge cases

- Posting/retry creates one invoice-backed receivable; balance explains each applied effect separately.
- An organization reference does not combine independent program/individual debt automatically.
- Concurrent allocation/correction reads cannot report a mixed balance snapshot.
- Bounded stable pagination handles equal timestamps and changed balances.
- Inactive customer records remain interpretable as historical debtors.
- Authorization and tenant filters apply to detail, lists and counts, including empty results.

## Security/static review

Review financial-data enumeration, cursor validation and query-only behavior. Reads must not reconcile or mutate payments implicitly.

## Dynamic verification and unavailable-environment handling

Test unpaid/partial/settled/credited balance as later effects integrate; actual PostgreSQL proves snapshot consistency and isolation. Use bounded API query tests; do not present a cached balance as credit-admission authority. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-023: Receivable posting balances and debtor-scoped reads only.
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
