# COM-016 decision proposal: one informal supplier service purchase

**Status:** Accepted by the owner on 2026-10-10 for the complete proposed bounded contract. Acceptance authorizes implementation; qualification remains pending until its native evidence and normal repository gate pass.

## Proposed first case

Record one tenant's informal phone/requested purchase from one supplier for a
single non-stock printing or other supplier-performed service. The request may
optionally reference exactly one line of an already committed Order. Record only
what the supplier actually supplied, what that supply cost, what remains payable,
and any cash or bank amounts the tenant actually paid the supplier. Partial
receipts and partial payments are supported. A missing Order link is valid and
must not block a phone-order workflow.

The Order link is explanatory cross-reference only. It does not change the
committed Order or its frozen selling price, prove customer acceptance, create a
customer payable/receivable, or authorize customer billing, payment, inventory,
or fulfillment effects. Supplier cost is entered independently; no markup,
resale price, tax, or cost-plus calculation is derived.

## Proposed identity and facts

- Purchasing owns a tenant-scoped Supplier identity with stable ID, required
  bounded display name, optional bounded contact details, and active/retired
  state. Retiring retains history and prevents new requests. Supplier identity
  is a purchasing role in this useful capability, not a general `Party` record,
  `PartyKind`, shared identity registry, or cross-role customer/supplier merge.
- A request records its supplier, bounded service description, positive requested
  quantity, unit identity/code/precision selected from the existing tenant
  Catalog, request time and actor, and optional committed Order ID plus one line
  identity/revision. The unit's stable identity, code, and precision are
  retained; a later Catalog rename or retirement does not reinterpret the
  request. A linked Order/line is validated as a current committed tenant fact,
  but its price, text, and quantity do not supply the purchase description,
  quantity, or cost. The request is an operator record of an informal
  instruction; it is not a supplier-confirmed purchase order or evidence of
  legal acceptance.
- Quantity uses exact decimal arithmetic, no rounding, tolerance, or unit
  conversion. Support only units whose declared precision is 0 through 4 for
  this first case; `0.001` is accepted exactly when the selected unit permits
  three decimal places. Receipt quantities must be positive and exact at the
  retained unit precision. Their cumulative net amount cannot exceed the
  requested quantity. This follows Catalog's positive, unit-precision validation
  and Fulfillment's exact partial quantity rules, including its current four
  decimal-place ceiling. No stock balance or numerical inventory effect is
  created.
- Each actual receipt is an immutable event with a positive quantity and a
  positive, operator-entered **total cost for that receipt**, in NPR with two
  decimal places. It creates the same positive payable amount. A later partial
  receipt gets its own quantity, actual cost, and payable amount; the system does
  not estimate remaining cost from the request or spread a total across receipts.
  These facts are never recomputed from later catalog, supplier, or Order data.
- Each supplier payment is an immutable, positive NPR `decimal(19,2)` amount
  with method `cash` or `bank`, server time, actor, and optional bounded
  external reference. It is allocated to one payable only. Total effective
  payments cannot exceed the current payable. Customer payments and balances
  are unrelated.
- Request cancellation appends an attributable event for only the unreceived
  remainder. It cannot erase, cancel, or alter a received quantity or payable.
  A fully received request has no cancellable remainder.
- Corrections append a linked reversal/replacement event with reason, actor,
  server time, expected revision, and the existing recent trusted Tenant Web
  strong-authentication contract plus current initial-Owner authority and the
  separate correction permission. Original
  request, receipt, and payment facts remain readable; corrections never delete
  or rewrite them. Net receipt quantity remains within zero and requested
  quantity. A corrected payable cannot fall below effective supplier payments
  unless a separately recorded supplier refund covers the difference. A refund
  records positive NPR actually returned by the supplier and is linked to the
  affected payable/payment history; it is not an inferred correction or customer
  refund. No correction can cause over-receipt or over-payment.

The stored supplier balance is the derived sum of payable amounts minus supplier
payments plus recorded supplier refunds for that supplier. Corrections that
would make this value negative are rejected until a refund is recorded. It is
independent of customer balances. No general ledger, tax calculation/recovery, inventory
valuation, supplier invoice compliance claim, credit facility, purchase
approval workflow, or customer resale pricing is introduced.

## Authority and transaction boundary

CoreApi is the only proposed transport. Every request resolves current
tenant membership and checks current permission before parsing or performing a
mutation. Proposed independent permissions are:

| Permission | Scope |
|---|---|
| `purchasing.supplier.view` | Read supplier identity and bounded purchasing/payable history. |
| `purchasing.supplier.manage` | Create, revise or retire supplier contact identity with expected revisions and retained history. |
| `purchasing.request.record` | Create and cancel an informal request. |
| `purchasing.receipt.record` | Record actual partial supplier receipt and cost. |
| `purchasing.payment.record` | Record cash/bank supplier payments and actual supplier refunds. |
| `purchasing.correct` | Append corrections, with required recent strong authentication. |

No permission implies another. OpenFGA checks must use the pinned model and
intersect grants with current tenant membership; database tenant isolation
remains independently required. Current membership and authority are checked
for reads, new commands, and idempotent replays. A tenant's customer payment
authority grants no supplier-payment authority.

PostgreSQL owns the durable request, receipt, payable, payment/refund, correction,
and command-receipt facts. Tenant RLS, parameterized SQL, scoped foreign keys,
expected revisions, semantic idempotency, and row/transaction locking prevent
cross-tenant reads, duplicate retries, over-receipt, and over-allocation during
concurrent commands. A successful mutation and its receipt commit atomically;
provider or authorization failure produces no partial business effect. Queries
are bounded and read-only. No in-memory payable or best-effort fallback is
authoritative.

## Scope exclusions

This case does not add a generic procurement framework, formal purchase-order
approval, contract or supplier-side API, multi-line request, stock movement,
inventory reservation, outsourcing workflow, artwork/file exchange, tax, a
general ledger, customer receivable/payment effects, conversion between units,
FX, automatic resale markup, or general-purpose Party model. COM-018 remains the
owner for tracked inventory. Do not add feature-catalog/profile infrastructure
for purchasing; this proposal selects one useful owner-controlled case only.

## Owner decisions accepted on 2026-10-10

1. **Select this exact case.** Confirm that an informal phone request for one
   non-stock service, with optional one-line committed-Order attribution, is the
   real next supported workload. Without that selection COM-016 remains
   `NOT_INTRODUCED` and no code should be written.
2. **Confirm receipt cost meaning.** The proposal treats each receipt's NPR
   `decimal(19,2)` cost as the actual total amount attributable to that receipt,
   including any supplier-side charges the operator intends to include, with no
   tax breakdown. Confirm this is sufficient for the first payable record.
3. **Confirm supplier identity minimum.** The proposal stores a tenant-owned
   display name and optional contact details, without registration-number
   verification or automatic deduplication. Confirm those facts identify the
   supplier well enough for the supported case.
4. **Confirm correction authority.** Accept the current initial Owner,
   the separate `purchasing.correct` permission, and the existing recent trusted
   Tenant Web strong-authentication policy already used for Fulfillment entry
   corrections. The proposal adds no new authentication context or age rule.
5. **Confirm refund records.** The proposal supports an actual supplier refund
   event only to reconcile an already-paid amount after a payable correction.
   Confirm that this belongs in the first case; otherwise the first scope must
   reject corrections that would reduce the payable below paid amounts and
   leave that overpayment-resolution path explicitly unsupported.

The owner accepted all five choices above, including actual supplier refund records and current initial-Owner correction authority. Permission-to-role defaults, HTTP paths, storage names, exact database numeric
types, response DTOs, and bounded field limits can be fixed by the focused
implementation owner after case selection. They do not need to become separate
product decisions unless implementation evidence reveals a real user or
compatibility tradeoff.

## Qualification obligations if selected

Claim only this tenant-owned single-service workflow. Falsifiable evidence must
cover PostgreSQL tenant isolation/RLS, atomic receipt+payable and
payment/refund+receipt writes, migration/refusal behavior, request cancellation
with partial receipt, exact unit precision and no over-receipt, cost/payable
rounding/range, payment allocation/no-overpay, correction linkage and
fresh-auth rejection, concurrent/idempotent mutations, bounded protected reads,
current permission rechecks, and independence from customer balances. Keep those
cases in recurring PostgreSQL/CoreApi tests, name the normal repository gate and
requalification triggers, and claim no deployed or observed-customer outcome
without its separate evidence.
