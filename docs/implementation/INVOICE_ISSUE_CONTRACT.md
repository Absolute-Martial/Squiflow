# Invoice issue contract

**Product version:** v0.0.1\
**Status:** host-neutral issue behavior has focused pure-contract evidence; combined qualification remains `BLOCKED` pending independent review and the revised normal gate; durable invoice runtime remains `NOT_INTRODUCED`  
**Focused owner:** Invoices, limited to issuance of retained invoice facts from a committed Order

This owner defines the smallest invoice-issue boundary supported by current source
and accepted business decisions. It does not add an endpoint, project, migration,
provider adapter, authorization tuple or frontend. Before implementation, the
blocking choices under **Decisions required before implementation** must be closed.

## Source and authority inspected

The current source has two relevant public capability boundaries:

- Orders owns priced drafts and direct commitment. Commitment changes only lifecycle
  metadata and revision; it preserves the priced lines, totals, currency and frozen
  customer organization/program attribution. A committed order is terminal for the
  current Orders edit/abandon contract. `Application.Orders` currently references
  `Application.Customers` and `Application.Tenancy`.
- Customers owns tenant-scoped immutable organization/program identities and a
  separate individual billing record. Individual records have display name,
  optional email/phone, active/inactive availability and revision. The individual
  record is not an IdentityAccess login account. Current Customers queries do not
  grant debtor authority.

The first invoice business decisions are already accepted in
`docs/domain/BUSINESS_MODEL.md` and `docs/decisions/CURRENT_DECISIONS.md`: direct
committed orders are supported without a quotation; billing is separate from
commitment and fulfillment; organization is the default debtor; the attributed
program may owe independently; an individual customer billing record may be
selected; separate billing authority chooses and freezes the debtor; initial
currency is `NPR`; tax is outside this slice; and invoice arithmetic remains
`decimal(19,4)` with per-line `ToEven` rounding and a sum of rounded lines.

## Issue inputs and retained facts

The semantic issue command, independent of any future HTTP shape, needs only:

- the current tenant/account context established from active membership;
- one committed `orderId` and its `expectedOrderRevision`;
- debtor selection: omitted/default organization, the committed order's attributed
  program, or one explicitly selected individual billing-record identity;
- one caller-scoped idempotency key;
- the business date input or derivation required by the still-open business-date
  decision below.

The caller does **not** supply invoice prices, totals, currency, customer names,
issue timestamp, invoice identity or business reference. Those are authoritative
facts resolved or allocated by the owning capabilities.

A successful issue must retain an immutable invoice snapshot sufficient to explain
that issue without dereferencing mutable current state:

- tenant invoice identity;
- source Order identity and the exact committed Order revision used;
- committed customer attribution identities from the Order;
- invoice currency code `NPR`;
- each issued line's position, description, quantity, unit code, applied unit price
  and rounded line total, plus the summed invoice total, all at `decimal(19,4)`;
- selected debtor kind and source identity;
- debtor display name frozen at issue; for a program debtor, its organization and
  program identities/names are retained; for an organization debtor, its
  organization identity/name is retained; for an individual debtor, its individual
  identity, observed revision and display name are retained;
- issuing account identity;
- authoritative server issue timestamp in UTC;
- invoice business date once its authority is decided;
- the allocated organization-scoped business reference once its rule is decided.

Optional individual email/phone are intentionally not invoice facts in this
smallest slice. If a later printable/legal document needs contact or address facts,
that contract must explicitly choose and retain the exact issued values rather
than reading current Customers data during historical rendering.

Issued facts are append-only for this slice. A current customer rename/contact
change, future program/default change, authorization change or order/customer query
result must not rewrite or recompute an already-issued invoice. Correction,
credit/reissue and cancellation semantics are outside this contract.

## Debtor validation and current authority

Invoice issuance requires a distinct current billing permission. Order create,
view, edit, price, commit, customer create/view and individual-availability grants
are not substitutes. A future authorization model should follow the existing
pattern of active account + current tenant membership + one persisted tenant grant,
for example `invoice_issuer` producing `can_issue_invoice`; exact relation naming is
an implementation detail, not an additional business role.

Current membership and billing permission must be rechecked on every attempt,
including idempotent replay, before a retained invoice receipt is disclosed.
Authorization-provider unavailability fails closed. The invoice capability receives
only the resulting authoritative tenant/account context; provider SDK types remain
in the host authorization adapter.

Debtor resolution is tenant-scoped through Customers public application/query
contracts, never direct reads of Customers provider tables:

- default organization is valid only when the committed Order has frozen
  organization attribution, and that exact organization is the debtor source;
- program selection is valid only for the exact program frozen on the committed
  Order and therefore its owning organization; a sibling program is not silently
  substituted;
- individual selection must resolve the exact tenant-owned individual record and
  freeze its source identity/revision/display name. Whether `inactive` forbids a
  new selection is intentionally unresolved below because current Customers
  documentation does not assign that state debtor-eligibility meaning.

Wrong-tenant/missing customer identities must not be distinguishable through
financial issue results. The individual billing record remains independent of
IdentityAccess: selecting it never grants login authority, and the issuing account
never becomes the debtor by implication.

## Invoice and Order relationship

An invoice may be issued only from a current committed Order. Draft and abandoned
Orders are ineligible. The issue operation must compare the exact expected committed
revision before creating financial history. The Order's committed currency must be
`NPR`; another three-letter Order currency is a safe ineligibility outcome, not an
implicit conversion.

The committed Order is the authoritative source of priced facts. Invoice issuance
must not ask a pricing component to select a new price or silently recalculate a
committed unit price from current settings. Arithmetic validation uses the accepted
`decimal(19,4)` rules: round each applicable line amount to four decimals with
`MidpointRounding.ToEven`, then sum the rounded line totals.

The cardinality/allocation rule between one Order and invoices is **not yet
accepted**. Until the partial/multiple-invoicing decision below is closed, no
schema uniqueness rule, line-allocation model, "already invoiced" outcome or
remaining-balance behavior may be invented.

Quotation identity is not an issue prerequisite. Fulfillment is an independent
effect and is neither checked nor created by this contract.

## Numbering ownership and uniqueness

Invoices owns invoice identity and business-reference allocation. HTTP handlers,
Orders, Customers and document/PDF rendering must not mint invoice numbers.

The accepted uniqueness requirements are:

- an internal invoice identity is unique within its tenant and remains stable;
- a second organization-scoped business reference or sequence exists and is
  retained with the issued invoice;
- allocation is committed atomically with the invoice and its idempotency receipt,
  so an exact replay returns the same identity/reference and does not consume a
  second reference.

The exact reference/sequence rule is still blocking. In particular, the repository
has not decided the scope anchor for an individual debtor or an Order with no
organization attribution, whether the value is a sequence or another reference,
or the allocation/gap behavior under failed/concurrent issuance. Printable syntax
and jurisdiction-specific fiscal numbering remain outside this bounded slice even
after the internal business-reference rule is settled.

## Business date and authoritative timestamp

`issuedAt` is an authoritative server UTC timestamp recorded by the invoice write
transaction and replayed unchanged. It is not caller supplied and is not the
invoice business date.

The invoice business date is a separate retained fact. The repository intentionally
has not yet chosen whether it is supplied by an authorized operator or derived
from a tenant/business timezone, nor any backdating/future-date bounds. That exact
choice is required before implementation; client locale, browser clock and server
UTC date must not be silently treated as the business rule.

## Concurrency, caller-scoped idempotency and recovery

Issue idempotency follows the current mutation pattern: scope is tenant + current
account + operation kind + normalized key. The semantic fingerprint includes every
caller-controlled business choice, including Order identity, expected revision and
debtor selection. If business date is later defined as caller-supplied, it is also
fingerprint input; if it is server-derived, the resulting date is retained output.
Generated invoice identity, business reference and server timestamp are results and
are not fingerprint input.

An exact retry after current authority succeeds returns the original retained
invoice result, including its original debtor snapshot, numbers and timestamp,
even if current customer information later changes. Reusing the key for different
semantic intent is a stable idempotency conflict.

The owning PostgreSQL adapter must commit invoice header/lines, numbering effect and
command receipt in one transaction, under explicit tenant predicates and forced
RLS, using only parameterized SQL kept in adapter-owned embedded `.sql` resources.
A failed transaction retains none of those effects. If a caller loses the response
or cancellation races with commit, retrying the same key is the recovery path.

Concurrent different-key attempts against the same Order must be serialized by the
chosen invoice/order allocation invariant, not by an optimistic pre-check in the
host. If the accepted model is one full invoice per Order, only one durable issue
may win. If partial/multiple invoices are accepted instead, allocation must prevent
concurrent over-invoicing. The repository must choose between those models before
code or constraints are written.

## Historical replay and safe failure outcomes

The issued invoice and its retained command receipt are the historical authority
for this slice. Replay/read of historical issue results must not re-resolve current
Orders/Customers to rebuild the debtor or amounts. Receipt envelopes are versioned;
supported old versions preserve their original meaning and unsupported or
inconsistent versions fail closed rather than being guessed.

The future application contract needs explicit non-success outcomes for at least:
missing/invisible source, Order not committed, Order revision conflict, unsupported
Order currency, invalid debtor-for-Order selection, idempotency-key conflict and
provider/storage unavailability. The exact invoice-allocation conflict outcome
cannot be named until the partial/multiple rule is decided. If inactive individuals
become ineligible, that condition also needs a stable non-disclosing outcome.

Failures must not disclose another tenant's existence, Customer contact payloads,
raw authorization-provider detail, SQL/provider internals, idempotency keys or
full financial line payloads in diagnostics. Resource bounds for any later ingress
must follow the existing bounded-reader pattern; no endpoint is introduced here.

## Explicit non-claims

This contract does not introduce or imply tax, VAT/fiscal compliance, receivable or
general-ledger posting, account balance, credit limit, payment allocation,
settlement, refunds/credits, invoice correction/voiding, quotation issuance,
fulfillment, stock movement, PDF/document rendering, printing, email delivery,
customer portal, consolidated statements, persistent program billing defaults,
multi-currency, FX, automatic invoice creation, SaaS subscription billing or a
generic finance framework.

It also does not claim that current Customers organization/program names or
individual contact fields will remain immutable forever; the invoice snapshot is
what protects historical issued truth if those capabilities later grow edit paths.

## Decisions required before implementation

These are business/contract decisions, not implementation details that an agent may
choose silently:

1. **Order-to-invoice allocation:** one full invoice per committed Order, or
   partial/multiple invoices. If partial/multiple is chosen, define selectable
   lines/quantities, rounding of partial quantities, cumulative allocation limits,
   completion/remainder semantics and concurrent allocation behavior.
2. **Organization-scoped business reference:** define the organization that scopes
   the second identifier when the debtor is an individual and decide whether an
   unattributed committed Order is invoiceable at all. Then choose reference versus
   sequence semantics and allocation/gap behavior. Printable/fiscal format can stay
   deferred.
3. **Invoice business date authority:** operator-supplied versus tenant-timezone
   derived, including required timezone and allowed past/future bounds.
4. **Inactive individual eligibility:** decide whether an inactive individual may
   be selected for a new invoice. Historical invoices remain valid/readable either
   way.

No durable persistence/numbering/date implementation should begin until these four choices are accepted,
because they change persistence constraints, concurrency, idempotency fingerprints
or current debtor validation.

## Features that may remain outside this bounded slice

The following do not block a minimal issue implementation once the four decisions
above are closed: printable number syntax, jurisdiction-specific tax/fiscal rules,
customer addresses/contacts on printed documents, program billing defaults,
quotation linkage, invoice correction/credit flows, receivable/payment accounting,
consolidated billing, fulfillment integration, PDF/print/delivery and multi-currency.

## Host-neutral implementation note

`Application.Invoices` now implements only the provider-neutral behavior that can be
expressed without choosing the unresolved allocation, numbering or business-date
rules: current billing-authority checking through a capability port, replay-before-
mutable-read idempotency flow, committed Order/revision/NPR validation, decimal
19,4 + `ToEven` arithmetic verification, tenant-scoped debtor resolution through
current Customers public queries, immutable issued-fact contracts and explicit safe
outcomes. The atomic issue-store port is intentionally unimplemented; its future
adapter must own invoice identity/reference/date/timestamp allocation and durable
receipt persistence under the accepted decisions. Inactive individual selection
returns `ContractDecisionRequired` rather than silently treating inactive as either
eligible or ineligible. No endpoint, PostgreSQL adapter, migration or OpenFGA SDK
integration is introduced by this slice.

Retained debtor facts must carry a `Kind` matching their concrete supported record;
contradictory or undefined kinds fail constructor validation before admission as
issued facts. Individual resolution accepts the known `Active` state and preserves
the explicit `Inactive -> ContractDecisionRequired` outcome. An unsupported
availability returned by the Customers query fails fast with a static
`InvalidOperationException`, consistent with other unsupported capability-port
statuses. It never produces an issue candidate or calls the issue store's commit
operation. Rejecting an unknown contract value does not decide inactive eligibility.

Focused correction evidence is retained under
`artifacts/verification/invoices-core/`: eight adversarial cases first failed
against the pre-fix source (zero passed, eight failed, zero skipped; exit 1), then
passed with the constructor/resolution guards. The full focused test and format
results are recorded in `correction-results.json`; these are pure orchestration
and fact-validation checks, not PostgreSQL, OpenFGA or durable invoice evidence.
The earlier combined gate precedes these corrections and does not qualify them.

## Proposed file and dependency map

The host-neutral `Application.Invoices` project and its focused unit-test project now
exist. Provider/host paths below remain proposed and are not introduced by this
slice.

```text
modules/invoices/
  Application.Invoices/
    Application.Invoices.csproj
    InvoiceIssue.cs                 # command/result, invariant validation, idempotency intent
    InvoiceSnapshot.cs              # retained issued facts, no provider/HTTP types
    dependencies:
      -> Application.Orders
      -> Application.Customers
      -> Application.Tenancy

  Application.Invoices.Postgres/
    Application.Invoices.Postgres.csproj
    Persistence/...
    Migrations/...
    Sql/IssueInvoice.sql
    Sql/FindIssueReceipt.sql
    dependencies:
      -> Application.Invoices
      -> existing EF Core/Npgsql packages already used by capability PostgreSQL adapters

modules/orders/Application.Orders/
  CommittedOrderInvoiceFacts.cs     # proposed narrow public query; committed immutable facts only

modules/customers/Application.Customers/
  InvoiceDebtorFacts.cs             # proposed narrow debtor-resolution query; no billing authority

services/core-api/Application.CoreApi/       # only when ingress is separately introduced
  -> Application.Invoices.Postgres
  -> pinned OpenFGA adapter adds the dedicated invoice-issue permission

authorization model
  infrastructure/authorization/openfga/tenant-authorization-model.json

database migrator
  services/db-migrator/Application.DatabaseMigrator/
  -> Application.Invoices.Postgres migration contribution

tests (when implementation begins)
  unit/Application.Invoices.Tests/
  integration/Application.Invoices.Postgres.Tests/
  integration/Application.CoreApi.Tests/    # only with an HTTP issue surface
```

No new external package is justified by this contract. The application project
owns financial issue meaning; Orders and Customers expose only the narrow facts
they own; the PostgreSQL adapter owns invoice persistence/number allocation and
embedded runtime SQL; CoreApi remains transport/current-provider-authorization
composition. No module reads another capability's private provider tables.

## Regression criteria for the future implementation

Implementation is not `PRODUCTION_HONEST` merely because these tests are proposed.
The eventual slice must permanently prove:

- only a committed Order at the exact expected revision can issue, and non-`NPR`
  Orders cannot be silently converted;
- the accepted allocation/cardinality rule survives same-key and different-key
  races without duplicate/over-invoiced effects;
- line arithmetic is `decimal(19,4)`, `ToEven` per line and summed rounded totals;
- debtor alternatives obey the frozen Order attribution/current Customers contract,
  with current membership + dedicated billing permission rechecked on replay;
- exact retries return byte-equivalent business facts and changed intent conflicts;
- current Customer changes do not alter issued snapshots or historical replay;
- PostgreSQL tenant isolation, forced RLS, atomic issue/receipt/numbering rollback,
  least runtime privileges and embedded SQL placement hold against PostgreSQL 17;
- unsupported/corrupt historical receipt versions fail closed;
- missing/wrong-tenant resources and provider failures produce safe diagnostics;
- architecture tests keep `Application.Invoices` free of ASP.NET, EF/Npgsql and
  OpenFGA dependencies and preserve independent executable project graphs.
