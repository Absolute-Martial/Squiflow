# Historical commercial drafting handoff

This is the earlier drafting record. Its internal-only dependencies and initial status notes are superseded by the [integrated phase guide](README.md), [generated phase index](TASK_INDEX.md), full task metadata and [catalog baseline notes](../BASELINE_NOTES.md). Use those current files for dispatch; this draft does not qualify runtime behavior.

Planning artifact only: 32 assignments, COM-001 through COM-032. No commercial capability was implemented or qualified by writing this track. Common agent/handoff files and the integrated catalog are owned by the parent integrator.

Current source inspected includes Orders snapshots and PostgreSQL commitment, Customers individual contracts, current README and focused owners. Direct commitment and individual creation/read/availability are preservation inputs, not rebuild assignments. The shared README currently identifies AdminApi protected request-budget dynamic qualification as introduced and blocked; resolve source truth at execution rather than inheriting older membership-blocker wording.

| Task | Outcome | Status | Internal dependencies | Release |
|---|---|---|---|---|
| [COM-001](COM-001-verify-the-commercial-starting-point.md) | Verify the commercial starting point | VERIFY_EXISTING | none | REQUIRED |
| [COM-002](COM-002-customer-contacts-and-representative-relationships.md) | Customer contacts and representative relationships | READY_AFTER_DEPENDENCIES | COM-001 | REQUIRED |
| [COM-003](COM-003-duplicate-detection-and-deliberate-customer-consolidation.md) | Duplicate detection and deliberate customer consolidation | DECISION_REQUIRED | COM-002 | REQUIRED |
| [COM-004](COM-004-bounded-onboarding-customer-import.md) | Bounded onboarding customer import | CONDITIONAL | COM-003 | CONDITIONAL |
| [COM-005](COM-005-tenant-product-and-service-catalog-with-explicit-units.md) | Tenant product and service catalog with explicit units | DECISION_REQUIRED | COM-001 | REQUIRED |
| [COM-006](COM-006-resolve-price-selection-and-override-policy-contracts.md) | Resolve price selection and override policy contracts | DECISION_REQUIRED | COM-005 | REQUIRED |
| [COM-007](COM-007-published-price-policy-and-explainable-selection.md) | Published price policy and explainable selection | READY_AFTER_DEPENDENCIES | COM-006 | REQUIRED |
| [COM-008](COM-008-controlled-manual-final-price-overrides.md) | Controlled manual final-price overrides | READY_AFTER_DEPENDENCIES | COM-007 | REQUIRED |
| [COM-009](COM-009-optional-quotations-and-immutable-issued-revisions.md) | Optional quotations and immutable issued revisions | DECISION_REQUIRED | COM-007 | REQUIRED |
| [COM-010](COM-010-quotation-responses-and-idempotent-order-conversion.md) | Quotation responses and idempotent order conversion | READY_AFTER_DEPENDENCIES | COM-009 | REQUIRED |
| [COM-011](COM-011-one-bounded-typed-customer-workflow-variation.md) | One bounded typed customer workflow variation | DECISION_REQUIRED | COM-007 | REQUIRED |
| [COM-012](COM-012-approval-decisions-and-human-continuation.md) | Approval decisions and human continuation | READY_AFTER_DEPENDENCIES | COM-011 | REQUIRED |
| [COM-013](COM-013-current-policy-admission-for-committed-orders.md) | Current policy admission for committed orders | READY_AFTER_DEPENDENCIES | COM-008, COM-012 | REQUIRED |
| [COM-014](COM-014-partial-fulfillment-and-actual-work-stages.md) | Partial fulfillment and actual work stages | DECISION_REQUIRED | COM-013 | REQUIRED |
| [COM-015](COM-015-artwork-approval-and-accountable-handoff.md) | Artwork approval and accountable handoff | READY_AFTER_DEPENDENCIES | COM-014 | REQUIRED |
| [COM-016](COM-016-practical-purchasing-receipts-and-supplier-payables.md) | Practical purchasing receipts and supplier payables | CONDITIONAL | COM-005 | CONDITIONAL |
| [COM-017](COM-017-outsourced-production-and-resale-evidence.md) | Outsourced production and resale evidence | CONDITIONAL | COM-014, COM-016 | CONDITIONAL |
| [COM-018](COM-018-conditional-item-tracking-and-inventory-movements.md) | Conditional item tracking and inventory movements | CONDITIONAL | COM-005, COM-014 | CONDITIONAL |
| [COM-019](COM-019-close-the-first-invoice-contract-decisions.md) | Close the first invoice contract decisions | DECISION_REQUIRED | COM-001 | REQUIRED |
| [COM-020](COM-020-host-neutral-invoice-and-bill-to-invariants.md) | Host-neutral invoice and bill-to invariants | READY_AFTER_DEPENDENCIES | COM-019 | REQUIRED |
| [COM-021](COM-021-atomic-postgresql-invoice-issuance-and-numbering.md) | Atomic PostgreSQL invoice issuance and numbering | READY_AFTER_DEPENDENCIES | COM-020 | REQUIRED |
| [COM-022](COM-022-invoice-api-authority-and-issue-qualification.md) | Invoice API authority and issue qualification | READY_AFTER_DEPENDENCIES | COM-021 | REQUIRED |
| [COM-023](COM-023-receivable-balances-and-debtor-scoped-reads.md) | Receivable posting balances and debtor-scoped reads | DECISION_REQUIRED | COM-022 | REQUIRED |
| [COM-024](COM-024-manual-payment-recording-and-reconciliation.md) | Manual payment recording and reconciliation | DECISION_REQUIRED | COM-023 | REQUIRED |
| [COM-025](COM-025-partial-allocations-unapplied-funds-and-settlement.md) | Partial allocations unapplied funds and settlement | READY_AFTER_DEPENDENCIES | COM-024 | REQUIRED |
| [COM-026](COM-026-conditional-external-payment-integration.md) | Conditional external payment integration | CONDITIONAL | COM-025 | CONDITIONAL |
| [COM-027](COM-027-conditional-credit-terms-and-shared-exposure-admission.md) | Conditional credit terms and shared exposure admission | CONDITIONAL | COM-025, COM-013 | CONDITIONAL |
| [COM-028](COM-028-cancellation-before-committed-irreversible-effects.md) | Cancellation before committed irreversible effects | READY_AFTER_DEPENDENCIES | COM-014, COM-025 | REQUIRED |
| [COM-029](COM-029-invoice-corrections-credits-and-disputes.md) | Invoice corrections credits and disputes | DECISION_REQUIRED | COM-025, COM-028 | REQUIRED |
| [COM-030](COM-030-returns-refunds-and-partial-compensation.md) | Returns refunds and partial compensation | DECISION_REQUIRED | COM-029 | REQUIRED |
| [COM-031](COM-031-retained-commercial-history-statements-and-useful-reports.md) | Retained commercial history statements and useful reports | READY_AFTER_DEPENDENCIES | COM-030, COM-023 | REQUIRED |
| [COM-032](COM-032-qualify-supported-commercial-journeys-and-backend-handoff.md) | Qualify supported commercial journeys and backend handoff | READY_AFTER_DEPENDENCIES | COM-003, COM-010, COM-013, COM-015, COM-022, COM-025, COM-028, COM-029, COM-030, COM-031 | REQUIRED |

## Cross-track integration

The metadata dependency lists intentionally contain commercial IDs only. The integrator should bind these semantic prerequisites to exact identity/operations/gate IDs:

- COM-001: GATE-001 baseline receiving-environment qualification; current introduced blocker closure before subsequent implementation.
- COM-007/011/012/022/027: qualified current tenant identity, membership, explicit independent grants and publication/approval/billing/credit authority as applicable. No Platform Admin authority implies tenant data access.
- COM-004: OPS-008 only for retained import bytes; OPS-001/002 only if the selected bounded import requires durable background processing.
- COM-011/012: OPS-005 only for introduced deadlines; OPS-010 only for selected external notifications. In-product continuation remains mandatory without delivery.
- COM-015: OPS-007/008 for retained artwork. OPS-009 only for a selected generated-document requirement.
- COM-021/022: invoice issue explicitly does not depend on PDF rendering or notifications. OPS-002 only if an actual independent post-issue consequence is introduced; OPS-021 for earned independently consumed compatibility contracts.
- COM-026: chosen provider/test credentials plus OPS-001/002/003 for durable external payment execution/recovery; OPS-005 only for selected scheduled reconciliation.
- COM-028: qualified OPS-002/003 cancellation semantics only when an external durable dispatch exists.
- COM-031: OPS-013 for common authoritative audit reads where needed; OPS-009 for selected retained reports/documents.
- COM-032: GATE-002 owns backend frontend-readiness, after relevant identity and operational qualification and all selected conditional dependencies.

## Release disposition

Quotation capability, adaptive customer/program guidance, actual fulfillment, invoice/receivable, manual payment/allocation, correction and retained reporting are REQUIRED for this comprehensive plan. Quotation use is optional per order. COM-004 (onboarding import), COM-016 (purchasing/payables), COM-017 (outsourcing), COM-018 (tracked stock), COM-026 (external payment provider), COM-027 (credit) are CONDITIONAL by named supported workload. Each requires an explicit owner disposition, safe absence and activation trigger before omission from full completion. Selected cases become required qualification dependencies; conditional does not mean silently omitted.

## Source and decision coverage

- Customer contacts/representatives/duplicate/import: `BUSINESS_OPERATION_END_TO_END.md`, `BUSINESS_MODEL.md` sections 3–4/16, `CROSS_CUTTING_BUSINESS_PRIMITIVES.md`, current Customers owners; COM-001–004.
- Catalog and all selected selling-price sources, manual initial/later entry, override reasons/ceilings, scope, publication/source revisions: `PRICING_COMPONENT_BOUNDARY.md`, business model section 5; COM-005–008. Cost/payable is separate COM-016/017.
- Optional issued quotations and exact-version response/conversion: business model section 6 and end-to-end cases; COM-009/010.
- One actual typed rules/form/workflow variation, approval and unavailable-actor recovery, admission/current fact authority: `WORKFLOW_DESIGN.md`, `NATIVE_RULE_ENGINE.md`, `TENANT_PERMISSIONS.md`, `ORDER_COMMITMENT_SLICE.md`; COM-011–013.
- Actual partial fulfillment/artwork/handoff, suppliers, purchased/outsourced input and conditional tracking modes: business model sections 7–10, end-to-end map; COM-014–018.
- Newly present canonical `INVOICE_ISSUE_CONTRACT.md` reconciled during writing. Four blocking decisions are COM-019: allocation/cardinality; organization-scoped reference anchor/type/gaps, including individual/unattributed sources; business-date authority/bounds; inactive-individual eligibility. COM-020–022 preserve committed exact revision/NPR source and frozen prices/debtor, neutral/provider/HTTP ownership, atomic number/issue/receipt and grants. COM-023 separately earns invoice-backed receivable posting and consistent balances.
- Accepted NPR (display रु), tenant-unique invoice identity plus organization reference direction, decimal(19,4) line ToEven-four-then-sum, no initial tax, organization-default/independent-program/separate-individual per-invoice bill-to under separate billing permission are preserved. Printable/fiscal format and future program defaults stay separate/open.
- Payments, partial/unapplied allocations, reconciliation, conditional provider OutcomeUnknown and shared credit exposure: business model section 11 and end-to-end scope; COM-024–027.
- Before-effect cancellation versus post-effect credit/return/refund and linked partial compensation: workflow and cross-cutting owners; COM-028–030.
- Retained histories, debtor statements, conditional supplier payable/accountant reports, owned-API end-to-end evidence and truthful non-claims: COM-031/032. No tax/fiscal/general-ledger, universal ERP or SaaS-subscription claim is made.

## Documentation verification

The task structure, allowed metadata values, 4–8 acceptance cases, 10-line assignable prompts, 300–450-word bounds, contiguous IDs and acyclic internal dependencies were checked locally. Runtime tests were not run because this is assignment documentation only. All task links currently resolve, including parent-owned common files; incoming source/docs edits were left untouched.
