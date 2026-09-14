# Phase 6F — Integrated Phase-6 Production-Honesty Gate

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## Production intent

After Phase 6 passes, declared Platform Admin operations can execute through an independently secured/audited Admin plane and declared background work can be accepted/executed durably by Worker, with failure independence, bounded claims/retries/quarantine, and explicit recovery rather than process-memory or privileged-bypass shortcuts.

## Scope contract

Before sign-off, classify each introduced Admin/Worker/scheduler/background-consequence responsibility as `NOT_INTRODUCED`, `PRODUCTION_HONEST`, or `BLOCKED`.

A first narrow Admin command or Worker job type is acceptable. A reachable privileged path with incomplete authorization/audit or durable work accepted only in memory is `BLOCKED`.

## Gate conditions

Phase 6 passes for its declared scope when:

- Admin Web talks directly to an independent private Admin API;
- Admin authentication/device/private-network/application authorization are separate gates;
- material Admin operations have durable authoritative audit;
- highest-risk operations can require JIT/physical/recovery/four-eyes controls according to the policy actually introduced;
- Worker accepts only durably recorded work and implements bounded claim/retry/quarantine/drain behavior;
- scheduler, if introduced, creates durable occurrences/jobs rather than being truth itself;
- Core/Admin/Worker failure-independence tests pass for introduced boundaries;
- background/provider consequences use owning capability semantics and idempotency;
- no generic privileged force-success/raw-SQL/raw-counter bypass exists.

## Evidence requirement

Exercise, as applicable, Core unavailable while Admin remains functional, Admin unavailable while tenant business remains functional, unauthorized tenant/platform user, private-network evidence without application authorization, shared-state races, Worker crash before/after external effect, stale lease, duplicate/redelivered job, repeated schedule occurrence, no-progress/quarantine, noisy-tenant fairness, drain/shutdown, and recovery while an ordinary host is unavailable.

Do not use in-memory queue acceptance, scheduler memory, or audit logs alone as evidence of durable job/control correctness.

## Completion meaning

Passing Phase 6 qualifies the declared Admin/Worker process foundations as production-honest for current workloads. Later controls/job types are new scope and must meet the same bar when introduced.
