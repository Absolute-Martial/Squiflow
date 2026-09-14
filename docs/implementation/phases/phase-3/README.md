# Phase 3 — Authoritative Server Persistence and Synchronization

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

Phase 3 connects local durable intent to real central authority and qualifies PostgreSQL using the actual workload rather than synthetic CRUD.

This does **not** mean server persistence, API contracts, or capability application code are forbidden before Phase 3. Real modules may begin earlier. Phase 3 is the point where the authoritative persistence + synchronization path is qualified as a dependable cross-device/server foundation.

## Subphases

```text
3A  PostgreSQL authoritative persistence and tenant isolation
3B  Authoritative admission, idempotency and concurrency
3C  Sync upload/download, cursor and backpressure
3D  Version overlap, migrations and compatibility
3E  Integrated Phase-3 production-honesty gate
```

## Phase maturity added

After Phase 3 the same capability can have local/provisional Workstation execution and server-authoritative admission/commit without becoming two independent business implementations.

## Continuing development

Customers, Orders and any other real capability may gain authoritative queries/commands, local projections, Web/Workstation adapters, and compatibility fixtures as needed. The phase constrains correctness/authority; it does not restrict which business capability may be developed.

## Phase-specific evidence and regression map

- **3A — PostgreSQL/tenant isolation:** real PostgreSQL integration tests permanently cover tenant discriminator/RLS/pool hygiene, transactions, constraints, migration mechanics, and representative query/index behavior. Core isolation checks run `PER_MR`; workload/query-plan/WAL/checkpoint/autovacuum/resource measurements run `SCHEDULED`/`RELEASE_CANDIDATE` when cardinality or topology changes.
- **3B — admission/idempotency/concurrency:** `PER_MR` tests cover current authorization/fact/invariant recheck, same-intent duplicate, same key+changed intent, response loss after commit, expected-version races, unique constraints, bounded retry behavior, and one-transaction mutation/idempotency/outbox semantics where applicable.
- **3C — sync:** protocol/host integration tests permanently cover bounded batches, duplicate/redelivery, partial failure, durable cursor application, backpressure, cancellation, reconnect and response loss. Network/process interruption experiments are recurring; Workstation sync semantics remain transport-independent.
- **3D — schema/protocol compatibility:** historical fixtures/matrices and old/new reader/writer tests run `PER_MR` for supported versions. Destructive contraction is blocked until inventory/drain/rollback-or-roll-forward evidence passes `PRE_RELEASE`/release gating.
- **3E — integration:** requires the real provider/host layers for claims they own; mocks do not qualify RLS, driver, protocol, or migration behavior.

## Transitional contract

Before 3B/3C are qualified, a server endpoint or sync path may not be presented as authoritative merely because it reaches PostgreSQL. Protected mutation requires the complete current admission path for the claimed operation.

Before 3D compatibility is qualified, independently deployed clients/contracts must not be promised a supported version-overlap window. Unsupported versions fail explicitly instead of being interpreted as current.

## Operational targets and error budgets

Sync latency/backlog age, API latency, and non-safety-critical availability may receive workload-based SLOs once measured operation exists. The following remain hard invariants with no error budget:

- tenant isolation;
- semantic duplicate prevention where promised;
- protected concurrency invariants;
- explicit unsupported-version rejection;
- durability of authoritative committed state within the declared DB contract.