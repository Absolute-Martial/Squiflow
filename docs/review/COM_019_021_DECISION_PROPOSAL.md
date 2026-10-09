# COM-019–021 decision proposal: first durable invoice issue

**Status:** Owner accepted the complete first-issue contract on 2026-10-10. This closes COM-019's business choices; it does not qualify the existing partial host-neutral implementation or introduce durable invoice issuance.

## Owner-accepted first-issue contract

The owner accepted all five recommendations below. They do not accept tax or fiscal rules, payment accounting, or a new authorization model. The focused invoice owner and decision records now carry these choices.

| Decision | Accepted first-scope rule | Boundary |
|---|---|---|
| Order allocation | One complete invoice per committed Order. Reject another issue for an Order that already has an invoice; no partial or multiple invoices. | No line/quantity allocation model is introduced. |
| Organization anchor | Require frozen organization attribution on the committed Order. Use that organization as the reference scope even when the selected debtor is its program or an individual. Reject an unattributed Order. | The selected debtor does not change the numbering anchor. |
| Identity and reference | Store a tenant-wide invoice GUID and an increasing numeric ordinal scoped to the frozen attributed organization. Gaps are allowed. | This selects an internal business reference only; no printable syntax, gapless guarantee, or fiscal numbering/compliance is accepted. |
| Business date | Derive the business date from the authoritative server issue instant in UTC and retain that instant separately. Do not accept a caller-supplied date or backdating/future-dating. | No tenant-timezone setting or caller date authority is introduced. |
| Individual debtor | A newly issued invoice may select only an active individual billing record. Reject inactive records; previously issued snapshots remain unchanged. | Availability is checked at new issue, not used to rewrite historical invoices. |

The accepted reference is a per-organization numeric ordinal alongside the tenant-wide invoice GUID. Gaps are explicitly allowed. The owner did not choose a printable number format, gapless sequence, tax/fiscal rule, or jurisdictional compliance claim.

## Accepted current contract and non-claims

The following meanings are already accepted and should be preserved:

- Issue only from a committed Order at its exact expected revision; an Order’s draft customer/program attribution is not itself debtor selection.
- The default debtor is the attributed organization. The attributed program may owe independently, or separate billing permission may select an individual billing record. Selection is per invoice and is frozen; it does not change future defaults.
- Persist currency code `NPR` (`रु` is display notation), `decimal(19,4)` values, round each line to four decimals using `ToEven`, then sum the rounded lines. Freeze source prices and selected debtor at issue.
- Tax is outside the first invoice implementation scope. That is not tax exemption, tax advice, fiscal numbering, jurisdictional compliance, or permission to omit any legally required documents.
- Corrections, credits, cancellation/reissue, receivables, payments, allocations, settlement, printable documents, and delivery are not part of COM-019–021’s invoice-issue contract.

The focused contract says exact printable syntax and fiscal requirements may remain open. The current individual billing-record contract defines identity, display name, optional email/phone, and active/inactive lifecycle; it does not make that record a sign-in account. See [business model](../domain/BUSINESS_MODEL.md), [individual billing record owner](../implementation/CUSTOMER_INDIVIDUAL_BILLING_RECORD_SLICE.md), [accepted decisions](../decisions/CURRENT_DECISIONS.md), and the [open-decision registry](../decisions/OPEN_DECISIONS.md).

## Authority, provider and safe-retry boundary

Issuance keeps the already accepted separate billing-authority requirement. The current host-neutral capability checks it through `IInvoiceIssueAuthority`; Orders, pricing, customer-view, and individual-availability permissions do not grant invoice issue authority. COM-019–021 do not select a permission name, role, relation, tuple model or provider adapter. COM-022 owns the dedicated tenant billing relation and live pinned-OpenFGA/API qualification. On every attempt, including same-key replay, the API must establish current active tenant membership and billing authority before disclosing a receipt or invoice facts. Provider failure fails closed; PostgreSQL must not infer identity from caller-selected tenant input.

COM-021 may introduce only the PostgreSQL persistence required for the accepted issue contract: tenant-unique invoice identity, organization-scoped reference allocation, immutable invoice/debtor/line snapshots, and a caller-scoped idempotency receipt, committed atomically. Use tenant predicates, forced RLS and least-privilege runtime grants; the database adapter must not recalculate prices or resolve customers by reading another module’s tables. Shared migration ordering and solution registration remain with the integrator.

Retry behavior must make the issue receipt authoritative. Same caller + same key + same normalized intent returns the original retained facts after current authorization is rechecked. Same key + changed intent conflicts. A known pre-commit failure leaves no invoice, reference allocation presented as issued, or successful receipt. If the client loses the connection during commit, report an unknown outcome and recover by re-reading the same receipt in a fresh transaction; do not claim rollback, mint a second invoice, query an aborted transaction, or promise exactly-once behavior beyond the tested local transaction. Concurrent duplicate-key and Order-allocation attempts must produce one retained issue and a stable result.

No invoice schema currently exists, so an accepted COM-021 schema is an initial additive introduction. Do not rewrite Orders, Customers, or historical facts. Any future invoice schema version change must preserve issued snapshots and receipt identity; destructive backfill, renumbering, reinterpretation of old references, or a silent switch of date/anchor policy requires a separately reviewed compatibility/migration decision. Do not claim compatibility with invoices that have never existed.

## Admitted sequence and evidence

The task boundary remains the one in [COM-019](../development-tasks/phase-02-commercial-backend/COM-019-close-the-first-invoice-contract-decisions.md), [COM-020](../development-tasks/phase-02-commercial-backend/COM-020-host-neutral-invoice-and-bill-to-invariants.md), and [COM-021](../development-tasks/phase-02-commercial-backend/COM-021-atomic-postgresql-invoice-issuance-and-numbering.md):

1. Owner selects or amends the five rules above. Update the focused invoice contract and accepted/open decision records; give concrete organization, independent-program, individual, duplicate issue, and rejected-unattributed examples. Validate arithmetic edge cases against the existing `ToEven`/precision contract.
2. Verify the existing provider-neutral implementation and change only deltas required by accepted choices (COM-020). Its tests qualify calculations, validation, authority-port orchestration and retained facts only.
3. Introduce atomic PostgreSQL issue persistence (COM-021) and actual PostgreSQL regressions for concurrent numbering/duplicate issue, full rollback, restart replay, uncertain commit recovery, tenant isolation, RLS/grants, and immutable facts. Add no issue endpoint in this task.
4. Qualify customer-facing ingress separately in [COM-022](../development-tasks/phase-02-commercial-backend/COM-022-invoice-api-authority-and-issue-qualification.md), including the current live identity/membership, billing relation and OpenFGA checks. Pure tests or HTTP doubles do not prove those provider claims.

The accepted decisions close only the business-choice prerequisite. The current host-neutral code represents a partial-invoice model and still has an `Inactive -> ContractDecisionRequired` path; it requires contract-alignment changes and is `BLOCKED` for receiving qualification under the accepted contract. Historical 25/25 pure tests and review evidence qualify only that donor partial scope, not the accepted full-order/date/reference/inactive rules. Durable invoice runtime remains `NOT_INTRODUCED`. COM-021 is PostgreSQL/application transaction work only: it adds no HTTP endpoint, receivable, payment, rendering, or delivery behavior. COM-022 separately owns invoice ingress and live authority/provider qualification.

## Next receivable and payment path

COM-019–021/022 would permit an issued-invoice path only. They do not prove a customer balance or payment and cannot satisfy the deferred COM-017 customer-paid/supplier-unpaid proof. The next required decisions and scopes are:

- [COM-023](../development-tasks/phase-02-commercial-backend/COM-023-receivable-balances-and-debtor-scoped-reads.md): decide when an issued invoice creates exactly one receivable, whether/how historical invoices are reconciled, and how a balance is derived and read consistently for the frozen debtor. Keep independently owing organization, program, and individual debt separate; a reference anchor does not pool them.
- [COM-024](../development-tasks/phase-02-commercial-backend/COM-024-manual-payment-recording-and-reconciliation.md): decide supported manual payment channels and evidence, pending/confirmed/rejected/disputed outcomes, correction/reversal authority, precision, and duplicate external-reference policy. A recorded claim is not proof of received funds.
- [COM-025](../development-tasks/phase-02-commercial-backend/COM-025-partial-allocations-unapplied-funds-and-settlement.md): decide eligible payer/debtor relationships, explicit allocation authority, rounding, and reversal after reconciliation; then prove concurrent partial allocations, unapplied remainder, and settlement without crossing independent debtor or currency boundaries.

Only after COM-023–025 are implemented and qualified can a customer payment be shown as allocated/settled against its receivable. Supplier payment remains a separate Purchasing fact; customer settlement does not imply supplier settlement. Return to COM-017 after that path qualifies and ask the owner to reselect the outsourced-production case.
