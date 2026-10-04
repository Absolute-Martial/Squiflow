# Conditional supplier purchasing and payable screens

Task ID: WEB-008
Phase: 04-tenant-web
Status: CONDITIONAL
Model: GPT-6 Luna (high)
Dependencies: WEB-005, COM-016
Release requirement: CONDITIONAL

## Outcome

Expose qualified supplier purchase/service receipt and payable views for the supported commercial case, allowing informal/phone ordering where accepted.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/BUSINESS_OPERATION_END_TO_END.md`; `docs/domain/BUSINESS_MODEL.md`; `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write supplier/purchase/receipt/payable presentation and browser checks under apps/web. Do not introduce supplier rules, mandatory procurement workflow, stock tracking, payment integration or backend migrations.

## Decisions/prerequisites

Backend prerequisites: selected purchasing case and qualified supplier identity, requested/received item or service, actual cost, partial receipt/payment, payable and optional order-link contracts. Financial authority/projections must be accepted first. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Informal ordering does not require a fictional issued purchase order.
- Purchased services can be represented without inventing stock quantities.
- Partial receipt, actual cost and outstanding payable are distinguished.
- Order linkage does not make customer payment settle the supplier.
- Cost fields and supplier contact records respect independent permissions.
- Stale revisions, duplicate submission and uncertain payment state are explicit.
- Cross-tenant supplier/order links cannot be selected or disclosed.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review monetary display/rounding contracts, supplier PII and excessive-field submissions. UI cannot mark payable paid or change actual cost through a hidden unrestricted field.

## Dynamic verification and unavailable-environment handling

Run actual API/Playwright case checks for partial receipt, stale cost revision, duplicate operation and independent payable authority. Keep a permanent selected purchasing journey and document unqualified provider behavior. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Confirm purchasing is required by the selected case.
Read supplier, receipt and payable contracts.
Build bounded lists/details and receipt entry.
Keep purchased goods/services and stock behavior distinct.
Render actual cost and partial balances accurately.
Submit only authorized explicit operations.
Support accepted informal ordering.
Test tenant links, stale versions and retry semantics.
Do not add unearned supplier/payment machinery.
Return source ZIP and inspected case evidence.
```
