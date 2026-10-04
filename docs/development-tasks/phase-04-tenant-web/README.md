# Tenant-operator Web assignments

This is a future assignment catalog, not an implementation or qualification claim. Current truth remains in `README.IMPLEMENTATION.md`; product version stays v0.1.0. Catalog status expresses readiness to assign after prerequisites, not PRODUCTION_HONEST/BLOCKED runtime state.

GATE-002 backend readiness must qualify before this runtime implementation starts. Quotation support, accepted customer-policy/approval behavior and actual fulfillment are required; quotation use per order is optional. Supplier/purchasing/inventory branches follow selected qualified backend cases. External customer portal remains a separate conditional decision.

Use shared [agent rules](../AGENT_RULES.md), [handoff](../HANDOFF_AND_INTEGRATION.md) and the [generated phase index](TASK_INDEX.md). Exact backend dependencies are already declared in each task. A frontend cannot hide an absent backend behind a screen. WEB-015/016 precede WEB-014 qualification despite their larger stable IDs.

| Task | Outcome | Status | Release requirement |
|---|---|---|---|
| [WEB-001](WEB-001-decide-tenant-web-rendering-and-session-topology.md) | Decide tenant Web rendering and session topology | DECISION_REQUIRED | REQUIRED |
| [WEB-002](WEB-002-real-operator-login-tenant-entry-and-application-shell.md) | Real operator login, tenant entry and application shell | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-003](WEB-003-customer-organization-program-and-individual-screens.md) | Customer organization, program and individual screens | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-004](WEB-004-priced-draft-entry-preview-revision-and-abandonment.md) | Priced draft entry, preview, revision and abandonment | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-005](WEB-005-commitment-and-explainable-permitted-next-actions.md) | Commitment and explainable permitted next actions | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-006](WEB-006-optional-use-quotation-and-conditional-approval-continuation.md) | Optional-use quotation and conditional approval continuation | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-007](WEB-007-actual-fulfillment-with-conditional-supplier-performed-work.md) | Actual fulfillment with conditional supplier-performed work | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-008](WEB-008-conditional-supplier-purchasing-and-payable-screens.md) | Conditional supplier purchasing and payable screens | CONDITIONAL | CONDITIONAL |
| [WEB-009](WEB-009-conditional-item-inventory-and-movement-screens.md) | Conditional item inventory and movement screens | CONDITIONAL | CONDITIONAL |
| [WEB-010](WEB-010-invoice-receivable-and-manual-payment-allocation-screens.md) | Invoice, receivable and manual payment allocation screens | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-011](WEB-011-corrections-statements-and-retained-business-documents.md) | Corrections, statements and retained business documents | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-012](WEB-012-tenant-settings-profiles-usage-and-permission-administration.md) | Tenant settings, profiles, usage and permission administration | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-013](WEB-013-conditional-external-customer-portal-decision-and-boundary.md) | Conditional external customer portal decision and boundary | CONDITIONAL | CONDITIONAL |
| [WEB-014](WEB-014-qualify-the-complete-tenant-operator-journey.md) | Qualify the complete tenant-operator journey | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-015](WEB-015-catalog-and-pricing-policy-authoring-screens.md) | Catalog and pricing-policy authoring | READY_AFTER_DEPENDENCIES | REQUIRED |
| [WEB-016](WEB-016-customer-policy-forms-and-approval-configuration-screens.md) | Customer policy, typed forms and approval configuration | READY_AFTER_DEPENDENCIES | REQUIRED |

GPT-6.1 Sol owns identity, session, authority and new capability flows. Luna or an available Flash model can handle fixed-contract display/component slices and read-only reviews with stronger integration review. Money/authority changes escalate; gate qualification remains strong-model/owner work. See [model routing](../MODEL_ROUTING.md).

Every task requires its scoped static security review, exact dynamic evidence, lasting regression guard and source-only ZIP. Receiving integration runs .NET 10 and actual Playwright/provider checks. Missing environments remain explicit; no SDK/test pass is claimed by writing this catalog. Workstation, SyncApi and Guard are deferred future boundaries and are not implemented by these phases. No folder/project is created merely to mirror this plan.
