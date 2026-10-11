# COM-023–025 — Receivables, manual customer payments and settlement proposal

**Product version:** v0.0.1  
**Status:** owner accepted the proposed contract on 2026-10-10; no receivable/payment runtime introduced  
**Prerequisite:** qualified COM-020–022 invoice issuance and historical reads

The owner deferred COM-017 outsourcing until customer settlement exists. This
proposal selects the smallest customer settlement case needed before returning to
that decision. It does not select an outsourced production case.

## Proposed first contract

### Receivable recognition and historical invoices — COM-023

Every new issued invoice creates exactly one receivable in the same central
transaction as invoice facts, its organization ordinal and the issue receipt.
The receivable retains the invoice identity, exact frozen debtor kind/identity,
`NPR`, original invoice total and original UTC issue date. A zero-total invoice
has a zero receivable and is already settled; no fabricated payment is needed.

On introducing this responsibility, a one-time deployment migration posts one
matching receivable for every valid invoice already retained by the invoice
owner. Existing facts are validated before posting, invalid history aborts the
migration, and unique invoice linkage prevents duplicate recognition. Retain the
original invoice date and separately record the recognition instant. Normal
reads and retries never silently backfill or repair financial history.

Outstanding amount is original recognized amount minus active allocations.
Credits, write-offs, interest, aging buckets, credit limits and general-ledger
posting remain outside this first case. An organization reference anchor never
pools independently owing program or individual debt. Inactive customer records
remain valid historical debtor identities for payment and debt reads.

### Manual received-money evidence — COM-024

Select cash and bank receipts only. Customer amounts use the existing invoice
`decimal(19,4)` precision, are positive, bounded and explicitly `NPR`; inputs with
more than four fractional places are rejected rather than silently rounded.

A payment identifies the exact debtor whose money is being received. That debtor
is an organization, independent program or individual, explicitly selected by the
operator; it is never inferred from the operator's login. A representative can
physically hand over money, but this slice does not store third-party payer
relationships or transfer money between debtors.

Recording stores the amount, channel, a required bounded receipt/evidence note,
recording actor/time and caller-scoped semantic receipt. A bank receipt also
requires a bounded external reference. Bank reference uniqueness is per tenant
and channel after trimming, with case retained; a duplicate conflicts even for
another debtor. Cash does not require an external reference. This selects no
bank account registry, bank-provider verification or document attachment.

New payments are `Pending` until a different currently authorized reconciler
records actual received-funds evidence and confirms them. Confirmation,
rejection and dispute are attributable immutable events. A recorder cannot
self-confirm, even if they also have reconciliation authority. A pending payment
may be confirmed, rejected or marked disputed. A confirmed payment may be marked
disputed; that blocks new allocations while preserving existing allocations and
balances until explicitly reversed. A disputed payment can be confirmed again
with new evidence or reversed after all allocations are released. Rejected and
reversed payments are terminal; a new actual receipt uses a new payment record.

A full reversal requires current initial-Owner status, separate correction
permission, recent trusted Tenant Web strong authentication and a reason. It
records correction of the received-money evidence; it never asserts that a refund
was physically sent. A confirmed/disputed payment can be reversed only when it
has no active allocations. Original payment amounts, references and evidence are
never overwritten. A mistaken amount is fully reversed and replaced by a new
record. Partial monetary reversal and customer refunds remain outside this case.

### Explicit partial allocation and settlement — COM-025

Only confirmed payments can be allocated. An operator with independent allocation
authority chooses one receivable and a positive exact amount per command. A
payment may cover several invoices through separate commands, but every target
must have the same tenant, frozen debtor kind/identity and `NPR` currency as the
payment. There is no organization-wide pooling, automatic oldest-debt allocation
or cross-debtor transfer. A payment can be recorded before any invoice; its
confirmed remainder remains visibly unapplied.

Active allocations cannot exceed either confirmed payment funds or the target
receivable's outstanding amount. Locking/constraints arbitrate concurrent
attempts; allocation, its semantic receipt and consistent projections commit
atomically. No rounding occurs during allocation. A receivable is settled exactly
when outstanding amount is zero; full customer settlement does not settle a
supplier payable or change Order/fulfillment facts.

An authorized correction releases an entire exact prior allocation with a linked
immutable reversal event, reason, current initial-Owner authority and recent
trusted strong authentication. Partial reallocation releases the original and
creates a new explicit allocation; it never edits the old amount. Released money
becomes unapplied, the receivable becomes outstanding again, and original
payment/allocation history remains explainable. Payment reversal follows only
after all its allocations have been released.

## Independent current authority and resource boundaries

Select separate ordinary tenant permissions for financial viewing, payment
recording, reconciliation, allocation and correction. Initial-Owner status alone
grants none of them. Current membership and the relevant permission are checked
on first execution, replay and read; unavailable authority fails closed.
Correction additionally requires the current initial Owner and recent trusted
strong authentication described above. Financial reads disclose no customer
email/phone or raw bank-provider payload.

Use bounded protected detail/list/history and debtor-balance reads, with stable
resource-typed cursors and one consistent database snapshot. Reads do not mutate,
reconcile, confirm or allocate. Commands retain current actor/time, expected
revision where mutable state is observed, semantic idempotency and explicit safe
conflict/unavailable/unknown outcomes. PostgreSQL must prove atomicity, tenant
isolation, immutable history, races, restart recovery and authority revocation.

## Concrete examples

- An organization invoice of NPR 1,000 accepts an explicit confirmed NPR 400
  allocation and shows NPR 600 outstanding. A confirmed payment of NPR 500 then
  allocates NPR 400, leaving NPR 100 unapplied.
- An independent program invoice cannot receive the parent organization's funds
  under this contract, even though both share the invoice-reference organization.
- An individual's historical invoice remains payable after their customer record
  becomes inactive; their frozen identity still scopes payment and allocation.
- A pending bank entry is not available money and settles nothing. A disputed
  confirmed entry permits no new allocation and does not silently undo prior ones.
- Correcting a wrongly allocated NPR 400 first releases that exact allocation;
  the original invoice becomes outstanding again and payment funds are unapplied.

## Decision and qualification boundary

The owner accepted recognition/backfill, precision, receipt channels, reference
uniqueness, independent reconciliation, debtor eligibility and correction/allocation
rules through the exact proposed-contract choice on 2026-10-10. These choices
unblock COM-023–025 implementation after their invoice prerequisites qualify;
COM-020–022 verification continues independently.

Acceptance closes only these business choices. It does not qualify receivables,
payments, settlement, outsourcing, refunds, tax/fiscal compliance, provider
integration, general ledger, attachments, automatic collections or a complete
production product. The product version remains v0.0.1.
