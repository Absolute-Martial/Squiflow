# COM-016 — Practical purchasing receipts and supplier payables

Task ID: COM-016
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Model: GPT-6 Luna (high)
Dependencies: COM-005
Release requirement: CONDITIONAL

The owner selected COM-016's first supplier-service case through the [accepted proposal](../../review/COM_016_DECISION_PROPOSAL.md). Its [focused owner](../../implementation/PURCHASING_SUPPLIER_SERVICE_SLICE.md) defines the complete contract. The selected Purchasing scope is locally `PRODUCTION_HONEST`, `BLOCKED = none`, under [the exact qualification receipt](../../review/COM_016_OPS_013_IMPLEMENTATION_RECEIPT.md). `VERIFY_EXISTING` is planning assignment metadata, not runtime qualification. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Verify the selected tenant-owned informal supplier-service workflow: requested quantity, actual partial receipts and NPR cost/payable facts, supplier payments/refunds, and linked corrections. Optional attribution to one committed Order line remains explanatory and does not change customer pricing, receivables, inventory, or fulfillment.

## Current basis and canonical inputs

Read the accepted [decision proposal](../../review/COM_016_DECISION_PROPOSAL.md), [focused purchasing contract](../../implementation/PURCHASING_SUPPLIER_SERVICE_SLICE.md), `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [business operation end-to-end owner](../../implementation/BUSINESS_OPERATION_END_TO_END.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: the introduced Purchasing capability, PostgreSQL adapter/migrations/runtime grants, CoreApi composition, and focused test/build evidence needed to qualify this exact case. Verify tenant-owned supplier identity, informal requests, partial receipts/cancellation, exact costs/payables, payments/refunds/corrections, current authority and bounded protected reads. Exclude formal purchase approval, multi-line purchasing, tracked inventory (COM-018), outsourcing (COM-017), resale pricing, tax, general ledger, customer balances, provider-side supplier APIs and generic procurement infrastructure.

## Decisions/prerequisites

The owner accepted the case and its receipt-cost meaning, supplier identity minimum, correction authority and actual supplier-refund path. Reuse the owner’s exact precision, NPR amount, authorization, idempotency, concurrency, correction, limit and failure rules; do not reopen or silently vary them. Dependency COM-005 supplies the existing Catalog unit boundary. COM-016 remains a conditional release branch; do not activate COM-017 or COM-018 from this assignment.

## Acceptance and edge cases

- Phone requests work without a formal purchase order or Order attribution; linked requests reference only one committed tenant Order line and retain frozen unit and Order facts.
- Partial receipts record only actual positive quantity and exact NPR total cost; cumulative net receipt cannot exceed the request, and every receipt payable equals its recorded cost.
- Cancellation affects only the unreceived remainder; receipts and their payables remain retained.
- Cash/bank supplier payments and actual supplier refunds are attributable and cannot over-allocate or exceed their valid payable/payment balance.
- Corrections append linked facts and retain originals; recent trusted Owner authority and the separate correction permission are enforced.
- Tenant isolation, same-key replay, concurrent receipt/payment/refund/correction, bounded read/history, unsupported persisted versions, and atomic rollback are proved at real PostgreSQL/CoreApi/OpenFGA boundaries.
- Supplier amounts and private contact, descriptions, references and reasons do not leak through errors, diagnostics or unauthorized projections.

## Security/static review

Trace current membership and each independent pinned-model purchasing right on reads, first commands and replays. Inspect tenant RLS, scoped keys, parameterized SQL, runtime grants, immutable facts, stale revisions, retry/concurrency behavior and safe errors/logs. Follow source-admission and dependency boundaries in the focused owner; do not add a package or new process without an accepted source/boundary decision.

## Dynamic verification and unavailable-environment handling

Run the selected provider/API/model, native HTTP, runtime-grant, migration-registry and architecture evidence against the actual PostgreSQL/OpenFGA/CoreApi boundaries. The final provider repeat passed 40/40; rerun it when provider/contract inputs change. Then run the exact normal `./eng/verify.sh` and independent CoreApi/DbMigrator Release publications on the same integrated source; inspect full commands, totals, failures and skips. Current scoped counts and pending checks are recorded in [TASK_STATUS](../TASK_STATUS.md); focused passes do not qualify this introduced responsibility. If a required environment is unavailable or a claim fails, name it and keep the responsibility **BLOCKED**.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Report changed files, exact commands/results, source hashes, static-review findings, claim/owner, recurring regression guard, requalification trigger, non-claims and any blocker. Preserve incoming work. Do not commit or push.

## Assignable prompt

```text
Verify only COM-016 on the integrated receiving source.
Read its accepted proposal, focused owner and shared agent rules.
Inspect the existing source, migrations, grants, CoreApi path and tests.
Preserve the selected single-service purchasing case and exclusions.
Review supplier, receipt, payable, payment, refund and correction invariants.
Run real PostgreSQL/OpenFGA/CoreApi checks, including the pending repeat.
Run exact ./eng/verify.sh and independent CoreApi/DbMigrator publications.
Inspect full command output, totals, failures and skips.
Fix only scoped defects and required regression gaps.
Report blocked claims, non-claims, guards and requalification triggers.
Return a reviewable handoff; do not commit or push.
```
