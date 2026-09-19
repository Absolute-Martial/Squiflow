# Cross-Cutting Business Primitives

**Version:** v0.1.0

Keep only the cross-cutting rules that prevent expensive inconsistency across Sales, Purchasing, Inventory, Payments and Documents. Do not turn this document into a framework or a future accounting system.

Consequential business-term meaning and disputed vocabulary are owned by `docs/domain/BUSINESS_TERMS.md`. This document owns the cross-cutting behavioral semantics below; the glossary should reference these rules rather than duplicate them.

## 1. Money and currency — minimal baseline

Do not hardcode one currency throughout business logic.

The v0.0.15 requirement is simply:

```text
Tenant.DefaultCurrencyCode

Money/financial record:
- Amount
- CurrencyCode where historical meaning requires it
```

Practical rules:
- tenant setup provides a default currency code;
- new monetary records default from the tenant configuration;
- issued/posted records retain the applicable currency code so changing the tenant default later does not reinterpret history;
- use decimal/fixed-precision monetary storage appropriate to the selected .NET/database implementation;
- define rounding where the first real pricing/payment slice actually needs it.

Do **not** implement now:
- exchange-rate fetching;
- FX conversion;
- multi-currency documents/accounting;
- gain/loss accounting;
- mixed-currency settlement;
- a currency service/provider abstraction;
- a generic Money framework with unnecessary operator/helper layers.

If a paying/customer requirement needs multi-currency later, design that feature from its real workflow.

## 2. Quantity and unit

Do not assume every quantity is an integer.

The first inventory/order slice stores the quantity and the unit semantics actually used by that item/business. Add conversions only when a real item flow needs them.

Examples may include piece, sheet, length/area, roll, or non-stock service quantity, but these are business data/configuration rather than a reason to build a generic units engine.

This does not introduce MRP/BOM/wastage optimization.

## 3. Time and business date

Keep a distinction between:
- an authoritative event timestamp;
- a tenant/business local date/timezone where the business meaning needs it;
- a device-reported time while offline.

Do not trust a Workstation clock as server authority.

Use the framework/platform date-time types directly where appropriate; do not create a custom time abstraction unless a real deterministic boundary earns it.

## 4. IDs and document numbers

Internal identity and human/legal document numbers are different.

An Order/Payment keeps a stable internal ID even if a displayed document number changes or is assigned later.

Invoice/receipt numbering rules remain jurisdiction/product requirements and are not solved speculatively.

## 5. Corrections versus destructive edits

Once an issued/posted business fact has financial/stock/legal meaning, correct it explicitly with the aggregate-appropriate action such as revision, refund, reversal, adjustment or corrective document.

Do not silently overwrite history merely because the UI still displays an edit form.

## 6. Duplicate Party/customer records

Do not enforce fragile global uniqueness on name/phone/email.

If duplicate merge becomes necessary, it must preserve linked transaction history and audit which identity survived. A generalized fuzzy-matching/merge engine is not baseline.

## 7. Privacy/data lifecycle

Do not promise one universal delete/anonymize rule before the actual jurisdiction/product requirement is known.

When a customer-data lifecycle is implemented, distinguish removable profile/contact data from business/financial records that must remain for legitimate historical reasons.

## 8. Configuration changes do not rewrite history

Pricing, currency default, workflow, form and numbering configuration can change over time. Issued/posted records retain enough applied values/version identity that later configuration does not silently alter historical truth.

## 9. Implementation rule

These concepts do not require one shared `Primitives` project, helper library, or interface hierarchy on day one.

Place the minimal concrete types with the first module that needs them and extract a shared project only when two real modules need the same stable implementation/contract.
