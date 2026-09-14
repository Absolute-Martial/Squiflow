# Phase 0C — Host and Process Composition Foundation

**Purpose:** Establish sustainable executable composition/lifecycle behavior for the hosts that have actually been reintroduced, without creating future processes for symmetry.

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

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

## Host production honesty

For every introduced executable, implement the applicable current responsibilities immediately:

- validated configuration and secret separation;
- safe startup failure;
- cancellation/graceful shutdown;
- structured lifecycle/failure evidence;
- version/component identity;
- bounded loops/retries/buffers;
- health/readiness when semantically appropriate;
- process/IPC compatibility where another real process depends on it.

Do not postpone these merely because a later roadmap direction may contain broader runtime qualification.

## Evidence permanence

Lifecycle/fault claims for an executable are derived only after that executable and its real role exist.

Once qualified, the applicable checks remain regression protection, for example process start/stop/cancellation, configuration failure, bounded restart behavior or IPC-version handling where those claims exist.

Do not write failure matrices or cadences for future Worker/Admin/Sync/helper executables that have not been introduced.

## Accepted future examples

Architecture docs reserve directions such as Workstation, Guard, tenant Web, compact CoreApi, later WebApi/SyncApi/Worker/Admin, and optional desktop helper processes. These are examples/ownership locations until implementation earns them.

Their mention here does not create a future phase gate or require a particular process/subphase decomposition.

Guard, when rebuilt, remains external supervision/recovery only and cannot own business capability, central DB, authorization authority, Worker scheduling, or key custody.

## In-process communication

Ordinary modules in one executable call each other in-process through owned application/query surfaces. Do not add localhost HTTP/gRPC merely to make module boundaries look distributed.

## Exit gate

0C is complete when every executable that actually exists at that point:

- has explicit composition/lifecycle/failure/security boundaries;
- contains no duplicated business meaning;
- is bounded and observable for its declared responsibilities;
- is independently justified as a process;
- has falsifiable lifecycle/failure evidence appropriate to its role;
- has applicable permanent/recurring regression protection;
- leaves unneeded future executables uncreated and unspeculated at evidence-map level.