# Phase 6 — Independent Platform Admin and Durable Worker

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

Phase 6 introduces two new justified process/security boundaries only when their first real workloads exist:

```text
apps/admin-web
services/admin-api
services/worker
```

They are not generic infrastructure shells. From first operational use, each introduced runtime boundary must be **production-honest for its declared scope**. A narrow first Admin or Worker workload is fine; a reachable Admin/Worker path with deferred lifecycle/security/durability/recovery semantics is `BLOCKED`.

The phase does not prohibit Admin/security/Worker-related contracts, audit concepts, outbox records, or provider-control concepts from existing earlier where real work needs them. Phase 6 is the point where the **independent runtime boundaries themselves** become operational and therefore acquire their applicable lifecycle/security/recovery obligations immediately.

## Phase production intent

After Phase 6 passes, the declared Platform Admin operations and durable background workloads can be depended upon as independent runtime responsibilities: Admin authority/audit/failure independence are real, and Worker acceptance/claim/retry/quarantine/recovery semantics are durable rather than process-memory prototypes.

## Subphases

```text
6A  Private Platform Admin control plane and Admin API independence
6B  Admin authorization, device/JIT/high-risk audit controls
6C  Durable Worker job lifecycle and execution
6D  Platform controls, scheduling boundary and background consequences
6E  Failure independence and recovery
6F  Integrated Phase-6 production-honesty gate
```

## Continuing development

All earlier capabilities, Workstation, Web, Sync/API, persistence, security, configuration, observability and recovery tracks continue. New background/control workloads may be added only when their owning capability and durable authority are explicit.

If a real requirement needs Admin or Worker earlier than the planned phase, pull the relevant subphase foundation forward with its production-honesty obligations instead of introducing an unsafe temporary process.

## Phase-specific evidence and regression map

- **6A — Admin control plane/API independence:** `SECURITY_HOSTILE + PROCESS_FAILURE + INTEGRATION`. Permanent tests verify Admin Web talks directly to Admin API, tenant users cannot gain platform authority, private-network evidence alone never authorizes, Admin/Core availability is independent, and privileged routes cannot transit Core API as a hidden bypass. Cheap route/auth rules run `PER_MR`; real private-ingress/process isolation runs `SCHEDULED`/`PRE_RELEASE`.
- **6B — Admin device/JIT/four-eyes/audit:** state/policy tests run `PER_MR`; hostile tests cover revoked Admin device, expired JIT elevation, missing required approval/physical factor, replayed approval, cross-tenant support scope, and durable authoritative audit. High-risk policy changes trigger threat-model requalification.
- **6C — Worker lifecycle:** durable job state-machine/idempotency/claim/lease/retry/quarantine/drain tests run `PER_MR`. Process termination before/after external effect, stale lease, response loss, duplicate delivery and no-progress cases remain recurring `PROCESS_FAILURE` evidence.
- **6D — scheduling/consequences:** occurrence identity, duplicate firing, misfire/overlap, provider consequence idempotency and owning-capability invocation run `PER_MR` where deterministic. Scheduler/provider outage and restart scenarios run recurring integration/failure tests. Scheduler state never becomes business authority.
- **6E — failure independence:** controlled Core/Admin/Worker outage combinations and recovery paths run `SCHEDULED`/`PRE_RELEASE`, with cheap dependency/route invariants in every MR.
- **6F — integration:** no Admin/Worker claim survives sign-off without an active regression guard and `BLOCKED = none`.

## Transitional contract

Before **6A + applicable 6B** are production-honest, privileged Platform Admin operations remain disabled/unreachable; there is no temporary privileged route on Core API.

Before **6C** is production-honest, no production responsibility may treat an in-memory queue/task as accepted durable work. Earlier outbox/job records may exist as data contracts, but a Worker execution claim is `NOT_INTRODUCED` until durable execution semantics are qualified.

Before **6D** scheduling semantics are qualified, cron/timer firing may not be the sole durable evidence that a business occurrence existed.

## Hard invariants versus SLOs

Admin authorization, privileged audit integrity, durable accepted-work preservation, and semantic duplicate protection are hard invariants. Worker backlog age, job completion latency, and non-critical Admin availability may use measured SLOs/error budgets once representative operation exists.