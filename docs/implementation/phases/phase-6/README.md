# Phase 6 — Independent Platform Admin and Durable Worker

## Purpose

Phase 6 introduces two new justified process/security boundaries only when their first real workloads exist:

```text
apps/admin-web
services/admin-api
services/worker
```

They are not generic infrastructure shells. Each must be complete enough for its current responsibility from its first operational use.

The phase does not prohibit Admin/security/Worker-related contracts, audit concepts, outbox records, or provider-control concepts from existing earlier where real work needs them. Phase 6 is the point where the **independent runtime boundaries themselves** become operational and therefore acquire their full lifecycle/security/recovery obligations.

## Subphases

```text
6A  Private Platform Admin control plane and Admin API independence
6B  Admin authorization, device/JIT/high-risk audit controls
6C  Durable Worker job lifecycle and execution
6D  Platform controls, scheduling boundary and background consequences
6E  Failure independence and recovery
6F  Integrated Phase-6 gate
```

## Continuing development

All earlier capabilities, Workstation, Web, Sync/API, persistence, security, configuration, observability and recovery tracks continue. New background/control workloads may be added only when their owning capability and durable authority are explicit.

If a real requirement needs Admin or Worker earlier than the planned phase, pull the relevant subphase foundation forward with its full obligations instead of introducing an unsafe temporary process.