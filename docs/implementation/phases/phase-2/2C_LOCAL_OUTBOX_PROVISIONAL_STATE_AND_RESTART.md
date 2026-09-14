# Phase 2C — Local Outbox, Provisional State, and Restart Semantics

## Required local state

Introduce only the durable records required by the real slice:

- semantic `OperationId` / idempotency identity;
- capability/operation kind;
- intent payload/evidence owned by the capability/sync contract;
- provisional entity state;
- outbox/pending state;
- retry/attempt metadata only where useful;
- local acknowledgement state once Sync exists.

`System.Threading.Channels` or similar in-memory signalling may wake work but never become the durable authority for pending operations.

## Restart behavior

Prove:

- process dies after local commit;
- process dies before wakeup signal;
- sign-out with pending work;
- Workstation/Guard restart;
- duplicate wakeup;
- local retry does not duplicate local semantic intent.

## Resource bounds

Pending operations, retry loops, local logs, temp data and staging must have bounded behavior and visible storage-pressure semantics.

## Continuing capability growth

Any local-capable module may reuse the local durability pattern, but conflict/admission rules remain capability-specific.

## Exit gate

Local durable intent is reconstructable from SQLite alone after process-memory loss and cannot be silently discarded by restart/sign-out.