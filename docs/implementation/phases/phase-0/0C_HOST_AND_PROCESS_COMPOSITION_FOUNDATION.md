# Phase 0C — Host and Process Composition Foundation

**Purpose:** Establish sustainable executable composition/lifecycle behavior for the hosts that have actually been reintroduced, without creating future processes for symmetry.

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

## Host completeness

For every introduced executable, implement the applicable subset immediately:

- validated configuration and secret separation;
- safe startup failure;
- cancellation/graceful shutdown;
- structured lifecycle/failure evidence;
- version/component identity;
- bounded loops/retries/buffers;
- health/readiness when semantically appropriate;
- process/IPC compatibility where another process depends on it.

Do not postpone these merely because a later phase contains a deeper hardening gate.

## Accepted future examples

Architecture docs reserve directions such as Workstation, Guard, tenant Web, compact CoreApi, later WebApi/SyncApi/Worker/Admin, and optional desktop helper processes. These are examples/ownership locations until implementation earns them.

Guard, when rebuilt, remains external supervision/recovery only and cannot own business capability, central DB, authorization authority, Worker scheduling, or key custody.

## In-process communication

Ordinary modules in one executable call each other in-process through owned application/query surfaces. Do not add localhost HTTP/gRPC merely to make module boundaries look distributed.

## Exit gate

0C is complete when every executable that exists at that point:

- has explicit composition/lifecycle/failure/security boundaries;
- contains no duplicated business meaning;
- is bounded and observable for its current responsibilities;
- is independently justified as a process;
- has lifecycle/failure tests appropriate to its role;
- leaves unneeded future executables uncreated.
