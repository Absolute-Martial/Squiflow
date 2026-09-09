# Business Model and Practical Domain Scope

**Version:** v0.0.15

This document keeps SquiFlow grounded in the actual businesses it needs to support rather than turning every possible ERP feature into baseline architecture.

## 1. Core commercial model

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

## 2. Minimal shared business semantics

Do not let each module invent incompatible meanings for money, quantities, time or corrections.

Current minimal rules are in `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`:
- currency is configurable/not hardcoded;
- monetary history retains the applicable currency code where needed;
- quantity/unit semantics are explicit only as far as implemented journeys require;
- authoritative timestamp and business date/timezone are not confused;
- internal ID is different from a human/legal document number;
- posted/issued truth is corrected/revised rather than silently overwritten.

Do not turn these into generic frameworks before two real modules need shared implementation.

Jurisdiction-specific tax/invoice numbering/privacy requirements remain OPEN.

## 3. Customer scenarios

Support as slices require:
- walk-in / temporary customer;
- registered individual customer;
- organization/customer account;
- organization with programs/projects;
- representative acting for an organization/program;
- credit customer;
- future client-client/end-customer portal.

Typical small-shop flow:

```text
walk-in customer
→ order/request
→ design/print work
→ pickup/fulfillment
→ payment
```

Do not require a complex account hierarchy for a walk-in sale.

## 4. Organization/program billing

Organizations may have multiple programs/projects while settling at organization/account level.

Support where required:
- transaction linked to program/project;
- organization-level credit/settlement;
- consolidated billing/reporting;
- representative/contact relationships.

## 5. Products/services and pricing

Do not force a universal `ready-made` vs `custom-design` category.

Do not create a separate `social` or other generic catalog category merely because an external ERP/system-design taxonomy lists one. A tenant-visible category exists only when it represents a real item/service/reporting/pricing distinction for the business; category labels must not hard-code a product model that the tenant does not use.

Price may come from:
- standard/default price;
- organization/program price;
- wholesale price;
- negotiated quotation;
- Owner/manual permitted final price;
- outsourced-printing resale price.

Owner-authorized final pricing remains possible inside permission/rule limits.

The Owner can change applicable tenant/default/organization/wholesale/negotiated/outsourced-resale prices and can authorize the final price used by a transaction. Outsourced printing may therefore use a lower Owner-decided resale price when the external printer supplied the design-ready production input. Once a quotation/invoice/other issued record is posted, corrections use the defined revision/correction flow rather than silently rewriting history.

Historical issued totals retain the applied values/currency/configuration needed to explain them; later tenant settings must not silently recompute old issued truth.

## 6. Quotations/tenders

Support version/revision history and tenant workflow only as needed:
- draft;
- submitted/issued;
- revised;
- accepted/rejected/expired;
- tender-specific approval/revision where required.

Do not freeze every business into one universal quotation type list. Add a quotation/tender kind or tenant-visible label when an implemented journey needs different fields, approvals, numbering, validity, pricing, or reporting while retaining the shared revision/issued-history rules.

Issued versions should remain explainable/immutable rather than silently overwritten.

## 7. Printing and outsourced work

Two separate concerns exist:

### Local printing

Workstation sends an already committed/printable document through the normal Windows printer/spooler path. Print failure does not undo the underlying sale/order/invoice.

Owner: `docs/workstation/LOCAL_FIRST_DESKTOP.md`.

### Outsourced production/printing

If another printer/supplier performs only printing or production, record the actual external work/cost/payable. Do not invent design work that the supplier did not perform.

This is not a full manufacturing/MRP subsystem.

## 8. Suppliers and purchasing

Practical purchasing can support:
- supplier;
- requested/received items/services;
- cost;
- partial payment/outstanding payable where enabled;
- link to customer job/order where useful.

Phone/informal supplier ordering is valid. Do not require a formal PO workflow for every purchase.

## 9. Inventory scope

Possible tracking modes by item/category:
- precise quantity;
- availability-only;
- non-stock/service;
- damaged/unusable adjustment where useful.

Not baseline without a real tenant need:
- universal reservation subsystem;
- MRP/production planning;
- complex banner/roll wastage optimization;
- universal batch/lot/serial tracking;
- manufacturing BOM/routing.

## 10. Sales/order lifecycle

Required scenarios as the order slice grows:
- draft/abandon;
- submit/accept;
- partial fulfillment;
- cancellation before irreversible effects;
- correction/reversal after effects;
- return/refund;
- concurrent edit;
- offline local/pending state;
- print failure after transaction success.

Posted financial/stock truth is not a mutable form that can be edited away.

## 11. Payments/credit

Payment status is richer than `Paid = true/false`:

```text
NotStarted
Pending
Succeeded
Failed
OutcomeUnknown
PartiallyRefunded
Refunded
Reversed
```

Credit operations that depend on current shared exposure may be provisional or server-required offline.

Currency is not hardcoded. v0.0.15 does not add exchange rates or multi-currency accounting unless a real customer requires it.

## 12. Documents/files

Business documents can include quotation/tender, invoice/receipt, purchase document, customer artwork, generated PDF/report and attachments.

Business metadata/reference lives in the DB; durable retained bytes use the current private Hugging Face Storage Bucket according to `docs/data/FILES_AND_OBJECT_STORAGE.md`.

Encrypted bootstrap backup uses private Kaggle as documented there; raw customer data is not uploaded to Kaggle as ordinary dataset files.

## 13. Client-client hosted Web

A tenant may later expose a separate hosted/custom-domain portal to its own customers.

Potential capabilities:
- request/view quotation;
- upload artwork;
- check order status;
- view permitted documents;
- submit permitted information.

Client portal authority is separate from Staff/operator authority.

The exact first portal scope/account model is OPEN until that slice is scheduled. It inherits the current Web rule: online-only business behavior unless a later decision changes it.

## 14. Notifications/external integrations

Do not build a communications platform by default.

For a real journey ask whether an in-app work inbox is enough and which external channel is actually needed. When email/SMS/webhook delivery is required, use the existing API/outbox/future Worker boundary first.

Owner: `docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md`.

## 15. Dynamic rules/workflow/forms

Tenant variation can be represented through bounded configuration for required fields, validation, pricing/approval rules, workflow stages/transitions and feature visibility.

Hard tenant/security/domain invariants remain strongly typed and non-overridable.

Phase 5 proves one bounded/versioned form lifecycle; it does not create an arbitrary HTML/script application builder.

## 16. Import/onboarding

Real customers may have spreadsheets/legacy records.

Do not build generic ETL now. If onboarding requires import, start with the exact bounded format/customer data required, with validation, row errors, tenant scope, duplicate handling and restart/idempotency appropriate to that import.

## 17. Small-business usability rule

Owner + one Staff should be able to set up permissions, create customers/orders, print/fulfill, record payment, buy/receive materials, handle quotation/revision and understand pending/synced work without configuring dozens of enterprise modules first.

Generic architecture, DDD, database, and design-pattern sources may improve how these journeys are implemented; they do not replace these business decisions with a generic catalog, manufacturing, procurement, or ERP taxonomy.
