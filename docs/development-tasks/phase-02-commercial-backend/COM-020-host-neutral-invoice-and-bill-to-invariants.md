# COM-020 — Host-neutral invoice and bill-to invariants

Task ID: COM-020
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Model: GPT-6.1 Sol
Dependencies: COM-019
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Review and dynamically qualify the incoming host-neutral Invoices implementation, then adapt only the deltas required by COM-019's accepted decisions. Preserve its existing authority, replay and retained-fact behavior.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [INVOICE_ISSUE_CONTRACT.md](../../implementation/INVOICE_ISSUE_CONTRACT.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: modules/invoices/Application.Invoices, tests/unit/Application.Invoices.Tests and narrowly affected architecture guards. The incoming project already exists: inspect it before editing. Consume narrow public committed-Order and Customers debtor facts; never reselect prices or read provider tables. No provider SDK, HTTP, SQL, numbering transaction, file rendering or payment allocation belongs here. Shared solution/inventory corrections require integrator approval.

## Decisions/prerequisites

COM-019 closes exact first contract; current Customers and Orders supply narrow identities/frozen source facts. Separate billing permission belongs at authoritative application admission and cannot be inferred from neutral intent construction.

After acceptance, add only the needed business-date/fingerprint, numbering-scope, allocation-conflict and inactive-individual eligibility changes. The current commit outcome enum has no accepted allocation-conflict result; do not convert a business conflict into DependencyUnavailable or HistoricalReceiptInvalid. Derivation policy remains capability-owned even when an adapter supplies the authoritative clock/generated facts.

## Acceptance and edge cases

- NPR is retained as the currency code while display notation stays presentation-only.
- Line rounding uses four-decimal ToEven before summation with decimal(19,4) bounds.
- Selected organization/program/individual debtor and applied prices become immutable issued facts.
- Enforce the accepted full-invoice or partial allocation model at the exact committed source revision.
- Later customer defaults, prices or labels cannot recompute historical issue truth.
- Invalid version, unit, missing identity and overflow return explicit bounded results.

## Security/static review

Review minimum public surface, provider-free dependency direction and confused-deputy debtor selection. Avoid generic money/invoice abstractions unsupported by a real second consumer.

## Dynamic verification and unavailable-environment handling

Add pure midpoint, overflow, repeated-line and freeze tests that fail on invariant violations. These qualify neutral calculations only; explicitly leave PostgreSQL issuance and API authority unqualified until COM-021/022. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Verify COM-020's incoming host-neutral invoice implementation; do not recreate it.
Read current owners and applicable instructions.
Inspect existing callers and tests; preserve incoming work.
Close listed decisions before dependent contracts.
Run existing guards and add only meaningful tests or accepted-contract deltas.
Review security, authority, durability and concurrency.
Update focused behavior/decision documentation.
Run available checks; name exact unrun checks.
Deliver source-only ZIP and evidence handoff.
No commit/push or unrun qualification claims.
```
