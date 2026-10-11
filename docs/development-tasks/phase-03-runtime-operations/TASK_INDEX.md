# Phase 03-runtime-operations task index

Generated from full task metadata. Read the complete file and accepted dependency handoffs before dispatch.

| Task | Status | Model | Release | Dependencies |
|---|---|---|---|---|
| [OPS-001 — Select the first durable workload and authority contract](OPS-001-select-the-first-durable-workload-and-authority-contract.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | GATE-001, COM-001 |
| [OPS-002 — Persist one capability consequence and transactional outbox](OPS-002-persist-one-capability-consequence-and-transactional-outbox.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | OPS-001 |
| [OPS-003 — Implement Worker claims fencing and crash recovery](OPS-003-implement-worker-claims-fencing-and-crash-recovery.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | OPS-002 |
| [OPS-004 — Bound fair local execution with the selected actor runtime](OPS-004-bound-fair-local-execution-with-the-selected-actor-runtime.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | OPS-003 |
| [OPS-005 — Materialize durable scheduled occurrences with Quartz](OPS-005-materialize-durable-scheduled-occurrences-with-quartz.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | OPS-003, OPS-004 |
| [OPS-006 — Authorize privileged pause drain retry and quarantine controls](OPS-006-authorize-privileged-pause-drain-retry-and-quarantine-controls.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | OPS-003, ADM-002, ADM-014 |
| [OPS-007 — Qualify the owned object store adapter and migration boundary](OPS-007-qualify-the-owned-object-store-adapter-and-migration-boundary.md) | VERIFY_EXISTING | GPT-6.1 Sol | REQUIRED | GATE-001, COM-001 |
| [OPS-008 — Protect streamed uploads metadata and quarantine](OPS-008-protect-streamed-uploads-metadata-and-quarantine.md) | VERIFY_EXISTING | GPT-6.1 Sol | CONDITIONAL | OPS-007, OPS-011 |
| [OPS-009 — Render immutable document facts and retryable artifacts](OPS-009-render-immutable-document-facts-and-retryable-artifacts.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | OPS-003, OPS-004, OPS-007, OPS-011, COM-022 |
| [OPS-010 — Deliver one notification with bounded retries and in-app continuation](OPS-010-deliver-one-notification-with-bounded-retries-and-in-app-continuation.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | OPS-003, OPS-004 |
| [OPS-011 — Reserve and reconcile the first durable usage meter](OPS-011-reserve-and-reconcile-the-first-durable-usage-meter.md) | VERIFY_EXISTING | GPT-6.1 Sol | REQUIRED | OPS-001, OPS-007 |
| [OPS-012 — Expose narrow usage reads and authorized limit controls](OPS-012-expose-narrow-usage-reads-and-authorized-limit-controls.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | OPS-011, ADM-002, ADM-019 |
| [OPS-013 — Retain and read capability-owned business audit](OPS-013-retain-and-read-capability-owned-business-audit.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | GATE-001, COM-001 |
| [OPS-014 — Qualify the bounded OTLP pipeline and actionable alerts](OPS-014-qualify-the-bounded-otlp-pipeline-and-actionable-alerts.md) | READY_AFTER_DEPENDENCIES | GPT-6 Luna (high) | REQUIRED | GATE-001 |
| [OPS-015 — Reproduce the first secure deployment profile](OPS-015-reproduce-the-first-secure-deployment-profile.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | GATE-001, ADM-004, WEB-001, UIA-001 |
| [OPS-016 — Qualify separate database roles and migration registration](OPS-016-qualify-separate-database-roles-and-migration-registration.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | GATE-001, OPS-003, COM-021, ADM-025, ADM-032 |
| [OPS-017 — Qualify independent host CI and meaningful quality gates](OPS-017-qualify-independent-host-ci-and-meaningful-quality-gates.md) | VERIFY_EXISTING | GPT-6.1 Sol | REQUIRED | GATE-001 |
| [OPS-018 — Restore the complete first-profile recovery set](OPS-018-restore-the-complete-first-profile-recovery-set.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | OPS-015, OPS-016, OPS-007, ADM-017 |
| [OPS-019 — Measure representative capacity resource bounds and faults](OPS-019-measure-representative-capacity-resource-bounds-and-faults.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | OPS-014, OPS-015, OPS-016, COM-032, OPS-009, OPS-012 |
| [OPS-020 — Drill release preflight expansion and failed-release recovery](OPS-020-drill-release-preflight-expansion-and-failed-release-recovery.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | OPS-017, OPS-018, OPS-019, OPS-021 |
| [OPS-021 — Qualify consumer API compatibility and stable contracts](OPS-021-qualify-consumer-api-compatibility-and-stable-contracts.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | COM-032, ADM-024, ADM-032 |
| [OPS-022 — Qualify parallel Testcontainers startup](OPS-022-qualify-parallel-testcontainers-startup.md) | VERIFY_EXISTING | GPT-6.1 Sol | REQUIRED | BAS-001 |

Conditional prerequisites and approved writable areas are in the full task and root tasks.json.
