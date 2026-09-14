# Phase 3 — Authoritative Server Persistence and Synchronization

**Status:** direction only — `NOT_INTRODUCED` as a qualified phase

## Direction

This phase is expected to qualify the first real path from local/provisional or interactive intent into central authoritative persistence and, where needed, cross-device synchronization.

Likely ownership areas include central persistence/tenant isolation, authoritative admission, retry/idempotency/concurrency, synchronization, and contract/schema overlap. Their exact decomposition must be derived from the real capability and workload when it exists.

## Known dependencies

A real capability must already expose enough domain/application semantics to define authority, and any synchronization work must have a real local producer/consumer rather than synthetic transport tests.

## Current preservation constraints

- keep business meaning host-neutral;
- keep local/provisional and server-authoritative outcomes distinct;
- keep semantic operation/version concepts transport-independent where introduced;
- do not manufacture service/network boundaries between ordinary in-process modules.

## Activation trigger

A real capability needs central authoritative commit and/or a Workstation operation needs server admission/synchronization.

## On activation

Create the detailed governance from current facts. The active work may use a `3A/3B/...` decomposition, a different split, or no lettered split at all. Evidence classes, compatibility fixtures, failure cases and regression cadences are chosen only after the actual authority/sync contract exists.

Do not restore the retired `3A–3E` files by default.

See `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md` for anticipation, not specification.