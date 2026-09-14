# SquiFlow Detailed Implementation Phase Packages

**Status:** Detailed implementation-planning structure  
**High-level roadmap:** `docs/implementation/PHASES_AND_GATES.md`

## Purpose

This directory breaks the high-level roadmap into cumulative, reviewable phase/subphase packages.

A phase is a **minimum architectural maturity envelope**, not a permit list and not a declaration that named components are finished forever.

```text
phase
= required foundation
+ existing components continuing to evolve
+ additional work allowed safely
+ material boundaries that remain earned-only
+ future sustainability constraints
+ security/failure/recovery/compatibility obligations
+ verification and integration gate
```

It is not:

```text
phase
= only these folders may change
= every named component is complete forever
= every future mechanism must be implemented immediately
```

## Cumulative development model

All later phases inherit earlier gates. At the same time, product development continues across multiple tracks:

```text
Capabilities       Customers / Orders / Quotes / Inventory / Payments / ...
Workstation        shell / local app / SQLite / sync UX / devices / printing / ...
Web                tenant UI / settings / reports / later portal / ...
Server/API         interactive ingress / sync ingress / authoritative applications / ...
Data               PostgreSQL / SQLite / object storage / migrations / backups / ...
Security           identity / authorization / encryption / Admin zero trust / ...
Background         outbox / jobs / Worker / scheduling / integrations / ...
Observability      logs / traces / metrics / diagnostics / authoritative audit links / ...
Recovery           process / migration / resnapshot / restore / release recovery / ...
Compatibility      DB / REST / Sync / SQLite / durable work / IPC / provider migration / ...
Operations         deploy / rack / capacity / private recovery / release / ...
```

A later headline phase usually unlocks a **behavioral maturity**, not the first date a capability/component is allowed to exist.

## Introduced-responsibility rule

Deferring an unneeded boundary is healthy. Deferring correctness after introducing a boundary is not.

```text
GOOD
Worker does not exist yet because no durable workload needs it.

BAD
Worker exists, but durable recovery/retry/idempotency/quarantine are postponed.
```

The same rule applies to Sync, Admin API, SQLite durability, encryption, provider effects, compatibility and every other material runtime boundary.

## Status vocabulary

```text
Seed                concepts/contracts or early adapter exists
Active              current development is extending it
Operational         real runtime responsibility exists
Qualified           current responsibility passed its owning gate
Expandable          expected to continue growing
ProductionQualified accepted for paying-customer use under current profile
```

`Qualified` never means `finished forever`.

## Detailed package index

### Phase 0 — Architectural development foundation

`phase-0/`

```text
README.md
0A_ARCHITECTURE_BASELINE_RECONCILIATION.md
0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md
0C_HOST_AND_PROCESS_COMPOSITION_FOUNDATION.md
0D_ENGINEERING_SAFETY_OBSERVABILITY_AND_REPRODUCIBILITY.md
0E_ACTIVE_CAPABILITY_AND_TRACK_DEVELOPMENT.md
0F_PHASE_0_INTEGRATION_GATE.md
```

### Phase 1 — Identity, tenant authorization, and sessions

`phase-1/`

```text
README.md
1A_ZITADEL_IDENTITY_AND_ACCOUNT_BINDING.md
1B_TENANT_AUTHORIZATION_ROLES_AND_DEVICES.md
1C_WEB_WORKSTATION_SESSION_AND_SURFACE_SECURITY.md
1D_AUTHORIZATION_CHANGE_RECONCILIATION_AND_FAILURE.md
1E_PHASE_1_INTEGRATION_GATE.md
```

### Phase 2 — Local-first Workstation durability and Guard recovery

`phase-2/`

```text
README.md
2A_FIRST_REAL_LOCAL_CAPABLE_SLICE.md
2B_SQLITE_WAL_ENCRYPTION_AND_ATOMIC_DURABILITY.md
2C_LOCAL_OUTBOX_PROVISIONAL_STATE_AND_RESTART.md
2D_GUARD_UPDATE_MIGRATION_AND_RECOVERY_COORDINATION.md
2E_PHASE_2_INTEGRATION_GATE.md
```

### Phase 3 — Authoritative persistence and synchronization

`phase-3/`

```text
README.md
3A_POSTGRESQL_AUTHORITY_AND_TENANT_ISOLATION.md
3B_AUTHORITATIVE_ADMISSION_IDEMPOTENCY_AND_CONCURRENCY.md
3C_SYNC_UPLOAD_DOWNLOAD_CURSOR_AND_BACKPRESSURE.md
3D_SCHEMA_PROTOCOL_AND_MIGRATION_COMPATIBILITY.md
3E_PHASE_3_INTEGRATION_GATE.md
```

### Phase 4 — Conflict, long-offline recovery, and rebase

`phase-4/`

```text
README.md
4A_LONG_OFFLINE_AND_COMPATIBILITY_ASSESSMENT.md
4B_CONFLICT_RECONCILIATION_AND_ADJUSTMENT.md
4C_RESNAPSHOT_REBASE_AND_PENDING_INTENT_PRESERVATION.md
4D_AUTHORITY_CONFIGURATION_AND_LIMIT_REFRESH.md
4E_PHASE_4_INTEGRATION_GATE.md
```

### Phase 5 — Versioned rules, workflow, and dynamic forms

`phase-5/`

```text
README.md
5A_RULES_FACT_AUTHORITY_AND_DECISION_TRACE.md
5B_VERSIONED_WORKFLOW_AND_CONTINUATION.md
5C_DYNAMIC_FORMS_AND_SAFE_PUBLICATION.md
5D_WORKSTATION_SNAPSHOTS_AND_CROSS_VERSION_EXECUTION.md
5E_PHASE_5_INTEGRATION_GATE.md
```

### Phase 6 — Independent Platform Admin and durable Worker

`phase-6/`

```text
README.md
6A_PRIVATE_ADMIN_CONTROL_PLANE_AND_API_INDEPENDENCE.md
6B_ADMIN_AUTHORIZATION_DEVICE_JIT_AND_AUDIT.md
6C_DURABLE_WORKER_JOB_LIFECYCLE.md
6D_PLATFORM_CONTROLS_SCHEDULING_AND_CONSEQUENCES.md
6E_FAILURE_INDEPENDENCE_AND_RECOVERY.md
6F_PHASE_6_INTEGRATION_GATE.md
```

### Phase 7 — Files, documents, printing, and backup/restore

`phase-7/`

```text
README.md
7A_OBJECT_STORAGE_OWNERSHIP_INTEGRITY_AND_PROVIDER.md
7B_ATTACHMENT_TRANSFER_CAPACITY_AND_CONSUMPTION.md
7C_DOCUMENT_GENERATION_PROCESS_ISOLATION_AND_PRINTING.md
7D_BACKUP_TARGET_RECOVERY_SET_AND_RESTORE.md
7E_PHASE_7_INTEGRATION_GATE.md
```

### Phase 8 — Cross-system security/performance/network/observability qualification

`phase-8/`

```text
README.md
8A_END_TO_END_OBSERVABILITY_AND_DIAGNOSTICS.md
8B_API_BROWSER_SECURITY_AND_ADMISSION_LIMITS.md
8C_PERFORMANCE_CACHE_NETWORK_AND_DEPENDENCY_BUDGETS.md
8D_DEPLOYMENT_RUNTIME_AND_RELEASE_HARDENING.md
8E_PHASE_8_INTEGRATION_GATE.md
```

### Phase 9 — Payments, credit, inventory, and protected authority

`phase-9/`

```text
README.md
9A_PAYMENTS_AND_PROVIDER_EFFECT_IDEMPOTENCY.md
9B_OUTCOME_UNKNOWN_REFUNDS_REVERSALS_AND_RECONCILIATION.md
9C_INVENTORY_CONCURRENCY_AND_CORRECTION.md
9D_CREDIT_PRICING_AND_HISTORICAL_FINANCIAL_TRUTH.md
9E_PHASE_9_INTEGRATION_GATE.md
```

### Phase 10 — Paying-customer production qualification

`phase-10/`

```text
README.md
10A_RACK_CAPACITY_SPOF_AND_RESOURCE_ENVELOPE.md
10B_REPLACEMENT_ENVIRONMENT_RESTORE_AND_SECURITY_RECOVERY.md
10C_RELEASE_MIGRATION_ROLLBACK_AND_ROLLFORWARD_DRILL.md
10D_PROVIDER_MIGRATION_LIMITS_AND_OPERATOR_OWNERSHIP.md
10E_PAYING_CUSTOMER_PRODUCTION_GATE.md
```

## Carry-forward rule

Every intentionally deferred material item should record:

```text
Item
Reason deferred
Owner document
Current preservation constraint
Trigger for implementation/revisit
Latest required closing gate
Current test/check preventing accidental violation
```

This is stronger than an unowned `TODO`.

## Pull-forward rule

If a real requirement needs a later foundation earlier than planned:

```text
real requirement appears
→ identify owning architecture responsibility
→ pull the required subphase/gate forward explicitly
→ implement it correctly
→ update roadmap/decision record
→ then use it
```

Do not create a temporary unsafe shortcut simply because the original phase number is later.

## Package evolution

The subphase structure itself is expandable. If a parent phase becomes too broad, add another subphase file rather than turning one file into an unreadable monolith or silently moving responsibilities elsewhere.