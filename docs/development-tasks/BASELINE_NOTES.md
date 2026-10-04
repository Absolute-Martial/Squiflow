# Catalog baseline notes — 2026-10-03

The catalog was reconciled against `/cutiepie/Squiflow` at Git HEAD **`5a94ad107084dbe872e8556d9400414735974d43`** with existing uncommitted work. This SHA is a traceability anchor, not a complete dirty-tree identity or proof of passing checks. [README.IMPLEMENTATION.md](../../README.IMPLEMENTATION.md) owns current inventory; this file does not duplicate project counts.

## Incoming work to preserve

- AdminApi protected-route metadata, request-budget configuration/pipeline/tests and focused request-budget owner: review/qualification under ADM-002, not a duplicate deadline implementation.
- Invoice issue owner/decisions plus `modules/invoices/Application.Invoices` and `tests/unit/Application.Invoices.Tests`: contract closure under COM-019 and existing-core qualification under COM-020, not restoration or a second Invoices project.
- Modified root/module instructions, solution registration and implementation/decision records associated with that work.
- Unrelated `package-lock.json`: record/preserve it; include only when the selected source allowlist actually needs it.

An earlier draft referred to missing .NET/Docker. The catalog session observed **.NET SDK 10.0.401** and a responsive **Docker server 29.8.1**. Availability is not an executed test result. Actual current-gate execution is reported separately in [VERIFICATION_RESULT.md](VERIFICATION_RESULT.md); no historical total is borrowed as fresh evidence.

## Source checks used to reconcile tasks

Orders has direct commitment and retained facts; Customers has separate individual billing records; host-neutral Invoices source is present. Durable invoice persistence/numbering/business-date policy and HTTP runtime still need their distinct contract/store/ingress assignments. JSON validation correction is existing baseline work and is not assigned again.

When dispatching later, rerun BAS-001 on the provided source and record safe dirty-file hashes plus ZIP digest. Any newly completed task changes readiness; update the live ledger and accepted handoffs before assigning a dependent task. Hashes in TASK_HASHES.sha256 identify this planning package only.
