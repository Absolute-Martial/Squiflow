# Phase 6C — Durable Worker Job Lifecycle

## Create Worker only for a real workload

The durable truth remains PostgreSQL/outbox/job state. In-memory channels/actor mailboxes may coordinate execution but cannot be the only accepted-work record.

Worker-related job contracts/outbox records may exist earlier. If a real workload requires the Worker executable before the planned Phase 6 sequence, pull this entire durability/lifecycle foundation forward rather than creating fire-and-forget execution.

## Required lifecycle

Use the smallest states needed by real jobs, covering concepts such as:

```text
Pending
Claimed
Running
Completed
RetryScheduled
Failed
Quarantined
Cancelled
OutcomeUnknown where external effect can be ambiguous
```

## Required semantics

- semantic job identity/idempotency;
- tenant/workload ownership;
- claim/lease/fencing where competing executors require it;
- bounded concurrency;
- finite classified retry;
- no-progress detection;
- poison/quarantine path;
- graceful pause/drain/shutdown;
- fairness/aging so priority does not create accidental permanent starvation;
- compatibility/version metadata for jobs that survive deployment.

## Runtime framework

Any selected runtime (including Proto.Actor if still canonical) is an execution mechanism under these durable semantics, not the source of truth.

## Failure injection

Crash before effect, during effect, after effect before completion persistence, stale lease, duplicate claim/delivery, process restart, DB unavailable, poison work.

## Exit gate

A Worker crash cannot make accepted durable work disappear or blindly repeat an ambiguous external effect.