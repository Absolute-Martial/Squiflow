# Phase 02-commercial-backend task index

Generated from full task metadata. Read the complete file and accepted dependency handoffs before dispatch.

| Task | Status | Model | Release | Dependencies |
|---|---|---|---|---|
| [COM-001 — Verify the commercial starting point](COM-001-verify-the-commercial-starting-point.md) | VERIFY_EXISTING | GPT-6 Luna (high) | REQUIRED | BAS-001 |
| [COM-002 — Customer contacts and representative relationships](COM-002-customer-contacts-and-representative-relationships.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-001, GATE-001, ADM-008 |
| [COM-003 — Duplicate detection and deliberate customer consolidation](COM-003-duplicate-detection-and-deliberate-customer-consolidation.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | COM-002 |
| [COM-004 — Bounded onboarding customer import](COM-004-bounded-onboarding-customer-import.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | COM-003 |
| [COM-005 — Tenant product and service catalog with explicit units](COM-005-tenant-product-and-service-catalog-with-explicit-units.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | COM-001, GATE-001 |
| [COM-006 — Resolve price selection and override policy contracts](COM-006-resolve-price-selection-and-override-policy-contracts.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | COM-005 |
| [COM-007 — Published price policy and explainable selection](COM-007-published-price-policy-and-explainable-selection.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-006, ADM-008 |
| [COM-008 — Controlled manual final-price overrides](COM-008-controlled-manual-final-price-overrides.md) | VERIFY_EXISTING | GPT-6.1 Sol | REQUIRED | COM-007 |
| [COM-009 — Optional quotations and immutable issued revisions](COM-009-optional-quotations-and-immutable-issued-revisions.md) | VERIFY_EXISTING | GPT-6.1 Sol | REQUIRED | COM-007 |
| [COM-010 — Quotation responses and idempotent order conversion](COM-010-quotation-responses-and-idempotent-order-conversion.md) | VERIFY_EXISTING | GPT-6.1 Sol | REQUIRED | COM-009 |
| [COM-011 — One bounded typed customer workflow variation](COM-011-one-bounded-typed-customer-workflow-variation.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | COM-007, ADM-021 |
| [COM-012 — Approval decisions and human continuation](COM-012-approval-decisions-and-human-continuation.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-011, ADM-010 |
| [COM-013 — Current policy admission for committed orders](COM-013-current-policy-admission-for-committed-orders.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-008, COM-012 |
| [COM-014 — Partial fulfillment and actual work stages](COM-014-partial-fulfillment-and-actual-work-stages.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | COM-013 |
| [COM-015 — Artwork approval and accountable handoff](COM-015-artwork-approval-and-accountable-handoff.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-014 |
| [COM-016 — Practical purchasing receipts and supplier payables](COM-016-practical-purchasing-receipts-and-supplier-payables.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | COM-005 |
| [COM-017 — Outsourced production and resale evidence](COM-017-outsourced-production-and-resale-evidence.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | COM-014, COM-016 |
| [COM-018 — Conditional item tracking and inventory movements](COM-018-conditional-item-tracking-and-inventory-movements.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | COM-005, COM-014 |
| [COM-019 — Close the first invoice contract decisions](COM-019-close-the-first-invoice-contract-decisions.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | COM-001 |
| [COM-020 — Host-neutral invoice and bill-to invariants](COM-020-host-neutral-invoice-and-bill-to-invariants.md) | VERIFY_EXISTING | GPT-6.1 Sol | REQUIRED | COM-019 |
| [COM-021 — Atomic PostgreSQL invoice issuance and numbering](COM-021-atomic-postgresql-invoice-issuance-and-numbering.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-020, GATE-001 |
| [COM-022 — Invoice API authority and issue qualification](COM-022-invoice-api-authority-and-issue-qualification.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-021, ADM-010, GATE-001 |
| [COM-023 — Receivable posting balances and debtor-scoped reads](COM-023-receivable-balances-and-debtor-scoped-reads.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | COM-022 |
| [COM-024 — Manual payment recording and reconciliation](COM-024-manual-payment-recording-and-reconciliation.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | COM-023 |
| [COM-025 — Partial allocations unapplied funds and settlement](COM-025-partial-allocations-unapplied-funds-and-settlement.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-024 |
| [COM-026 — Conditional external payment integration](COM-026-conditional-external-payment-integration.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | COM-025, OPS-003 |
| [COM-027 — Conditional credit terms and shared exposure admission](COM-027-conditional-credit-terms-and-shared-exposure-admission.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | COM-025, COM-013 |
| [COM-028 — Cancellation before committed irreversible effects](COM-028-cancellation-before-committed-irreversible-effects.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-014, COM-025 |
| [COM-029 — Invoice corrections credits and disputes](COM-029-invoice-corrections-credits-and-disputes.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | COM-025, COM-028 |
| [COM-030 — Returns refunds and partial compensation](COM-030-returns-refunds-and-partial-compensation.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | COM-029 |
| [COM-031 — Retained commercial history statements and useful reports](COM-031-retained-commercial-history-statements-and-useful-reports.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-030, COM-023, OPS-013 |
| [COM-032 — Qualify supported commercial journeys and backend handoff](COM-032-qualify-supported-commercial-journeys-and-backend-handoff.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | GATE-001, COM-003, COM-010, COM-013, COM-015, COM-022, COM-025, COM-028, COM-029, COM-030, COM-031, OPS-009, OPS-013 |

Conditional prerequisites and approved writable areas are in the full task and root tasks.json.
