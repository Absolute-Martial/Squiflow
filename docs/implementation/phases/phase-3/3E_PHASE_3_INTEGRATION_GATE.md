# Phase 3E — Integrated Phase-3 Production-Honesty Gate

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## Production intent

After Phase 3 passes, the server can authoritatively admit and persist the declared business operations while supported Workstations synchronize them through an explicit durable/versioned protocol, with tenant isolation, current authority, semantic idempotency, invariant-specific concurrency, and recoverable response-loss/duplicate behavior.

## Scope contract

Before sign-off, classify every introduced PostgreSQL/API/Sync/idempotency/concurrency/schema/protocol responsibility as `NOT_INTRODUCED`, `PRODUCTION_HONEST`, or `BLOCKED`.

A first narrow synchronized capability is acceptable. A real sync/API path that silently reinterprets unsupported versions, duplicates semantic effects, leaks tenant state, or depends on unqualified central persistence is `BLOCKED`.

## Gate conditions

Phase 3 passes for its declared scope when:

- PostgreSQL central authority is qualified on the real workload and target deployment class;
- tenant isolation/RLS/pool hygiene are tested;
- authoritative admission rechecks current authorization/facts/invariants;
- semantic idempotency and per-invariant concurrency are proven;
- Workstation pending operations synchronize through a bounded durable protocol;
- response loss, duplicate delivery and partial batch failure recover correctly;
- download/cursor application is durable;
- old/new schema/protocol overlap has at least one real tested evolution;
- unsupported versions fail explicitly;
- no API/Sync host owns a separate copy of capability business meaning.

## Evidence requirement

Use the real PostgreSQL/provider and real host/protocol layer for claims that depend on them. Evidence should include, as applicable, response lost after commit, duplicate same-intent request, same idempotency key with changed intent, cross-tenant object/list/write attempts, pooled-connection tenant-context/RLS hygiene, concurrent expected-version conflict, partial batch failure, retry amplification, old supported protocol, unsupported protocol, migration overlap, query/index/cardinality behavior, WAL/checkpoint/resource effects, and recovery after DB/process interruption.

A green API happy-path test does not prove tenant isolation, idempotency, concurrency, compatibility, or DB operational behavior unless those properties are actually exercised.

## Completion meaning

Passing Phase 3 qualifies the declared authoritative-persistence/sync foundation as production-honest. It unlocks deeper conflict/long-offline recovery work but does not restrict other capabilities from continuing to evolve under these guarantees.
