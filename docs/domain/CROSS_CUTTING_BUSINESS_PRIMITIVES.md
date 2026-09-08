# Cross-Cutting Business Primitives

**Version:** v0.0.15

These primitives are deliberately defined before broad business modules because getting them wrong creates inconsistent totals, dates, quantities, corrections and identifiers across Sales, Purchasing, Inventory and Finance.

## 1. Money

Do not represent money as an unqualified floating-point number.

A money value carries at least:

```text
amount
currency
```

Exact .NET/storage types remain provider/implementation choices, but rounding and precision are explicit by operation/currency policy.

Define separately:
- unit price precision;
- tax/discount calculation precision;
- display precision;
- settlement/rounding step;
- how line rounding and document-total rounding reconcile.

Do not silently change historical issued totals because a tenant later changes pricing/rounding configuration.

## 2. Currency

Initial tenants may operate in one currency, but the model must not assume every amount in all time is implicitly one global currency.

Before enabling multi-currency, define:
- document currency;
- settlement currency;
- exchange-rate source/time/evidence;
- gain/loss/accounting policy if needed;
- whether mixed-currency allocation is allowed.

Multi-currency is not automatically a v0.0.15 feature; currency identity in the data model is the baseline safety requirement.

## 3. Quantity and unit

Stock/service quantities need an explicit unit/capability model.

Examples:
- pieces;
- sheets;
- meters/feet;
- square area;
- rolls;
- service/non-stock quantity.

Do not assume every quantity is an integer or that all units are freely convertible.

Conversions, rounding and minimum increments are defined per item/category when the business actually requires them.

This does **not** introduce MRP/BOM/wastage optimization.

## 4. Time and business date

Store authoritative instants in an unambiguous form and keep the relevant business timezone/context for user-facing dates/deadlines.

Distinguish:
- absolute event timestamp;
- tenant/business local date;
- due date without a time where applicable;
- timezone used for scheduling/deadlines;
- device-reported local clock, which is not server authority.

Workstation offline creation can record device time for UX/evidence while server receipt/authoritative timestamps remain distinct.

DST/timezone changes and wrong device clocks are test cases.

## 5. Numbering and external references

Internal IDs and human/legal document numbers are different.

Internal IDs should remain stable across sync/revisions. Invoice/quotation/receipt numbering policy can be tenant/jurisdiction-specific and may require server authority or controlled offline allocation.

Do not make a mutable display number the primary identity of an Order/Payment.

Exact invoice numbering/legal rules remain OPEN until jurisdiction requirements are chosen.

## 6. Corrections versus mutation

Once a document/payment/stock effect becomes issued/posted business truth, correction is represented explicitly rather than silently overwriting history.

Possible patterns include:
- new revision;
- reversal;
- refund;
- adjustment;
- void/cancel before effect;
- corrective document.

The exact mechanism is aggregate-specific.

## 7. Party identity, duplicate detection and merge

Customers/organizations can be entered twice, especially offline or by different Workstations.

Do not enforce a fragile global uniqueness rule on name/phone/email.

A future merge operation must preserve:
- transaction history;
- organization/program relationships;
- external references;
- audit of which records were merged;
- conflict review for incompatible fields.

Automatic duplicate suggestions can exist later; automatic destructive merge is not baseline.

## 8. Privacy and data lifecycle

Tenant-owned customer/contact data needs an explicit lifecycle:
- active;
- archived/inactive where useful;
- retention requirements;
- export/access policy;
- deletion/anonymization where legally/product-required;
- legal/business records that must remain immutable even if personal profile data is removed.

Do not promise universal deletion semantics before jurisdictional obligations are known.

## 9. Tenant configuration/version effect

Pricing, rounding, workflow, form and numbering configuration is versioned when a historical transaction must remain explainable.

A later configuration change does not retroactively alter the meaning of an issued document unless an explicit migration/correction is performed.

## 10. Testing

Property/invariant tests should cover:
- rounding totals;
- partial payment allocations;
- refund cannot exceed remaining refundable amount unless policy explicitly supports it;
- quantity never changes from a duplicate idempotent command;
- timezone/date boundaries;
- historical document remains stable after configuration change;
- duplicate Party merge preserves linked transactions.
