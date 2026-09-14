# Phase 2B — SQLite/WAL, Encryption, and Atomic Durability

## Required persistence qualification

Qualify the selected SQLite/.NET stack using the real slice:

- connection/transaction ownership;
- WAL/checkpoint behavior;
- busy/locked contention;
- abrupt termination/restart;
- disk-full/low-space behavior;
- integrity/corruption handling;
- schema migration foundation;
- Windows packaging/lifecycle behavior.

## Encryption

Local business persistence must be encrypted at rest across the actual durability surface:

```text
main DB
WAL/journal
SHM/temp where material
migration/checkpoint copies
local outbox/provisional state
sensitive staging
```

Qualify the exact encryption provider rather than hard-coding provider types into capability code.

Use a device-specific DEK, locally protected through the selected Windows mechanism, with centrally recoverable wrapped representation according to the OpenBao/key architecture when that prerequisite is available.

## Atomicity

The local business change and the semantic outbox/pending operation that represents it must commit atomically when both are required by the slice.

## Exit gate

An unauthorized copy of the protected local store/checkpoint cannot be opened without required key material, and supported abrupt failure cannot silently lose a reported-success local commit.