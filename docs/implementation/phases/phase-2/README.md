# Phase 2 — Local-First Workstation Durability and Guard Recovery

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

Phase 2 turns the Workstation from a presentation shell into a production-honest local-capable runtime for the first real Customer/Order or equivalent slice.

This is not a rule that only Customer/Order can exist. Other capabilities may continue or gain local state when their semantics fit the same qualified local foundation. Likewise, capability/domain/UI work may exist before Phase 2; this phase is where local durable execution itself becomes qualified.

## Subphases

```text
2A  First real local-capable business slice
2B  SQLite/WAL, encryption and atomic local durability
2C  Local outbox, provisional state and restart semantics
2D  Guard/update/migration/recovery coordination
2E  Integrated Phase-2 production-honesty gate
```

## Phase maturity added

After Phase 2, an explicitly local-capable Workstation action can become durably local without a network round trip and survive supported process/restart failure, while still remaining distinct from server-authoritative acceptance.

## Continuing development

Customers, Orders, other modules, Web/API, identity/authorization, observability, deployment and testing may continue in parallel. Any capability that adopts local durability must respect the same encryption/migration/recovery/outbox foundations rather than create its own ad-hoc local store.

## Phase-specific evidence and regression map

- **2A — first local-capable slice:** deterministic/domain tests plus a real local application path prove exactly what `LocalCommitted`/`PendingRemote` means. UI/state tests permanently prevent local acceptance being relabeled as server-authoritative. `PER_MR`.
- **2B — SQLite/WAL/encryption:** real SQLite/provider/encryption integration proves atomicity, WAL/journal/shared-memory/temp behavior, locking, key/open behavior, disk-full/low-space handling, and recovery for the declared durability contract. Fast integration checks run `PER_MR`; destructive crash/storage/encryption exercises run recurring `SCHEDULED`/`PRE_RELEASE` where needed.
- **2C — outbox/provisional/restart:** `PER_MR` tests cover one-transaction local business+pending-intent persistence, duplicate/wakeup behavior, restart/reopen, sign-out/restart with pending work, and bounded retry/wakeup loops. Accepted local work may never depend only on an in-memory wakeup.
- **2D — Guard/update/migration/recovery:** real process and migration interruption tests prove restart budgets, crash-loop safe mode, update handoff/checkpoint semantics, interrupted migration recovery, and Guard not owning business/key authority. Local process cases run `PER_MR` where practical; installer/update/migration fault cases remain `SCHEDULED`/`PRE_RELEASE`.
- **2E — integration:** no local-durability claim remains qualified unless its permanent checks remain active and current.

## Transitional contract before Phase 3

Phase 2 intentionally allows locally durable work before central synchronization exists.

While Phase 3 is absent:

```text
local commit
= durable local/provisional acceptance only

server-authoritative acceptance
= NOT_INTRODUCED
```

The UI/domain status must communicate that distinction. There is no temporary fake sync, no silent network-success assumption, and no central-authority claim.

If identity/session state expires, already committed local state is preserved according to its local durability contract; whether a new protected local operation may be accepted depends on the operation's authority classification and current qualified policy.

## Operational targets

Startup/recovery/local-operation latency may later receive measured SLOs. Durability, encryption, tenant/security boundaries, and no-loss of acknowledged local intent within the declared durability contract are hard invariants and do not receive an error budget.