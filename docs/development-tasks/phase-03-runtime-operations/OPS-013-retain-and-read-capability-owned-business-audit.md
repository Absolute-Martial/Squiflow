# OPS-013 — Retain and read capability-owned business audit

Task ID: OPS-013
Phase: 03-runtime-operations
Status: VERIFY_EXISTING
Model: GPT-6 Luna (high)
Dependencies: GATE-001, COM-001
Release requirement: REQUIRED
Cross-track prerequisites: COM-001

The owner selected the Fulfillment pickup profile through the [accepted proposal](../../review/OPS_013_DECISION_PROPOSAL.md). Its [focused audit owner](../../implementation/BUSINESS_AUDIT_PICKUP_PROFILE.md) defines the complete contract. The selected profile is locally `PRODUCTION_HONEST`, `BLOCKED = none`, under [the exact qualification receipt](../../review/COM_016_OPS_013_IMPLEMENTATION_RECEIPT.md). `VERIFY_EXISTING` is planning assignment metadata, not runtime qualification. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Verify durable, authorized pickup evidence for one successful operator-recorded handoff in the accepted single-line, committed, program-attributed, catalog-priced in-house non-stock Order case. Reuse Fulfillment’s event journal, activity links, command receipts and protected history; add no second ledger or audit service.

## Current basis and canonical inputs

Read the [accepted OPS-013 proposal](../../review/OPS_013_DECISION_PROPOSAL.md), [pickup audit profile](../../implementation/BUSINESS_AUDIT_PICKUP_PROFILE.md), [Fulfillment owner](../../implementation/ORDER_SERVICE_FULFILLMENT.md), and the [COM-014/015 receiving evidence](../../review/COM_014_015_IMPLEMENTATION_RECEIPT.md). Also read `README.IMPLEMENTATION.md`, [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [observability contract](../../observability/OBSERVABILITY_IMPLEMENTATION_CONTRACT.md), [security baseline](../../security/APPLICATION_SECURITY_BASELINE.md), and [persistence selection](../../data/PERSISTENCE_SELECTION.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: existing Fulfillment/PostgreSQL event and receipt behavior, CoreApi protected history, runtime grants, migrations and focused tests/evidence for this one pickup profile. Verify event/receipt atomicity, append-only enforcement, replay, tenant-scoped bounded history and independence from telemetry. Exclude other commercial transitions, broad audit projections, ADM-023 administrative reads, a universal event store, new audit service, automatic retention/cleanup and legal-forensics or tamper-evident claims.

## Decisions/prerequisites

The owner accepted the Handoff event as sufficient evidence, indefinite retention without runtime maintenance, appended corrections, current active membership plus `orders.fulfillment.view`, and the existing protected history/privacy boundary. The profile claims a recorded business effect only; it does not prove physical delivery, customer signature/acceptance, payment or legal receipt. Preserve the separate OPS-013 required release route; do not broaden the selected profile.

## Acceptance and edge cases

- The retained pickup identifies its tenant, Order/line and frozen Order source revision, actor, Handoff action, accepted effect, event/fulfillment revision, authorization revision, server UTC time and event/receipt identity.
- A required event, activity or receipt write failure rolls back the pickup effect; successful same-key replay returns the retained result without another fact.
- The restricted runtime role is denied event/receipt `UPDATE` and `DELETE`; database-owner/schema migration access is not runtime maintenance authority.
- Current active membership and `orders.fulfillment.view` protect read-only tenant history; bounded revision-ordered pages remain stable when newer events are appended.
- With logging providers removed and no OTLP exporter, the real PostgreSQL/OpenFGA/CoreApi journey writes pickup, restarts and reads the same retained history.
- Existing Fulfillment recipient/reason privacy scope remains intact; operational telemetry/logs contain no such values, credentials or raw bodies.

## Security/static review

Trace current permission and membership on history reads and idempotent replay. Inspect actual PostgreSQL transaction, RLS, runtime privileges, receipt/event consistency guards, bounded pagination, redacted errors and telemetry separation. Retained evidence is authoritative PostgreSQL state; it is not unrestricted forensic proof.

## Dynamic verification and unavailable-environment handling

Run exact PostgreSQL failure rollback, direct restricted-role mutation denial, same-key recovery/replay, bounded history, access-denial and real PostgreSQL/OpenFGA/CoreApi restart evidence with logging providers removed. Inspected PostgreSQL/native counts and the final integrated checks are in [TASK_STATUS](../TASK_STATUS.md). Then run the exact normal `./eng/verify.sh` and independent CoreApi/DbMigrator Release publications on one integrated source; inspect full output, totals, failures and skips. Focused passes do not qualify OPS-013. Missing or failed required evidence leaves the introduced responsibility **BLOCKED**.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Report changed files, exact commands/results, source hashes, static-review findings, claim/owner, recurring regression guard, requalification trigger, non-claims and blockers. Preserve unrelated work. Do not commit or push.

## Assignable prompt

```text
Verify only the accepted OPS-013 pickup profile on integrated source.
Read the proposal, audit-profile owner, Fulfillment owner and shared rules.
Inspect existing PostgreSQL evidence, receipts, grants and CoreApi history.
Preserve the declared pickup case, privacy boundary and non-claims.
Prove write-failure rollback and restricted-role UPDATE/DELETE denial.
Verify bounded history, access checks, replay and restart durability.
Run with logging providers removed and no OTLP exporter.
Run exact ./eng/verify.sh and independent CoreApi/DbMigrator publications.
Inspect full command output, totals, failures and skips.
Keep OPS-013 BLOCKED until every required integrated check passes.
Return evidence, guards and requalification triggers; do not commit or push.
```
