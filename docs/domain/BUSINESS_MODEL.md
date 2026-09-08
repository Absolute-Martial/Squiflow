# Business Model and Practical Domain Scope

**Version:** v0.0.15

This document keeps the domain grounded in the actual businesses SquiFlow is intended to support instead of inventing enterprise subsystems merely because ERP products have them.

## 1. Core commercial model

Use these concepts to connect the business modules:

```text
Party
→ Commercial Relationship / Account
→ Business Context
→ Transaction
→ Workflow
→ Settlement
```

A Party can be an individual customer, organization, supplier, representative or other actor.

A Business Context can include tenant, branch/location, program/project and Workstation where relevant.

## 2. Shared business primitives

Sales, Purchasing, Inventory and Finance must not each invent incompatible representations for money, quantities, dates or corrections.

Use the shared rules in `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md` for:
- Money + currency identity + rounding;
- Quantity + Unit;
- absolute timestamp versus tenant/business date/timezone;
- internal ID versus human/legal document numbering;
- corrections/reversals/revisions;
- Party duplicate/merge;
- privacy/data-lifecycle concepts.

Jurisdiction-specific tax/invoice numbering/privacy policy remains OPEN until the deployment/product market is selected.

## 3. Customer scenarios

Support:
- walk-in / temporary customer;
- registered individual customer;
- organization/customer account;
- organization with multiple programs/projects;
- representative acting for an organization/program;
- credit customer;
- client-client/end-customer through the hosted portal when that surface enters implementation scope.

Typical small-shop flow:

```text
walk-in customer
→ order/request
→ design/print work
→ pickup/fulfillment
→ payment
```

Do not require a full account hierarchy for a simple walk-in sale.

## 4. Organization/program billing

Organizations may have several programs/projects but settle at an aggregate organization/account level.

Support:
- transaction associated with a specific program;
- organization-level credit/settlement where configured;
- consolidated billing/reporting;
- representative/contact relationships.

## 5. Products/services and pricing

SquiFlow supports products/services appropriate to the business rather than enforcing a `ready-made` versus `custom-design` split when the tenant's work does not use that distinction.

Owner-authorized pricing is important. The platform can provide default/reference pricing, approval thresholds and audit, but final permitted price decisions remain with the tenant's authority model.

Examples:
- standard price;
- organization/program price;
- wholesale price;
- negotiated quotation price;
- owner/manual final price;
- outsourced-printing price.

Historical issued totals remain tied to the applicable pricing/rounding/configuration version; a later configuration change must not silently recompute old issued truth.

## 6. Quotations/tenders

Quotation support is not one simple quote state.

Support version/revision history and tenant-defined workflow where useful:
- draft;
- submitted/issued;
- revised;
- accepted/rejected/expired;
- tender-specific revision/approval where required.

Published/issued quotation versions should remain explainable/immutable rather than being silently overwritten.

## 7. Printing and outsourced work

A realistic case is that the tenant's printer is unavailable or another printer/friend/supplier performs only the printing.

SquiFlow should allow an outsourced production/printing cost or supplier purchase against the job/order without pretending SquiFlow also performed design work when the external party/customer supplied the design.

The Owner can determine the applicable resale/final price according to permissions/rules.

This is not the same as introducing a full manufacturing/MRP subsystem.

Local printer/spooler execution is a device side effect owned by `docs/workstation/GUARD_AND_DEVICE_INTEGRATION.md`; a print failure does not undo committed order/invoice truth.

## 8. Suppliers and purchasing

Suppliers may be contacted informally, including by phone, and may provide materials/services with partial payments.

Support practical supplier/purchase data:
- supplier;
- requested/ordered items/services;
- received amount/status;
- purchase cost;
- outstanding payable/partial payment where enabled;
- link to a customer job/order where useful.

Do not require complex procurement workflow for every small purchase.

## 9. Inventory scope

Inventory is practical stock knowledge, not a full manufacturing system.

Possible tracking modes by item/category:
- precise quantity;
- availability-only;
- non-stock/service;
- damaged/unusable quantity where useful.

Damaged stock should be representable through an auditable adjustment/disposition.

### Not baseline

Do not introduce these unless a real tenant requires them:
- generic reservation subsystem for everything;
- MRP/production planning;
- complex banner/roll wastage optimization;
- universal batch/lot/serial tracking;
- manufacturing BOM/routing.

Banner/roll wastage can become a specialized future calculation if the business benefit justifies its complexity.

## 10. Sales/order lifecycle

Exact states vary by profile, but the model must distinguish draft work from posted/completed business history.

Required scenarios include:
- draft/abandon;
- submit/accept;
- partial fulfillment;
- cancellation before irreversible effects;
- correction/reversal after effects;
- return/refund;
- concurrent edit;
- offline provisional state;
- print failure after transaction success.

Do not implement posted financial/stock history as a mutable form that can simply be edited away.

## 11. Payments/credit

Payment status is richer than `Paid = true/false`.

Possible operational states:
- NotStarted;
- Pending;
- Succeeded;
- Failed;
- OutcomeUnknown;
- PartiallyRefunded;
- Refunded;
- Reversed.

Credit uses current authoritative exposure for operations where exceeding a limit matters. Offline credit-related operations may be provisional or server-required depending on policy.

Money/currency/rounding/allocation semantics come from the cross-cutting primitive contract; a payment module may not invent a separate incompatible `double amount` model.

## 12. Documents/files

Business documents can include:
- quotation/tender documents;
- invoice/receipt;
- purchase document;
- customer artwork/source files;
- generated PDF/report;
- attachments.

Database stores business metadata/reference; durable bytes live in object storage or local pending/staging storage according to lifecycle.

The current ~100 GB primary object-storage envelope and retention/backup rules are owned by `docs/data/FILES_AND_OBJECT_STORAGE.md` and the deployment-capacity plan.

## 13. Client-client hosted Web

A SquiFlow tenant may expose a separate hosted/custom-domain portal to its own customers.

This is not the same security surface as staff Web. Client-client identity/tokens/routes can never be used as staff/operator authority.

Potential capabilities are enabled per tenant, for example:
- view/request quotation;
- upload design/artwork;
- check order status;
- view permitted documents;
- submit permitted information.

The exact first portal scope and customer-account authentication model remain OPEN until this surface becomes an implementation slice.

When implemented, it inherits the current Web rule: online-only business behavior unless a later decision explicitly changes it, plus the accessibility/security requirements for public/client-facing Web.

Do not assume helpdesk/marketing automation is a core product requirement.

## 14. Notifications and external integrations

An order/workflow may need to tell a customer or staff member something, but this does not justify a communications platform by default.

External email/SMS/webhook delivery is a consequence capability owned by `docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md`.

For each journey ask:
- is an in-app work inbox enough?;
- which external channel is genuinely required?;
- does delivery failure change business truth or only notification state?;
- who can configure the channel/template?;
- what consent/privacy/provider-cost rule applies?

Initial channels remain requirement-driven.

## 15. Dynamic rules/workflow/forms

Tenant variation should be represented through bounded configuration:
- required fields;
- validation;
- pricing/approval rules;
- workflow stages/transitions;
- feature visibility;
- program/branch scope.

Hard tenant-isolation/security/domain invariants remain strongly typed and non-overridable.

Dynamic form definitions are versioned/bounded data rather than arbitrary HTML/script. Phase 5 must prove one real form lifecycle, including what happens when an old draft/workflow instance references an older form definition.

## 16. Import/onboarding existing business data

Real customers may already have spreadsheets/legacy records.

Do not build a generic ETL platform now, but do not ignore onboarding migration either.

Before onboarding beyond manual entry becomes a requirement, decide the first supported path, for example a bounded CSV import with:
- explicit template/schema version;
- dry-run/validation report;
- row-level errors;
- duplicate/Party matching policy;
- tenant scope;
- idempotent/restartable import where practical;
- audit/source evidence;
- formula/CSV injection safety on exports/import previews.

The first import scope remains OPEN and should be driven by actual customer onboarding data.

## 17. Small-business usability rule

The default experience must not require users to understand ERP terminology they do not need.

A tenant with Owner + one Staff member should be able to:
- set up team permissions;
- create customers/orders;
- print/fulfill;
- record payment;
- buy/receive materials;
- handle a quotation/revision;
- see pending work;
- understand what is offline/pending/synced;

without configuring dozens of enterprise modules first.
