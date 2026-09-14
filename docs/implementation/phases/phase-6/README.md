# Phase 6 — Independent Platform Admin and Durable Worker

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

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
