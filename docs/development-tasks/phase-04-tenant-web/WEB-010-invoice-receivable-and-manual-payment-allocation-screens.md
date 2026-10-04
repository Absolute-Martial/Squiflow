# Invoice, receivable and manual payment allocation screens

Task ID: WEB-010
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: WEB-005, COM-025
Conditional dependencies: COM-026 when provider payment controls are selected; COM-027 when credit/exposure controls are selected
Release requirement: REQUIRED

## Outcome

Provide the required supported billing-to-settlement operator views over qualified invoices, receivables, manual payment records and exact allocations.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/BUSINESS_OPERATION_END_TO_END.md`; `docs/domain/BUSINESS_MODEL.md`; `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write apps/web invoice/receivable/manual-payment/allocation presentation and focused tests. Exclude tax/fiscal-compliance claims, payment-provider integration, ledger arithmetic and any UI-created debtor or credit policy.

## Decisions/prerequisites

Backend prerequisites: accepted numbering/business dates/currency, authorized debtor selection and retained issue-time identity, invoice/receivable issuance, manual payments, allocation/unapplied amount and correction contracts. Required organization/program/individual debtor cases must qualify first. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Organization default, independently owing program and assigned individual remain distinct.
- Operator identity is never silently used as debtor; issue-time bill-to is retained.
- Invoice issue confirms immutable prices/debtor and actual consequences.
- Partial payment, unapplied money, allocation and remaining receivable are distinct.
- Allocations target exact authorized receivables and use server amounts/versions.
- Lost responses, duplicate keys and concurrent allocations resolve without false settlement.
- No settled label appears until the authoritative supported financial outcome exists.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review amount/currency precision, sensitive debtor/contact projections and operation confirmation. Display server balances only; hidden fields or stale credit snapshots cannot authorize exposure or allocation.

## Dynamic verification and unavailable-environment handling

Run actual PostgreSQL-backed billing/payment APIs through Playwright for three debtor cases, partial payment, concurrent allocation and response loss. Keep permanent settlement regressions; mocked amounts do not qualify financial integrity. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Read the accepted financial and debtor contracts.
Build only required issue/read/payment/allocation screens.
Display retained invoice facts and live receivable state.
Keep manual payment and allocation separate operations.
Use server values and explicit revision/retry identities.
Require current authorization at every submit.
Explain partial, unapplied, uncertain and settled outcomes.
Test each debtor case and allocation race.
Do not invent tax or payment-provider authority.
Return source ZIP and financial evidence mapping.
```
