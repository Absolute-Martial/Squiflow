# Phase 04-tenant-web task index

Generated from full task metadata. Read the complete file and accepted dependency handoffs before dispatch.

| Task | Status | Model | Release | Dependencies |
|---|---|---|---|---|
| [WEB-001 — Decide tenant Web rendering and session topology](WEB-001-decide-tenant-web-rendering-and-session-topology.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | none |
| [WEB-002 — Real operator login, tenant entry and application shell](WEB-002-real-operator-login-tenant-entry-and-application-shell.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | WEB-001, GATE-002 |
| [WEB-003 — Customer organization, program and individual screens](WEB-003-customer-organization-program-and-individual-screens.md) | READY_AFTER_DEPENDENCIES | GPT-6 Luna (high) | REQUIRED | WEB-002, COM-003 |
| [WEB-004 — Priced draft entry, preview, revision and abandonment](WEB-004-priced-draft-entry-preview-revision-and-abandonment.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | WEB-003, COM-008, COM-011 |
| [WEB-005 — Commitment and explainable permitted next actions](WEB-005-commitment-and-explainable-permitted-next-actions.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | WEB-004, COM-013 |
| [WEB-006 — Optional-use quotation and conditional approval continuation](WEB-006-optional-use-quotation-and-conditional-approval-continuation.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | WEB-005, COM-010, COM-012 |
| [WEB-007 — Actual fulfillment with conditional supplier-performed work](WEB-007-actual-fulfillment-with-conditional-supplier-performed-work.md) | READY_AFTER_DEPENDENCIES | GPT-6 Luna (high) | REQUIRED | WEB-005, COM-015 |
| [WEB-008 — Conditional supplier purchasing and payable screens](WEB-008-conditional-supplier-purchasing-and-payable-screens.md) | CONDITIONAL | GPT-6 Luna (high) | CONDITIONAL | WEB-005, COM-016 |
| [WEB-009 — Conditional item inventory and movement screens](WEB-009-conditional-item-inventory-and-movement-screens.md) | CONDITIONAL | GPT-6 Luna (high) | CONDITIONAL | WEB-005, COM-018 |
| [WEB-010 — Invoice, receivable and manual payment allocation screens](WEB-010-invoice-receivable-and-manual-payment-allocation-screens.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | WEB-005, COM-025 |
| [WEB-011 — Corrections, statements and retained business documents](WEB-011-corrections-statements-and-retained-business-documents.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | WEB-010, COM-031, OPS-007, OPS-009, OPS-013 |
| [WEB-012 — Tenant settings, profiles, usage and permission administration](WEB-012-tenant-settings-profiles-usage-and-permission-administration.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | WEB-002, ADM-032, ADM-024, OPS-011, OPS-012, OPS-013 |
| [WEB-013 — Conditional external customer portal decision and boundary](WEB-013-conditional-external-customer-portal-decision-and-boundary.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | WEB-001 |
| [WEB-014 — Qualify the complete tenant-operator journey](WEB-014-qualify-the-complete-tenant-operator-journey.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | WEB-002, WEB-003, WEB-004, WEB-005, WEB-006, WEB-007, WEB-010, WEB-011, WEB-012, WEB-015, WEB-016, GATE-002, OPS-017, OPS-021 |
| [WEB-015 — Catalog and pricing-policy authoring screens](WEB-015-catalog-and-pricing-policy-authoring-screens.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | WEB-002, COM-005, COM-007, COM-008 |
| [WEB-016 — Customer policy, forms and approval configuration screens](WEB-016-customer-policy-forms-and-approval-configuration-screens.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | WEB-002, COM-011, COM-012, ADM-021 |

Conditional prerequisites and approved writable areas are in the full task and root tasks.json.
