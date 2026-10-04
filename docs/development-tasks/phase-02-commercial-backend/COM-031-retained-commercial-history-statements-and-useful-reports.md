# COM-031 — Retained commercial history statements and useful reports

Task ID: COM-031
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-030, COM-023, OPS-013
Conditional dependencies: COM-016 when supplier payable reporting is selected; OPS-009 when retained document exports are selected
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Provide bounded attributable commercial history and debtor statements/reporting that explain issued, fulfilled, paid, credited and outstanding facts after configuration changes.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [ORDER_DRAFT_HISTORY.md](../../implementation/ORDER_DRAFT_HISTORY.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: capability-owned historical read composition, debtor statements and useful operator/accountant totals with focused tests. Include supplier payable reports when COM-016 is enabled. Exclude a data warehouse, universal general ledger, tax filing and telemetry-as-audit.

## Decisions/prerequisites

Choose first useful report filters, business-date/timezone and statement consolidation rules. Independent debtors retain separate authority. OPS-013 supplies common authoritative-audit reads only where needed; OPS-009 supplies retained exports if selected.

## Acceptance and edge cases

- History retains actor/time, pricing source/override and original issued facts.
- Statement reconciles invoice, credit, payment/allocation and unapplied amounts without double count.
- Organization summaries distinguish independently owing programs and individual debtors.
- Conditional supplier reports show received cost, payments and remaining payable independently of customer settlement.
- Pagination and export/query bounds prevent unbounded scans or personal-data leakage.
- Later price, form, contact or stage changes do not reinterpret retained facts.

## Security/static review

Review report-level permission/filter enforcement, CSV/formula encoding if exported and historical snapshot consistency. Sensitive business evidence is not copied into telemetry.

## Dynamic verification and unavailable-environment handling

Test reconciliation against authoritative effect records, exact partial corrections and simultaneous writes using actual PostgreSQL. Validate bounded no-store API reports; bytes/exports require their own qualified OPS path. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-031: Retained commercial history statements and useful reports only.
Read current owners and applicable instructions.
Inspect existing callers and tests; preserve incoming work.
Close listed decisions before dependent contracts.
Implement the smallest complete scope and focused tests.
Review security, authority, durability and concurrency.
Update focused behavior/decision documentation.
Run available checks; name exact unrun checks.
Deliver source-only ZIP and evidence handoff.
No commit/push or unrun qualification claims.
```
