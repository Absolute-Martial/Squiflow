# Workstation Update, Migration, Backup, and Recovery Ownership

**Status:** Accepted implementation direction

## 1. Principle

`SquiFlow.Guard` owns **coordination, safety gates, and recovery decisions**. It does not absorb the heavy implementation of backups, schema migrations, diagnostics, synchronization, or document work.

```text
Guard
  ├─ decides whether update/migration can proceed
  ├─ requires a verified recovery checkpoint
  ├─ coordinates process shutdown/startup
  ├─ evaluates post-update health gates
  └─ selects rollback/recovery when required

Capability workers
  ├─ Backup/Maintenance → create and verify checkpoint
  ├─ Migration         → apply schema/data migration
  ├─ Diagnostics       → collect/package/upload evidence
  └─ Updater           → stage/replace binaries
```

## 2. Safe update state machine

```text
Update available
      ↓
verify package/signature/version
      ↓
preflight
  ├─ disk reserve
  ├─ supported current version/schema
  ├─ no unresolved recovery
  ├─ required processes can stop safely
  └─ checkpoint capability available
      ↓
enter update/maintenance state
      ↓
create recovery checkpoint
      ↓
VERIFY checkpoint
      ↓
stage new binaries
      ↓
stop affected processes
      ↓
install/switch binaries
      ↓
run migration if required
      ↓
start target version
      ↓
health gates
      ├─ process alive
      ├─ IPC/heartbeat available
      ├─ local DB opens
      ├─ schema compatible
      ├─ rule/config snapshot compatible
      └─ basic readiness self-test
      ↓
   healthy? ── yes ──► commit update
      │
      no
      ▼
rollback/recovery
```

An executable starting is not sufficient proof that an update succeeded.

## 3. Recovery checkpoint

Before a migration capable of changing local durable state, create a checkpoint containing enough information to recover the Workstation coherently.

Conceptual contents:

```text
RecoveryCheckpoint/
├── manifest.json
├── database/
│   └── workstation.sqlite.backup
├── payloads/
│   └── required unsynchronized local payloads
├── configuration/
│   └── recoverable configuration snapshot
├── rules/
│   └── active rule/config version references
├── update/
│   ├── source application version
│   ├── source schema version
│   └── target migration/version
└── integrity/
    └── hashes/signature metadata
```

The manifest should record at least:
- CheckpointId;
- creation time;
- source application version;
- database schema version;
- intended target version/migration;
- device/workstation identity where appropriate;
- database/payload hashes;
- pending outbox count;
- pending durable job count where applicable;
- sync cursor/checkpoint metadata;
- backup format version.

A file merely existing is not a verified backup. Verification must prove the checkpoint is readable and internally consistent enough for the supported restore path.

## 4. Backup ownership

Guard does **not** take a direct dependency on SQLite/ORM backup implementation just to create checkpoints.

```text
Guard
  │ StartCheckpoint request
  ▼
Backup/Maintenance capability
  ├─ establish supported consistency boundary
  ├─ create SQLite backup/snapshot
  ├─ capture required local payloads/metadata
  ├─ build manifest
  ├─ hash/verify
  └─ return CheckpointResult
  │
  ▼
Guard records verified checkpoint and permits migration
```

This keeps Guard small and prevents database/application dependencies from accumulating in the watchdog process.

## 5. Backup classes

Treat backups according to purpose rather than one retention rule:

| Class | Purpose | Typical retention intent |
|---|---|---|
| Update checkpoint | Immediate binary/update rollback | Short |
| Migration checkpoint | Protect schema/data transformation | Until migration stability is proven |
| Disaster/user backup | Recover data after loss/corruption | Policy controlled/durable |

Disaster backups may be promoted to configured durable object storage. Update checkpoints are primarily local recovery artifacts unless policy requires otherwise.

## 6. Migration journal

Migrations must leave durable progress/recovery evidence.

Conceptual record:

```text
MigrationId
SourceSchemaVersion
TargetSchemaVersion
ApplicationVersion
CheckpointId
StartedAt
CompletedAt
CurrentStep
LastSuccessfulStep
RollbackSupported
Result
FailureCode
```

After power loss or process crash, Guard checks this state before starting normal Workstation operation. An incomplete migration enters recovery/resume/rollback handling rather than blindly launching the normal UI.

## 7. Rollback

Until the target version passes its health/stability gate, preserve:
- previous runnable binary/version or supported installer rollback asset;
- verified recovery checkpoint;
- previous compatible configuration where required;
- migration journal;
- diagnostic evidence.

Guard may coordinate rollback only according to an explicit compatibility plan. It must never guess how to mutate business data backward.

If a migration is intentionally non-reversible, the release/update plan must define forward-recovery behavior before rollout.

## 8. Diagnostics and logging during update/recovery

Update, backup, migration, and rollback events use the shared Serilog/OpenTelemetry vocabulary and should carry identifiers that let support reconstruct the lifecycle:

```text
UpdateId
MigrationId
CheckpointId
OperationId
CorrelationId
EventName
FailureCode
```

Examples:

```text
GUARD.UPDATE.STARTED
GUARD.CHECKPOINT.REQUESTED
BACKUP.CHECKPOINT.VERIFIED
MIGRATION.STARTED
MIGRATION.FAILED
GUARD.ROLLBACK.STARTED
GUARD.ROLLBACK.COMPLETED
```

Large diagnostic artifacts use the Diagnostics upload path rather than OTLP logs.

## 9. Failure rules

Test at minimum:
- power loss while checkpoint is being created;
- checkpoint creation succeeds but verification fails;
- power loss after checkpoint and before binary replacement;
- binary replacement succeeds but migration fails;
- migration succeeds but new Workstation cannot reach readiness;
- rollback binary starts against the supported restored schema/data;
- disk becomes low/full during staging;
- telemetry/collector unavailable throughout update;
- unsynchronized outbox/payloads survive failed update;
- repeated failed target version becomes quarantined rather than retried forever.

## 10. Non-goals for Guard

Guard does not own:
- SQL migration implementation;
- arbitrary business-data repair;
- business synchronization semantics;
- object-storage provider logic;
- telemetry provider credentials;
- backup retention policy beyond enforcing the recovery/update contract;
- document/report generation.

Guard is the local lifecycle safety coordinator, not a second application runtime.
