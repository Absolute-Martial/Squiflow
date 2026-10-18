# Customer receivables, payments and settlement

**Product version:** v0.0.1  
**Status:** owner-accepted contract on 2026-10-10; COM-023–025 runtime is introduced and `BLOCKED` receiving integrated qualification  
**Prerequisite:** COM-020–022 invoice issuance and historical reads qualify before this contract is activated  
**Focused owner:** invoice-backed customer receivables, manual customer payments, reconciliation, allocation and settlement

The exact first-case business choices are owned by the accepted
[COM-023–025 proposal](../review/COM_023_025_DECISION_PROPOSAL.md). This document
is the implementation owner for those choices. The accepted bounded runtime is
implemented under the existing Invoices capability and is receiving qualification
together with its invoice/authorization prerequisites. This is not a production
deployment or whole-product qualification.

The proposal closes the selected business choices. Do not reopen them in code.
API paths, stable permission identifiers, and routine resource limits belong to
the relevant host/security owners and existing repository patterns; they do not
change the business meaning below.

## Recognition and balances

Each newly issued invoice creates exactly one receivable atomically with the
invoice facts, organization ordinal and issue receipt in the central PostgreSQL
transaction. The receivable links the invoice identity and retains its frozen
debtor kind and identity, `NPR`, original invoice total, original UTC invoice
date, and a separate server recognition instant. The selected debtor determines
the debt identity; the organization used to scope the invoice ordinal does not
pool debt. Organization, program and individual receivables remain independent.

Before enabling issuance with receivables, a one-time deployment migration posts
one matching receivable for every valid invoice already retained. It validates
all source history before posting and aborts if any source invoice is invalid.
Unique invoice linkage prevents duplicate recognition. Backfilled receivables
retain the original invoice date and record the migration's recognition instant.
Normal reads, retries and runtime startup do not backfill, repair or reinterpret
financial history.

An invoice total of zero produces one zero receivable, already settled, without
a fabricated payment. For positive invoices, outstanding balance is the
original recognized amount minus active allocations. A receivable is settled
exactly when that amount is zero. Inactive organization, program or individual
customer records remain valid frozen debtor identities for historical balance,
payment and allocation reads.

No credit, write-off, interest, aging bucket, consolidated balance across
independent debtors, account balance or general-ledger posting is introduced.

## Manual payment evidence and reconciliation

The first supported manual receipt channels are cash and bank, in `NPR`. Amounts
are positive and bounded at `decimal(19,4)` precision. Inputs with more than four
fractional places are rejected; recording does not round them. A payment names
the exact organization, program or individual debtor whose money is received.
The operator's login never supplies debtor identity. Third-party payer
relationships and transfers between debtors are outside this contract.

Recording retains the amount, channel, required bounded receipt/evidence note,
recording account and server UTC time, plus a caller-scoped semantic receipt. A
bank receipt also requires a bounded external reference. Bank references are
unique per tenant and channel after trimming, with case preserved; a duplicate
conflicts even when it names another debtor. Cash has no external-reference
requirement. This does not introduce bank-account setup, provider verification
or document attachments.

A newly recorded payment is `Pending`; it contributes no available funds and
settles no debt. A different currently authorized reconciler must record actual
received-funds evidence to confirm it. The recording account cannot confirm its
own payment, even when that account also has reconciliation permission. A
pending payment may be confirmed, rejected or marked disputed. A confirmed
payment may be marked disputed. A disputed payment blocks new allocations while
preserving existing allocations and balances; it may be confirmed again only
with new evidence, or reversed after every allocation has been released.
Rejected and reversed payments are terminal. A new actual receipt creates a
new payment record.

Reversal is a full correction of the received-money evidence and requires
current initial-Owner status, a separate correction permission, recent trusted
Tenant Web strong authentication, and a reason. A confirmed or disputed payment
can be reversed only with no active allocations. It does not claim that money
was physically refunded. A mistaken amount is fully reversed and replaced with
a new payment. Partial monetary reversal and customer refunds are not introduced.

Payment amounts, bank references, evidence and transition facts are immutable.
Confirmation, rejection, dispute and reversal append attributable facts; they do
not overwrite the original receipt. A read projection may describe current
state, but retained history remains sufficient to explain each transition.

## Explicit allocation and settlement

Only confirmed payments may be allocated. An operator with independent
allocation authority chooses one receivable and one positive exact amount per
command. Payment and receivable must have the same tenant, frozen debtor kind
and identity, and `NPR` currency. The same debtor means the exact same business
identity: an organization's funds cannot settle its independently owing
program's or individual's invoices. No automatic oldest-debt application or
cross-debtor transfer is introduced.

An allocation may be partial. A payment may cover more than one invoice through
separate explicit commands, and may be recorded before an invoice exists. Its
confirmed unallocated remainder remains visible as unapplied money. Active
allocations cannot exceed confirmed payment funds or the receivable's current
outstanding amount. Allocation uses exact `decimal(19,4)` values without
rounding. PostgreSQL locking and constraints arbitrate competing commands; the
allocation, semantic receipt and balance effects commit atomically.

An authorized correction releases one entire prior allocation through a linked
immutable reversal event with a reason. It requires current initial-Owner
status, the separate correction permission and recent trusted Tenant Web strong
authentication. The original allocation amount is never edited or partially
released. Reallocation releases the old allocation and records a new explicit
one. Released funds become unapplied and the receivable becomes outstanding by
the released amount. Payment reversal is permitted only after all allocations
are released. Customer settlement has no effect on supplier payables, Orders,
fulfillment or other business facts.

## Authority, commands and reads

Financial viewing, payment recording, reconciliation, allocation and correction
use separate ordinary tenant permissions. Initial-Owner status, tenant
membership alone, invoice issue/view rights, Orders, Customers, Purchasing,
Fulfillment or Platform Admin authority imply none of those ordinary rights.
Every command, replay and read requires current active membership and its
specific current permission; authority-provider unavailability fails closed.
Correction additionally checks the current initial Owner and recent trusted
strong-authentication context. Reconciliation verifies that its actor differs
from the payment recorder.

Commands retain the current actor, authoritative server time, normalized
semantic intent and caller-scoped receipt. Use expected revisions when a
transition depends on mutable current state. Same-key/same-intent retries return
the original retained result only after fresh current authorization; changed
intent conflicts. A known pre-commit failure leaves no partial financial effect.
If the commit result is uncertain, do not claim rollback or success: recovery
uses the same caller and key in a fresh transaction. Errors and diagnostics do
not disclose raw provider detail, bank/evidence payloads, financial line data,
keys or another tenant's existence.

Provide bounded protected detail, list, history and debtor-balance reads with
stable resource-typed cursors. Each response that combines financial effects
uses one consistent database snapshot. Reads are tenant-filtered, check
authority even for empty results, and never reconcile, confirm, allocate, repair
or otherwise mutate state. They expose no customer email/phone or raw bank or
provider payload. A cached or paginated balance is explanatory data, not
credit-admission authority; this contract introduces no credit decision.

## Implemented runtime and receiving qualification

The implementation extends `Application.Invoices` and its existing PostgreSQL
adapter. It adds no executable, database, shared repository abstraction or
separate business engine. `20261015120000_CustomerSettlement` validates retained
invoice history and creates invoice-backed receivables, immutable original
payments, append-only events and caller-scoped financial receipts in the existing
`invoices` schema. The invoice issue transaction inserts its matching receivable.
The deployment migration cannot be rolled back by deleting financial history.

The provider locks the caller/key, payment and then affected receivable. It checks
current authority after waits and before commit, including before receipt replay.
Projection SQL combines each financial response in one statement snapshot.
Payment events and financial receipts commit together; deferred guards reject
incomplete history. Forced RLS and runtime SELECT/INSERT grants apply to the four
new tables; runtime UPDATE/DELETE/TRUNCATE/trigger authority is rejected. Views
run with invoker security. The runtime never repairs history on a read.

Routine bounds are 1,000 characters for evidence/reasons, 200 for bank references,
128 printable ASCII characters for caller keys, 50 records per read page, and
resource-/tenant-/filter-bound cursors of at most 160 characters. Each payment
permits 5,000 ordinary events within a 10,000-event retained limit, reserving
capacity to release every allocation and reverse the receipt. This does not
introduce a monetary rounding or partial-release rule. HTTP JSON is at most
16 KiB and depth six; unknown/duplicate members and numeric strings are rejected.

CoreApi tenant-relative routes are:

| Journey | Routes |
|---|---|
| Financial reads | `GET receivables`, `GET receivables/{invoiceId}`, `GET customer-payments`, `GET customer-payments/{paymentId}`, `GET customer-payments/{paymentId}/history`, `GET debtors/{kind}/{debtorId}/balance` |
| Original receipt | `PUT customer-payments/{paymentId}` |
| Reconciliation | `POST customer-payments/{paymentId}/confirm`, `/reject`, `/dispute` |
| Allocation | `POST customer-payments/{paymentId}/allocations` |
| Correction | `POST customer-payments/{paymentId}/reverse`, `POST customer-payments/{paymentId}/allocations/{allocationId}/release` |

Every mutation requires `Idempotency-Key`. Replies contain only payment/event
identities and the saved revision; viewing has its own authority. A replay returns
that original receipt, even when later events changed the current payment.
The existing invoice issue/read routes keep their separate permissions.

The five independent permission IDs are `receivables.view`, `payments.record`,
`payments.reconcile`, `payments.allocate` and `payments.correct`. Their relations
are included in the repository's pinned-model source. Deployment must publish,
verify and deliberately pin the compatible model before grants/activation; no
runtime re-pin, default grant or member/Owner shortcut is introduced. The
reconciliation execution protocol and unresolved-write quarantine are owned by
[Tenant authorization administration](TENANT_AUTHORIZATION_ADMINISTRATION.md).
Local model tests do not qualify a target store/model rollout or live identity.

The bounded Web Billing journey is owned by
[Web commercial workspace](WEB_COMMERCIAL_WORKSPACE.md). It initiates these
operations and displays receipts/frozen facts. It has no financial authority or
arithmetic engine. Web correction verification asks the configured identity
provider for fresh strong authentication; CoreApi still independently validates
the actual signed token, current Owner and correction right.

The receiving evidence is in
[the application completion receipt](../review/APPLICATION_COMPLETION_2026_10_16.md).
It separates focused PostgreSQL/HTTP/UI checks from the final normal gate,
independent publication, independent review and live-provider/target checks.
Until those local admission checks are complete this introduced scope remains
`BLOCKED`; passing focused tests alone does not qualify it.

## Evidence required before qualification

The claim is trustworthy only with falsifiable evidence against the real
PostgreSQL and CoreApi boundaries. Pure tests or provider doubles do not prove
database, OpenFGA, membership, recent-authentication or HTTP claims.

- **Recognition/backfill:** prove one receivable per newly issued invoice in the
  same transaction and one-time backfill of every valid retained invoice.
  Invalid or inconsistent source history aborts without partial backfill;
  repeated deployment does not duplicate recognition. Verify frozen debtor,
  amount, currency, invoice date and separate recognition time, including zero
  totals and inactive historical identities.
- **Money and identity:** prove NPR-only positive exact four-decimal payment and
  allocation amounts, rejection of excess scale/range, and exact debtor-kind,
  debtor-ID and currency matching for organization, program and individual
  cases.
- **Payment lifecycle:** prove pending funds are unavailable, another current
  account can confirm with evidence, self-confirmation is denied, and rejection,
  dispute, reconfirmation and full-reversal transitions retain immutable
  attributable history. Race duplicate trimmed bank references across different
  debtors and prove uniqueness is tenant/channel scoped with case preserved.
- **Allocations and corrections:** with actual concurrent PostgreSQL commands,
  prove payment funds and receivable outstanding cannot be over-allocated.
  Cover partial allocation, multiple explicit invoices, unapplied remainder,
  disputed-payment freeze without balance rewrite, linked full release,
  reallocation, reversal after release, denial of partial release and payment
  reversal while any allocation remains active.
- **Transactions and recovery:** inject failures at each retained write and
  prove the whole financial change, linked event/projection and receipt roll
  back. Observe a real COMMIT response loss after server commit, report unknown,
  and recover the same immutable result from a fresh connection using the exact
  key. Prove restart replay and changed-intent conflict.
- **Authority and API:** through CoreApi and the configured pinned OpenFGA
  model, prove each permission is independent, membership is current, recorder
  cannot self-confirm, only the initial Owner plus correction right and recent
  trusted Tenant Web strong authentication can correct, and revocation while
  waiting for locks prevents effects or replay disclosure. Outages fail closed.
  Exercise strict bounded ingress, safe errors, response privacy, and every
  command/read/replay surface.
- **Reads and database security:** prove bounded stable pagination, tenant
  filtering including empty counts, one-snapshot balance consistency under
  concurrent allocation/release, query-only behavior, forced RLS, least-privilege
  runtime grants, immutable source/events/receipts and fail-closed unsupported
  or corrupt history.
- **Gate and recurrence:** run the exact normal `./eng/verify.sh` repository
  gate on the final integrated source. Keep these provider and API regressions
  in recurring verification; requalify after schema/migration, amount or debtor
  semantics, transaction/lock boundaries, permission or strong-auth policy,
  cursor/read changes, or a change to retained history/version rules.

The supplier-service contract at
[PURCHASING_SUPPLIER_SERVICE_SLICE.md](PURCHASING_SUPPLIER_SERVICE_SLICE.md)
is a mechanism and test-pattern reference for append-only corrections, current
authority, forced RLS, idempotency and native evidence. Its supplier balances,
two-decimal precision, actual refunds and permissions do not transfer to
customer receivables or payments.

## Explicit non-claims

This owner does not introduce payment providers, bank-account verification,
third-party payer identity, cross-debtor transfers, partial monetary reversal,
customer refunds, credits, write-offs, interest, aging policy, credit limits,
general ledger, mixed currency, FX, tax or fiscal compliance, automatic
collections, statement aggregation, attachments, receipts as legal proof,
supplier settlement, refund delivery, payment assurance beyond recorded
evidence, or a complete production product. The payment record proves only the
bounded manual evidence and reconciliation result selected here; it does not
prove an external bank settlement or legal sufficiency.

No unresolved business-semantic contradiction was found in the accepted
COM-023–025 contract. Later variants listed above require their own owner
decision before implementation.
