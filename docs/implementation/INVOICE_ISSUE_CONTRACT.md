# Invoice issue contract

**Product version:** v0.0.1\
**Status:** owner-accepted issue contract on 2026-10-10; COM-020–022 host-neutral, PostgreSQL and CoreApi issue/read responsibilities are introduced and `BLOCKED` receiving qualification until integrated evidence passes
**Focused owner:** Invoices, limited to issuance of retained invoice facts from a committed Order

This owner defines the smallest invoice-issue boundary supported by accepted
business decisions. COM-019's choices are closed. COM-020 aligns the existing
host-neutral capability, COM-021 adds atomic PostgreSQL persistence and COM-022
adds separately protected issue and immutable-fact reads. Their integration is
receiving qualification; project or route existence is not evidence of a passed
gate. The newly introduced customer-settlement runtime is separately owned by
[CUSTOMER_RECEIVABLES_PAYMENTS_AND_SETTLEMENT.md](CUSTOMER_RECEIVABLES_PAYMENTS_AND_SETTLEMENT.md),
and Web presentation by [WEB_COMMERCIAL_WORKSPACE.md](WEB_COMMERCIAL_WORKSPACE.md).
Their existence does not qualify this invoice prerequisite.

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
- debtor selection: omitted/default organization, the committed Order's attributed
  program, or one explicitly selected active individual billing-record identity;
- one caller-scoped idempotency key;
- no caller-supplied date; the business date is derived from the UTC calendar date
  of the authoritative server issue instant.

The caller does **not** supply invoice prices, totals, currency, customer names,
issue timestamp, invoice identity or business reference. Those are authoritative
facts resolved or allocated by the owning capabilities.

A successful issue must retain an immutable invoice snapshot sufficient to explain
that issue without dereferencing mutable current state:

- tenant-wide invoice GUID;
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
- invoice business date derived from the UTC date of the authoritative issue instant;
- frozen organization identity used to scope its numeric reference ordinal;
- allocated increasing numeric ordinal for that organization (gaps are allowed).

Optional individual email/phone are intentionally not invoice facts in this
smallest slice. If a later printable/legal document needs contact or address facts,
that contract must explicitly choose and retain the exact issued values rather
than reading current Customers data during historical rendering.

Issued facts are append-only for this slice. A current customer rename/contact
change, future program/default change, authorization change or order/customer query
result must not rewrite or recompute an already-issued invoice. Correction,
credit/reissue and cancellation semantics are outside this contract.

## Debtor validation and current authority

Invoice issuance requires distinct current billing authority. Order create, view,
edit, price, commit, customer create/view and individual-availability grants are
not substitutes. The host-neutral `IInvoiceIssueAuthority` port checks that
authority on every call; COM-022 uses the separate `invoices.issue` / `invoice_issuer` /
`can_issue_invoice` authority defined by the [permission owner](../security/TENANT_PERMISSIONS.md).
Invoice viewing uses independent `invoices.view` / `invoice_viewer` /
`can_view_invoices` authority. Current initial-Owner or Platform Admin status
grants neither permission by implication. CoreApi/OpenFGA qualification remains
part of the receiving gate.

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
- individual selection must resolve the exact tenant-owned active individual
  record and freeze its source identity/revision/display name. An inactive
  individual is ineligible for a new invoice; later availability changes do not
  alter an already-issued snapshot.

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

Exactly one complete invoice may be issued per committed Order. A partial invoice
or another invoice for that Order is ineligible. Enforce this invariant at the
transaction boundary, not with a host-side pre-check. There is no line-allocation,
remainder, or multiple-invoice model in this contract.

Quotation identity is not an issue prerequisite. Fulfillment is an independent
effect and is neither checked nor created by this contract.

## Numbering ownership and uniqueness

Invoices owns invoice identity and business-reference allocation. HTTP handlers,
Orders, Customers and document/PDF rendering must not mint invoice numbers.

The accepted identifiers are:

- a tenant-wide invoice GUID that remains stable;
- an increasing numeric ordinal unique within the frozen attributed organization,
  retained as a second internal business reference;
- a required organization attribution on every invoiceable Order. That frozen
  organization scopes the ordinal even when the selected debtor is its program or
  an individual; an unattributed Order is ineligible;
- allocation is committed atomically with the invoice and its idempotency receipt,
  so an exact replay returns the same identity/reference and does not consume a
  second reference.

Gaps in organization ordinals are allowed and carry no implied invoice state. This
contract does not promise gapless numbering. The ordinal has no accepted printable
syntax and makes no jurisdictional, tax, or fiscal-numbering claim.

## Business date and authoritative timestamp

`issuedAt` is an authoritative server UTC timestamp recorded by the invoice write
transaction and replayed unchanged. It is not caller supplied and is not the
invoice business date.

The invoice business date is the UTC calendar date of `issuedAt`, the authoritative
server issue instant. Persist both facts. No tenant timezone, client locale,
browser clock, caller date, backdating, or future-dating rule participates in the
first issue contract.

## Concurrency, caller-scoped idempotency and recovery

Issue idempotency follows the current mutation pattern: scope is tenant + current
account + operation kind + normalized key. The semantic fingerprint includes every
caller-controlled business choice, including Order identity, expected revision and
debtor selection. The derived UTC business date is retained output and is not
caller intent.
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

Concurrent different-key attempts against the same Order must be serialized by
the one-full-invoice-per-Order invariant, not by an optimistic pre-check in the
host. Only one durable issue may win.

## Historical replay and safe failure outcomes

The issued invoice and its retained command receipt are the historical authority
for this slice. Replay/read of historical issue results must not re-resolve current
Orders/Customers to rebuild the debtor or amounts. Receipt envelopes are versioned;
supported old versions preserve their original meaning and unsupported or
inconsistent versions fail closed rather than being guessed.

The application contract needs explicit non-success outcomes for at least:
missing/invisible source, unattributed Order, Order not committed, Order revision
conflict, unsupported Order currency, invalid debtor-for-Order selection, inactive
individual, already-invoiced Order, idempotency-key conflict and provider/storage
unavailability. These outcomes must not disclose another tenant's data.

Failures must not disclose another tenant's existence, Customer contact payloads,
raw authorization-provider detail, SQL/provider internals, idempotency keys or
full financial line payloads in diagnostics. CoreApi ingress follows the existing bounded-reader pattern: at most 8 KiB and
JSON depth six, no unknown or duplicate properties and no caller-supplied
financial/generated facts.

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

## Owner-accepted first-issue choices, 2026-10-10

The owner accepted these five choices through COM-019:

1. **Order allocation:** one complete invoice per committed Order. A partial
   invoice or second invoice is rejected; no line allocation or remainder model is
   introduced.
2. **Organization anchor:** an invoiceable Order must carry frozen organization
   attribution. That organization scopes the reference ordinal even when the
   selected debtor is its program or an individual. An unattributed Order is
   rejected.
3. **Identity/reference:** retain a tenant-wide invoice GUID plus an increasing
   numeric ordinal scoped per attributed organization. Gaps are allowed. No
   printable syntax, gapless promise, or fiscal-numbering claim is selected.
4. **Business date:** derive the date from the UTC calendar date of the authoritative
   server issue instant; retain the full issue timestamp separately. No caller date,
   tenant timezone, backdating, or future-dating is accepted.
5. **Individual eligibility:** only an active individual billing record may be
   selected for a new invoice. Inactive records are rejected; existing issued
   snapshots are unchanged.

These accepted decisions unblock contract-alignment work, not persistence. Before
durable issuance, the host-neutral implementation must enforce these rules and
receive the relevant focused qualification. COM-021 then adds only the atomic
PostgreSQL issuance boundary; COM-022 separately adds and qualifies invoice API
authority. Tax/fiscal compliance, receivables, payments, documents, rendering and
delivery remain outside COM-019–021.

## Features that may remain outside this bounded slice

The following remain outside this bounded slice: printable number syntax,
jurisdiction-specific tax/fiscal rules,
customer addresses/contacts on printed documents, program billing defaults,
quotation linkage, invoice correction/credit flows, receivable/payment accounting,
consolidated billing, fulfillment integration, PDF/print/delivery and multi-currency.

## Receiving implementation and read boundary

The existing host-neutral orchestration preserves authority-before-replay and
replay-before-mutable-source ordering. Its accepted-contract deltas use a narrow
invoice source port and reusable invoice-owned source/debtor validation. Orders
owns the retained financial observation and validates its own policy, approval
and commercial evidence. Invoice source reading does not re-admit an already
committed quotation or select current prices. Customers owns borrowed current
debtor observations; mutable individual rows are held through issue validation.

The PostgreSQL adapter owns versioned immutable headers, typed full lines,
organization ordinal allocation and actor-scoped issue receipts. Production
composition uses the held connection/transaction for owned source and current
authority observations. It must not acquire nested pool leases while holding its
invoice transaction. Independent view-authorized reads return retained invoice
facts without resolving current Orders or Customers.

CoreApi exposes `POST /api/v1/tenants/{tenantId}/orders/{orderId}/invoice` with
expected Order revision, optional tagged debtor choice and required
`Idempotency-Key`. A new issue returns 201, exact replay 200, and only the invoice
identity; financial content is not an issue-permission side effect. Separate
view authority protects `GET /api/v1/tenants/{tenantId}/invoices/{invoiceId}` and
`GET /api/v1/tenants/{tenantId}/orders/{orderId}/invoice`. Protected success,
denial and errors remain `no-store`.

These are introduced receiving responsibilities, not qualification claims. Pure
unit tests, actual PostgreSQL and pinned OpenFGA/native API regressions, one exact
normal repository gate and independent affected-host publications are required.
The prior donor 25-case evidence remains historical and qualifies only its old
scope. COM-023–025's first settlement contract was owner accepted on 2026-10-10 under
[its focused owner](CUSTOMER_RECEIVABLES_PAYMENTS_AND_SETTLEMENT.md). Its runtime
remains absent pending qualified invoice prerequisites and its own implementation/evidence.

## Introduced file and dependency map

These are the earned boundaries under receiving qualification:

| Boundary | Owned source |
|---|---|
| Host-neutral invoice meaning | `modules/invoices/Application.Invoices/InvoiceIssue.cs`, `InvoiceReads.cs`; neutral Orders/Customers/Tenancy references |
| Invoice provider isolation | `modules/invoices/Application.Invoices.Postgres/`; existing EF Core/Npgsql packages, owned migrations and embedded SQL |
| Frozen Order observation | `modules/orders/Application.Orders/OrderFinancialSourceFacts.cs` and Orders-owned borrowed PostgreSQL reader |
| Current debtor observation | Customers-owned borrowed PostgreSQL billing reader; no financial permission meaning |
| API/provider composition | CoreApi `InvoiceRoutes`, `InvoiceSourceReader`, `InvoiceAuthority` and independent invoice authorization |
| Model and grants | Checked-in tenant OpenFGA model and `deploy/database/grant-core-api-runtime.sql` |
| Migration activation | Existing DatabaseMigrator registry, invoice contribution at order 400 |
| Recurring evidence | Existing invoice unit suite, earned invoice PostgreSQL suite, focused CoreApi and architecture suites |

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
