# Phase 2E — Integrated Phase-2 Production-Honesty Gate

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## Production intent

After Phase 2 passes, a Workstation user can rely on the declared local-capable operation surviving the supported local crash/restart/storage paths with its local/provisional meaning intact, encrypted as required, without pretending that local acceptance is server authority or that Guard owns business truth.

## Scope contract

Before sign-off, classify every introduced local-persistence/encryption/outbox/Guard/migration responsibility as `NOT_INTRODUCED`, `PRODUCTION_HONEST`, or `BLOCKED`.

A narrow first local-capable slice is acceptable. Fake durability, plaintext sensitive local persistence, unrecoverable migration/checkpoint behavior, or a Guard shortcut that owns business state is `BLOCKED`.

## Gate conditions

Phase 2 passes for its declared scope when:

- at least one real local-capable Customer/Order or equivalent operation commits atomically to SQLite/WAL;
- the UI distinguishes local/provisional from server-authoritative state;
- local business + outbox/pending intent survive supported Workstation/Guard restart paths;
- encryption covers the real local durability surface, not only the main `.db` file;
- local key protection/recovery direction does not require continuous Admin/OpenBao connectivity during normal offline startup;
- disk-full/lock/crash behavior is tested;
- checkpoint/migration recovery is established before routine local schema evolution;
- Guard supervision is bounded and does not own business logic/keys;
- other capabilities may safely gain local state by following the same production-honest foundation.

## Evidence requirement

Exercise the actual local persistence/driver/encryption/process boundaries for the properties claimed. At minimum as applicable: abrupt termination after local commit, restart, DB lock, disk full/low space, WAL/checkpoint behavior, key/open failure, interrupted migration/checkpoint, Guard restart budget, and sign-out/restart with pending local intent.

Mocks/in-memory stores do not prove SQLite/WAL/encryption/process durability claims.

## Completion meaning

Passing Phase 2 qualifies the declared local-durability foundation as production-honest. It does not imply Sync exists yet and does not limit local development to the first slice. Pending operations may remain local until Phase 3 introduces the authoritative remote path, but they may not be described as centrally accepted before that path exists.
