# COM-017 decision proposal: outsourced printing deferred

**Status:** Owner-selected deferral, 2026-10-10. Outsourced production remains `NOT_INTRODUCED`; this decision does not accept or qualify an outsourcing contract or runtime.

## Owner-selected deferral

The owner selected **defer outsourced production until customer settlement exists**. Do not start COM-017 implementation or treat the proposed printing case below as accepted scope. First close invoice contract decisions and durable issue persistence in COM-019–021, then introduce and qualify receivables, manual payment/reconciliation, and allocation/settlement in COM-023–025. Re-select and review the outsourced case after those prerequisites qualify. The separate API qualification task COM-022 is also required before customer invoice issuance is an operational path.

The invoice work already present is host-neutral contract behavior only. Durable invoice issuance is not introduced; receivable posting, customer payments, allocation and settlement are absent. COM-019–021 alone do not establish that a customer paid, so the requested “customer paid while supplier remains unpaid” evidence requires COM-023–025 as well. Outsourced production remains `NOT_INTRODUCED` pending those prerequisites and a new owner selection.

## Proposed first case and boundary

The candidate case for possible later selection is the printing-only outsourced version of COM-014/015’s existing shape: one already committed, program-attributed Order with one catalog-priced, non-stock service line, one retained unit identity/code/precision, and no unit conversion. The supplier performs printing only. The Order’s exact committed revision and line are the source facts. Existing Purchasing remains the authority for supplier request, actual receipt and cost/payable; Fulfillment remains the authority for customer-work completion, disposition and pickup. COM-014/015 and COM-016 qualification are prerequisites; this proposal does not qualify them.

The operator may record a bounded reference, proposed at 1–512 Unicode scalar values, to externally supplied design-ready input. The reference identifies input supplied from outside the product; it stores no bytes and makes no claim that the supplier created or designed it. There is no upload, object storage, artwork approval, file validation, image conversion, generated document or COM-015/OPS-008 requirement in this first case.

An informal Purchasing request may identify the supplier, requested service quantity/unit and the exact committed Order/line/revision using COM-016’s existing ownership and permission model. A request is the tenant operator’s record of an instruction, not proof that the supplier accepted it or began work. A purchase request or receipt never changes Fulfillment progress by itself. The operator records work start only when they have a basis to say outsourced production has begun, and explicitly allocates quantities from actual received purchase receipts to the exact Order line before those quantities count as Fulfillment completion. Use retained precision and exact quantities; no conversions, duplicate allocation, over-receipt or over-completion. Keep the allocation and its Fulfillment completion evidence atomic, version-checked and idempotent; the independently recorded Purchasing receipt and payable remain their own authoritative facts.

Completion and customer pickup remain separate. An operator records each partial pickup through the existing Handoff meaning and permission; a supplier receipt or outsourced completion never automatically records customer progress or pickup.

## Independent cost, price and settlement

Purchasing retains actual supplier receipt costs and payables under COM-016. The customer resale price is independently selected and authorized through the existing Pricing/Orders rules before commitment, then frozen with the committed Order. It may be lower than the supplier cost when the applicable existing price source or authorized override permits it. Do not add a cost-plus formula, automatic markup, cost floor, or post-commit price change.

This first case can prove that a lower permitted selling price remains independent of actual supplier cost. It cannot prove customer invoice issuance, an outstanding customer balance, payment, allocation, or settlement because those runtimes are absent. It also does not claim tax/fiscal compliance, a customer deposit rule, a supplier contract, supplier-side confirmation, legal receipt, or observed customer use.

No customer deposit or supplier prepayment is a gate to starting or allocating production in this case. Purchasing payment facts remain explicit records of actual supplier payments; neither they nor an absent customer payment automatically change Fulfillment progress.

## Discrepancy and correction behavior

A partial supplier receipt records only the quantity and cost actually supplied. Unreceived quantity remains a separate remainder. If a supplier rejects the work or a real issue prevents safe continuation, the operator records the applicable existing Fulfillment `Stopped`, `Disputed` or `Defective` disposition with a reason. The operator must explicitly choose to resolve and continue, cancel only the unreceived remainder under `purchasing.request.record`, or create a new supplier request for that remainder under the same current request right. There is no automatic re-sourcing, retry, quantity transfer or deletion. Actual receipts, payables, dispositions and their history remain retained.

Corrections append linked facts and preserve originals. A cross-capability correction affecting an outsourced receipt/completion link requires current `orders.fulfillment.correct` **and** `purchasing.correct`, current initial-Owner authority, the existing recent trusted Tenant Web strong-authentication policy, and expected revisions. It must preserve quantity dependencies and any linked pickup; a correction that would invalidate completed or picked-up quantity is rejected rather than silently retracting it. No physical return or financial remedy is inferred.

## Proposed authorization and privacy boundary

All actions require current active tenant membership and current independent rights on first execution and replay. The existing Fulfillment and Purchasing permissions remain separate; no role or Owner title implies them.

| Action | Required permission(s) | Returned data |
|---|---|---|
| Create/cancel a supplier request linked to one committed Order line | `purchasing.request.record` | Purchasing command result under COM-016’s protected Purchasing contract. |
| Record actual supplier receipt/cost | `purchasing.receipt.record` | Purchasing command result under COM-016’s protected Purchasing contract. |
| Allocate received quantity to outsourced Fulfillment completion | Both `orders.fulfillment.work.record` **and** `purchasing.receipt.record` | Only the Fulfillment completion/allocation result; do not return supplier identity, receipt cost or payable details. |
| Read supplier, request, receipt, cost and payable detail/history | `purchasing.supplier.view` | Bounded tenant-scoped Purchasing projection. `orders.fulfillment.view` alone never grants this data. |
| Read Fulfillment progress, pickup and protected Fulfillment history | Existing `orders.fulfillment.view` and existing history contract | Existing Fulfillment projection; never include supplier cost/payable fields. |
| Correct linked Purchasing and Fulfillment facts | Both correction rights, plus current initial Owner and existing recent strong-authentication admission | Safe correction result with originals retained; no broadened cost read. |

This conjunction limits cross-capability work entry to an actor who can record both work and supplier receipt, while cost visibility remains separately granted by `purchasing.supplier.view`. Do not add a broad shared purchasing/Fulfillment role or return a Purchase snapshot from a Fulfillment route.

## Owner decision

The owner selected **defer COM-017 until the invoice and settlement prerequisites can support the customer-paid/supplier-unpaid proof** on 2026-10-10. This proposal remains a discovery artifact for later reselection; its detailed production, permission, correction and qualification suggestions are not accepted requirements. After COM-019–022 and COM-023–025 qualify, return to the owner with the current evidence and ask whether to select this or another bounded case.

## Qualification obligations if selected

If the owner reselects outsourced production later, its receiving task must define its accepted scope and verify, with actual PostgreSQL and the real CoreApi/OpenFGA path, linked request/source revision validation, explicit partial receipt allocation and completion, no automatic progress, concurrent/idempotent no-double-allocation, incomplete/rejected supply continuation, append-only linked correction constraints, current permissions, denial of supplier cost to ordinary Fulfillment viewers, and price/cost independence. It must repeat applicable COM-016 transaction/RLS/runtime-grant guarantees and Fulfillment invariants. The full integrated gate and independent introduced-host publications remain required.
