# Phase 2D — Guard, Update/Migration, and Recovery Coordination

## Guard responsibility

Guard coordinates lifecycle/recovery; it does not implement business SQL or hold long-lived business encryption keys.

Required current behavior includes:

- launch/supervise Workstation;
- heartbeat/lifecycle observation where selected;
- bounded restart/backoff;
- safe-start/safe-mode path when crash budget is exceeded;
- update/migration handoff contract;
- bounded diagnostics/resource evidence.

## Migration/checkpoint foundation

Before local schema changes become routine, establish an encrypted checkpoint/recovery journal with metadata such as:

```text
MigrationId
SourceSchemaVersion
TargetSchemaVersion
CheckpointId
CurrentStep
LastSuccessfulStep
Result
RollbackSupported
EncryptionProfile/KeyReference
Integrity hashes
```

The owning maintenance/migration capability performs the actual backup/migration work; Guard coordinates safety gates.

## Failure injection

Kill Workstation/Guard during checkpoint, migration and restart. Exercise sleep/hibernate/clock jump, low disk, version mismatch and repeated startup crash.

## Exit gate

A failed local migration/update cannot silently destroy pending durable intent, and Guard remains a small external coordinator rather than a business/recovery monolith.