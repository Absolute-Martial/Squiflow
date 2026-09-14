# Phase 2E — Integrated Phase-2 Gate

Phase 2 is complete enough when:

- at least one real local-capable Customer/Order operation commits atomically to SQLite/WAL;
- the UI distinguishes local/provisional from server-authoritative state;
- local business + outbox/pending intent survive supported Workstation/Guard restart paths;
- encryption covers the real local durability surface, not only the main `.db` file;
- local key protection/recovery direction does not require continuous Admin/OpenBao connectivity during normal offline startup;
- disk-full/lock/crash behavior is tested;
- checkpoint/migration recovery is established before routine local schema evolution;
- Guard supervision is bounded and does not own business logic/keys;
- other capabilities may safely gain local state by following the same foundation.

Passing Phase 2 qualifies a reusable local-durability foundation. It does not imply Sync exists yet and does not limit local development to the first Customer/Order slice. Pending operations may remain local until Phase 3 provides the authoritative remote path.