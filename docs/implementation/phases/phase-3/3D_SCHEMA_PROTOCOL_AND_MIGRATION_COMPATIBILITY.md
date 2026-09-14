# Phase 3D — Schema, Protocol, and Migration Compatibility

## Version overlap

Once real old/new clients and durable state exist, make compatibility explicit rather than implicit.

Keep separate version domains for central DB migration, Workstation SQLite schema, REST contract, Sync protocol/OperationEnvelope and other durable contracts that actually exist.

## Evolution rule

Use:

```text
EXPAND
→ OVERLAP/TRANSITION
→ MIGRATE/BACKFILL
→ CUTOVER
→ CONTRACT
```

Do not remove a DB/API/Sync shape while supported old Workstations, old backend instances, pending operations, idempotency results or durable state still depend on it.

## Resolver responsibility

Normalize a supported old boundary contract into the current application contract before current business logic where practical. Do not create a mandatory runtime schema-registry service merely for versioning.

## Tests

- old Workstation → new server;
- supported old protocol → current server;
- unsupported protocol rejects explicitly without partial durable mutation;
- old/new readers against expanded schema;
- interrupted backfill/restart;
- rollback/roll-forward after partial migration;
- historical compatibility fixtures retained in source control.

## Exit gate

SquiFlow has proven one real non-empty-data evolution path and no longer depends on all clients/processes upgrading simultaneously.