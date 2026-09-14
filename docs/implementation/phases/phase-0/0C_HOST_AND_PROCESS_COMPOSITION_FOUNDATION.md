# Phase 0C — Host and Process Composition Foundation

**Purpose:** Establish sustainable executable composition/lifecycle behavior for the hosts that have actually been reintroduced, without creating future processes for symmetry.

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Production intent

After 0C passes, every executable that actually exists can be started, stopped, cancelled, failed, observed, and composed without turning process/framework code into business authority or relying on an unjustified process split.

## Starting point after reset

There are currently no product executables. Workstation, Guard, tenant Web, and compact CoreApi remain accepted architecture directions/ownership locations, not current runtime facts.

## Host rule

When a host is introduced, its composition root may own framework/platform setup, configuration, DI, lifecycle, transport/presentation, and host-specific effects. It must not become the owner of capability business meaning merely because requests/UI/process lifecycle enter there.

```text
Executable composition root
→ host/platform concerns
→ capability application/business surface
→ owned provider adapters when applicable
```

## Process creation rule

A new executable must answer:

1. What lifecycle/fault/security/resource/deployment boundary requires a process?
2. Why is an in-process boundary insufficient?
3. What state survives process death, if any?
4. What startup/shutdown/cancellation/health/recovery behavior is required now?
5. What compatibility/authentication contract exists across the process boundary?
6. How is the process observed and bounded?

If those answers do not justify isolation, keep the responsibility in-process.

## Host production-honesty bar

For every introduced executable, implement the applicable subset immediately:

- validated configuration and secret separation;
- safe startup failure;
- cancellation/graceful shutdown;
- structured lifecycle/failure evidence;
- version/component identity;
- bounded loops/retries/buffers;
- health/readiness when semantically appropriate;
- process/IPC compatibility where another process depends on it;
- explicit state/durability ownership across process death;
- startup/shutdown ordering where another component depends on it.

Do not postpone these merely because a later phase contains deeper qualification.

## Accepted future examples

Architecture docs reserve directions such as Workstation, Guard, tenant Web, compact CoreApi, later WebApi/SyncApi/Worker/Admin, and optional desktop helper processes. These are examples/ownership locations until implementation earns them.

Guard, when rebuilt, remains external supervision/recovery only and cannot own business capability, central DB, authorization authority, Worker scheduling, or key custody.

## In-process communication

Ordinary modules in one executable call each other in-process through owned application/query surfaces. Do not add localhost HTTP/gRPC merely to make module boundaries look distributed.

## Evidence and permanent regression protection

For each executable introduced:

- configuration/startup validation tests run `PER_MR`;
- cancellation/graceful-shutdown and bounded-loop behavior run `PER_MR` where deterministic;
- architecture tests permanently prevent capability/business ownership leaking into the host/composition root;
- process termination/restart/crash-loop/version/IPC scenarios run `PER_MR` where cheap and `SCHEDULED`/`PRE_RELEASE` where true process orchestration is needed;
- lifecycle event/failure evidence is asserted without secrets/PII;
- any health/readiness endpoint is tested against its actual semantic contract rather than merely returning 200.

A host that has only ever been launched successfully is not lifecycle-qualified.

## Transitional contract

A host may be introduced before every future provider/process/dependency exists, but unavailable dependencies must remain explicit. Do not return fake success, fake health, or placeholder authority merely to keep the process launchable.

If another process begins depending on the host before IPC/version/authentication semantics are qualified, that dependency is `BLOCKED`; either keep the processes independent or pull the required boundary forward.

## Exit gate

0C passes when every executable that exists at that point:

- has explicit composition/lifecycle/failure/security boundaries;
- contains no duplicated business meaning;
- is bounded and observable for its declared responsibilities;
- is independently justified as a process;
- has named lifecycle/failure evidence plus permanent/recurring regression checks;
- leaves unneeded future executables uncreated;
- has no known reachable lifecycle shortcut deferred as later hardening.
