# Business operation end-to-end scope

**Status:** source-backed business scope and implementation guide, not a completed
backend or a universal tenant workflow. Current executable truth remains in
`README.IMPLEMENTATION.md`. Product version remains v0.0.1.

**Owner request, 2026-10-02:** first identify the complete business operation;
separate pricing; support guided, adaptable operation for each tenant's customers
and programs. Do not continue adding isolated order states without this map.

## 1. Actors and boundaries

The tenant is the business using the application. Its customer is a different
business identity: an organization, program or individual customer/billing record.
An authenticated operator is different again. Signing in does not make an operator
the customer, debtor or approver.

The first supported commercial case is an organization with program/account
billing. The organization is the default debtor; a program can owe independently
or charges can be assigned to a separate individual customer/person billing record.
The selected debtor is retained at invoice issue time. Draft customer attribution
alone grants no billing or credit authority.

Default Owner/Staff operating templates are not a complete role implementation.
Representative/contact relationships and end-customer portal identities need their
own meaning and permissions. Platform administrators do not automatically become
tenant operators or gain customer-data access.

Behavior owners: `docs/domain/BUSINESS_MODEL.md`, `BUSINESS_TERMS.md`,
`CROSS_CUTTING_BUSINESS_PRIMITIVES.md`, `docs/security/TENANT_PERMISSIONS.md`.

## 2. Connected commercial operation

```text
Customer / organization / optional program / commercial context
    ↓
Product or service requirements + quantity and unit + required information
    ↓
Applicable price selection + authorized override, where supported
    ↓
Explainable valuation
    ├─ optional quotation → issue/revise → accept/reject/expire
    └─ direct order draft
    ↓
Order admission + required approval/current-authority checks
    ↓
Conditional production / outsourcing / stock / fulfillment
    ↓
Selected debtor + invoice issue + receivable
    ↓
Payment recording/reconciliation + allocation + settlement
```

This is a dependency picture, not a required execution sequence. Supported cases
may require deposits/payment before fulfillment, partial handoffs, several invoices
or no quotation. Cancellation, disputes, revisions, corrections, returns, credits
and refunds branch from the effect they actually correct. A payment is not a
boolean field on an order, and a fulfilled order is not automatically settled.

## 3. Complete responsibility inventory

Every row below is needed in the scope review; conditional rows are implemented
only for an explicitly supported case. A folder or accepted requirement is not
runtime implementation. Absent responsibility is `NOT_INTRODUCED`.

| Responsibility | Actual current behavior | Remaining business outcome / decision |
|---|---|---|
| Customer/commercial setup | Organization/program and individual records, contact/availability changes, representative relationships, manual duplicate decisions/forward canonicalization, bounded CSV planning and durable background row execution with independent permissions | Accepted raw-source retention remains missing OPS-008 work; billing/approval assignment remains separate |
| Products/services | Draft line description, quantity and ASCII unit code | Supported catalog/service identity, tenant categories, availability and any actual unit conversions; no universal ready-made/custom taxonomy; see `CATALOG_AND_UNIT_BOUNDARY.md` |
| Pricing | Backward-compatible manual entry plus versioned contextual price publication/selection, reasoned envelope-controlled overrides, explainable immutable facts and pre-commit revalidation | Committed quotation/agreement owning facts, discounts/approval workflows, supplier cost and outsourced resale authority remain separate; see `PRICING_POLICY_AND_PUBLICATION.md` |
| Order creation | Idempotent priced draft creation, optional validated organization/program attribution; protected non-persisting price preview through the same calculator | Guided required fields, effective customer policy, price-source evidence, optional quotation origin, applicable approval/admission prerequisites |
| Draft revision | Expected-revision replacement for manual or separate catalog-priced entry, independent authority, retained receipts/history; catalog-priced commitment rejects changed current policy/source/compatibility without silent repricing | General requotation/approval workflow and further lifecycle remain absent; immutable committed/issued facts never change |
| Quotations/tenders | Absent | Draft, issued immutable revisions, validity, acceptance/rejection/expiry and conversion; quotations remain optional |
| Order admission | Direct commitment freezes current priced content and customer attribution with separate current authority, revision and durable retry/history | Applicable approval prerequisites, published policy evidence and later fulfillment/billing effects; commitment alone does not create them |
| Approval and human continuation | Absent | Current approver authority, work discovery, required evidence, rejection, reassignment and unavailable-approver recovery |
| Production and fulfillment | Absent | Actual work steps, artwork/customer approvals, partial quantities, handoff/pickup/delivery facts, deadlines and discrepancy handling |
| Outsourcing | Absent | Supplier-performed production/printing, actual supplied input, cost/payable and link to customer work; do not invent design work |
| Suppliers/purchasing | Absent | Supplier, requested/received item or service, actual cost, partial payment/payable and optional order link; informal/phone ordering must remain possible |
| Inventory | Absent | Item-specific precise quantity, availability-only or non-stock/service behavior; movements, damaged adjustments and concurrency where tracking is enabled |
| Billing/debtor selection | Draft attribution only | Organization default, independent program or individual debtor; assignment authority, effective version, issue-time retention and consolidated statements |
| Invoice/receivable | Absent | Issue, numbering, dates, frozen prices/debtor, outstanding balance and safe corrections; tax excluded from first scope without claiming fiscal compliance |
| Payments/settlement | Absent | Manual/provider payment intent and outcome, partial payment, allocation to exact receivables, unapplied amount and reconciliation; uncertain outcomes remain explicit |
| Credit | Absent | Credit terms/limit and current shared exposure, approval and overdue handling; a stale cached balance cannot authorize new exposure |
| Cancellation/correction | Only draft abandonment | Before-effect cancellation versus after-effect corrective records; retained history, linked compensations and partial-effect handling |
| Returns/credit/refunds | Absent | Returned quantities/disposition, credit adjustments, refund eligibility/authorization and reconciled financial result; none implies every other reversal happened |
| Documents/artwork/printing | Absent | Retained quotation/invoice/receipt, templates/export, artwork and attachments; physical print failure never undoes committed business facts |
| Business history/reporting | Protected bounded view of retained draft-command actors/times and priced revisions from immutable receipts; lifecycle fields and best-effort diagnostics | Override reasons and broader attributable event history; receivable/payable/settlement explanation and useful reports; telemetry is not an audit ledger |
| Business notifications/integrations | Absent | Named consequences, durable delivery/retry/reconciliation and an in-product continuation owner even when delivery fails |
| Customer portal | Absent | Separately authorized customer views/actions and document/artwork access; operator permissions must not be reused as portal authority |
| Local continuity | No Workstation or sync runtime | Later provisional local work, durable pending intent and server receipt reconciliation; financial/current-authority effects remain explicitly server-controlled |

Scope sources: `BUSINESS_MODEL.md` sections 3–17; `WORKFLOW_DESIGN.md`;
`NATIVE_RULE_ENGINE.md`; `NON_FUNCTIONAL_REQUIREMENTS.md` NFR-BIZ-001–012;
`FILES_AND_OBJECT_STORAGE.md`; `NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md`;
`INTEGRATION_RESPONSIBILITY_AND_AUTHORITY.md`. Source/code authority prevails over
an older capability summary that still calls draft revision absent.

## 4. Adaptation without redefining business truth

Two independently versioned concerns must stay distinguishable:

| Protected business meaning | Supported configurable guidance |
|---|---|
| Tenant isolation, current permissions, idempotency, expected revisions | Labels, required supplementary information and compatible typed forms |
| Actual order/fulfillment/invoice/payment/correction effects | Customer/program stages, required approvals and supported next actions |
| Currency/precision and historical issued facts | Applicable price policy and permitted source/override selection |
| Debtor, stock, receivable/payable and settlement authority | Supported default billing choice, terms and customer-specific workflow policy |

Resolve supported customer/program policy inside the current tenant. Do not key
behavior on hardcoded customer names, GUIDs or organization categories. Do not
assume a precedence order for default/program/wholesale/quotation/override prices:
that is an explicit business decision. Pin the effective compatible policy,
rule/form/workflow versions for the operation and explain the selected action.

Guidance must say who can act next, what information is missing, why an action is
unavailable, and what happens if nobody acts. The server checks the same rules;
frontend hiding never grants authority. Customer-specific stages cannot manufacture
a paid invoice, stock movement, successful external refund or accepted approval.

Publishing a policy change does not silently reinterpret running work. Define
grandfathering, explicit migration or required revalidation for the implemented
change. Tenant code/scripts/SQL uploads are not part of this adaptation model.
The current feature compiler and dormant Autofac registry do not implement it.

Owners: `docs/workflow/WORKFLOW_DESIGN.md`, `docs/rules/NATIVE_RULE_ENGINE.md`,
`docs/architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md`.

## 5. What qualifies end-to-end completion

For each supported commercial case, demonstrate through owned APIs:

1. Configure only the capabilities and policy needed by that case.
2. Establish the real customer/commercial context and authorized actors.
3. Explain and apply a valid price, create/revise intent, use quotation if required.
4. Pass current approval/admission checks and record the authoritative outcome.
5. Record the actual fulfillment/outsourcing/stock effects the case needs.
6. Issue the correctly attributed invoice/receivable with retained prices/debtor.
7. Record, reconcile and allocate payment until the supported settlement outcome.
8. Handle applicable partial, cancelled, disputed, returned and refunded outcomes.
9. Read retained documents/history after prices or workflows have changed.
10. Retry uncertain responses and recover interrupted work without duplicate effects.

Evidence must cover forbidden authority, cross-tenant/customer mistakes, concurrent
actors, stale revisions, duplicate keys, unavailable dependencies, ambiguous external
effects and partial compensation. Actual PostgreSQL tests own transactional claims.
Business observability must explain the outcome without leaking customer content.

Deployment/recovery and identity qualification remain necessary backend gates,
but infrastructure checklists are not substitutes for this business journey.

### Required case coverage

These are business cases to qualify, not tests that already pass:

| Case | End-to-end evidence required |
|---|---|
| Organization default billing | Create customer/program context, select/explain price, place a direct order, complete the supported work, issue to the organization and reconcile/allocate its payment |
| Independently owing program | Preserve program attribution and explicit program debtor; issue and settle its receivable without silently treating the parent organization as the debtor |
| Assigned individual debtor | An authorized actor selects a separate customer/person billing record; issued bill-to remains unchanged after later assignment changes; operator identity never substitutes for debtor identity |
| Optional quotation | Retain issued/revised offer facts, accept a valid version and create the supported order without duplicate conversion on retry; direct order creation remains usable without a quotation |
| Customer-specific approval/guidance | Resolve the compatible published policy for that customer/program, explain required information and permitted next actions, deny skipped mandatory approval and provide unavailable-actor recovery |
| Outsourced production | Record only work the supplier performed, its actual cost/payable and the independently permitted customer resale price; customer settlement does not automatically settle the supplier |
| Partial effects and corrections | Explain remaining fulfillment/receivable/payment state; cancel only reversible pending work, and record the specific return, credit, stock adjustment or refund needed for effects already committed |

Customer portal, precise stock tracking, purchasing import and offline continuity
need additional cases when included in the supported product promise. Excluding
an unneeded case is an explicit scope decision, not proof that it was implemented.

## 6. Implementation order and unresolved decisions

Start by separating current price calculation from order-intent assembly without
changing values, fingerprints, receipts or wire contracts. Then introduce the
first concrete price source/policy with durable publication and authority. That
supports guided order creation and explicit acceptance instead of freezing a
single customer workflow prematurely. Add quotation where needed, then the first
real fulfillment/billing/payment path and its corrections, one complete slice at
a time. Purchasing/stock/outsourcing join when that supported path needs them.

Before introducing affected durable contracts, resolve price precedence and
override ceiling; the first extra customer/program workflow variation; individual
billing fields/lifecycle; admission effects; fulfillment quantity/stage meaning;
invoice numbering/business date; allocation/credit rules and post-effect correction
rules. `OPEN_DECISIONS.md` remains the registry; this map does not close them.

The started acceptance-state implementation was removed on the owner's steering
to perform this review first. No acceptance endpoint, migration or new financial
state is introduced by this map. Frontend work follows qualified backend business
operations rather than hiding their absence behind a screen.
