# Phase 8A — End-to-End Observability and Diagnostics

## Correlation path

Prove representative trace/log correlation across the paths that now exist, for example:

```text
Workstation
→ Sync/API
→ authentication/authorization
→ capability application
→ DB/outbox
→ Worker/provider
```

and Admin/API/key/recovery operations where implemented.

The underlying instrumentation should have been introduced incrementally in earlier phases. Phase 8 validates the end-to-end evidence model and its failure behavior across the current system.

## Evidence requirements

- stable EventId/EventName/FailureCode;
- correlation/causation/OperationId propagation;
- state-transition evidence;
- bounded-cardinality metrics;
- safe redaction;
- telemetry-pipeline self-health;
- Workstation bounded local evidence while offline;
- provider/collector outage behavior;
- crash/update/migration diagnostic evidence where implemented.

## Diagnostics process

Create an on-demand desktop Diagnostics process only if heavy bundle/crash/offline packaging work is now required. If created, define trigger, bounded inputs/output, encryption/redaction/upload, crash behavior and lifecycle immediately.

## Exit gate

A representative failure can be reconstructed without relying on raw secrets/customer dumps, and telemetry-provider failure cannot corrupt/roll back committed business state.