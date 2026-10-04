# Orchestrator reconciliation

Candidate findings from independent Luna staff are checked against current source/owners before becoming implementation work. This file does not override focused owners, qualify current gates or silently close open decisions. The dirty source snapshot is recorded in [BASELINE.json](BASELINE.json). Nine perspectives are coverage, not proof that every possible fault was found.

## Confirmed planning/status irregularities

| Priority | Observation | Root disposition | Unit to assign |
|---|---|---|---|
| P0 | ADM-003 code/focused evidence are complete on commit `6735370`, but the required independent post-fix reviewer receipt is absent; separately, the exact combined normal gate cannot complete locally because locked NuGet packages and Docker/Testcontainers are unavailable | Preserve focused evidence, obtain and retain the independent ADM-003 receipt, then require one complete uncontended normal gate on this exact source | ADM-003 / GATE-001 |
| P0 | Request-budget test cleanup awaits cancellation/pending work without a finite bound | Confirmed static harness limitation, not reproduced production timeout defect; preserve contract and incoming changes | HAR-001 bounded ADM-002 follow-up |
| P1 | README current blockers conflict with an unscoped BLOCKED=none paragraph | Confirmed static documentation inconsistency; do not declare runtime blockers closed | BAS-001 evidence/status reconciliation |
| P1 | Root instructions and identity/session summary lag focused implementation owners | Confirmed status/scope drift; do not copy erroneous reviewer citation or automatically reopen qualified narrow scope | BAS-001 / ADM-004 focused summary reconciliation |
| P1 | Folder map duplicates obsolete inventory and misses introduced ownership folders | Confirmed stale documentation; use single inventory owner, reconcile actual folder paths | BAS-001 focused doc reconciliation |
| P1 | Catalog has no assignable later Workstation/Sync/Guard/whole-product chain | Confirmed planning gap; one bounded planning prompt added here, no future runtime scaffold | FUT-001 |
| P2 | Migration-owner discovery uses snapshots only | Confirmed discovery limitation; missing actual module not established | OPS-016: define supported migration convention, add negative fixture/guard |
| P2 | Backup provider migration trigger versus deadline wording differs | Confirmed ambiguous statements; not proven mutually exclusive; focused owner decides timing | OPS-018 decision clarification before provider/drill work |

## Candidate findings deliberately not promoted to defects

- Admin Web waits tenant Web acceptance in the catalog. This matches earlier user-requested delivery order; independent runtime/build does not imply unrestricted scheduling. Preserve current dependencies. Clarify product sequencing versus true runtime dependence; change only deliberately.
- Proto.Actor is selected direction, but its current task already requires concrete serialization/supervision value. Retain workload-first admission; do not automatically add it to an unselected job.
- Current Orders detail uses RepeatableRead plus deterministic concurrency regression. A historical ReadCommitted concern does not justify reimplementing it.
- Orders receipt readers support historical formats; older binaries need separately qualified rollout/rollback. That is an explicit deployment non-claim, not an established current replay bug. Map to OPS-015/020/021.
- Most commercial/admin/operations gaps are already represented. Preserve completed narrow scopes, assign missing deltas only and keep runtime absence distinct from BLOCKED introduced claims.

## Required implementation tracks already covered

1. Identity/session topology; current role/delegation/revocation; discoverable registries; operator/device recovery.
2. Real catalog/pricing and customer policy; optional quotation; guided approval and current commitment.
3. Actual partial work; organization/program/individual durable invoice; receivables/payments/corrections/history.
4. Selected purchasing/outsourcing/inventory/credit/provider branches with explicit disposition.
5. Durable consequences, files/documents, business audit, metering, notification and in-product continuation.
6. Actual deployment, least privilege, restore, load, release and API compatibility qualification.
7. Actual tenant/Admin browser journeys and later offline product boundaries.

See [SEQUENCE.md](SEQUENCE.md), [ALL_CATALOG_UNITS.md](ALL_CATALOG_UNITS.md) and [END_TO_END_CHECKS.md](END_TO_END_CHECKS.md). No unit is marked accepted by this list. Unavailable live environments leave exact evidence pending; static checks and unit coverage cannot substitute.
