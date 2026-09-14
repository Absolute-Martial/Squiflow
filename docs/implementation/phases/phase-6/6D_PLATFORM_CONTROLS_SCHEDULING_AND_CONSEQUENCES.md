# Phase 6D — Platform Controls, Scheduling, and Background Consequences

## After-commit consequences

A capability that commits authoritative business state may publish a transactional outbox/durable job for notifications, document work, provider delivery, reconciliation or other real consequences.

Worker executes through the owning capability/integration adapter; it does not become a third business implementation.

## Platform controls

Admin API may expose exact, capability-safe operations such as:

- pause/drain a workload;
- retry/requeue according to policy;
- inspect/quarantine/reconcile work;
- change provider configuration;
- initiate key lifecycle/recovery operations;
- view safe health/status.

Do not expose generic `run SQL`, `force success`, `mark payment complete`, `set raw usage counter`, or arbitrary state editors.

## Scheduling boundary

If the first scheduled workload appears, preserve the model:

```text
schedule definition
→ due occurrence identity
→ durable job
→ Worker
→ owning capability
```

The scheduler decides when work should exist; durable DB state decides whether the occurrence/job exists/completed.

The exact scheduler package must follow the latest reconciled canonical decision at implementation time. If older documents disagree (for example Quartz versus a newer TickerQ direction), resolve that documentation conflict before introducing scheduler-specific code. Package choice must not change the durable occurrence/job semantics above.

## Exit gate

Background/platform controls manipulate explicit durable lifecycle states and cannot bypass owning capability invariants.