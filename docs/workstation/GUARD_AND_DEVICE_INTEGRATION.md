# Workstation Guard and Device Integration

**Version:** v0.0.15

This document defines the previously named but under-specified `SquiFlow.Guard` and the minimum device-integration boundary. It does not turn every peripheral into a baseline feature.

## 1. Workstation process model

Baseline process tree:

```text
SquiFlow.Workstation    main Avalonia/UI + local application runtime
SquiFlow.Guard          tiny supervision/recovery companion

on-demand helpers only when justified:
- document/PDF processing
- image/native processing
- import/export helper
- diagnostic packaging
```

Do not create a helper process for ordinary managed-code work that can run safely in the Workstation process.

## 2. Guard responsibility

`SquiFlow.Guard` is intentionally small.

It may own:
- detect Workstation process crash/hang according to a bounded heartbeat policy;
- safe restart orchestration;
- update handoff/rollback assistance;
- process-tree/resource anomaly observation;
- crash marker/evidence collection;
- launching a safe diagnostic/recovery mode;
- ensuring orphaned helper processes are cleaned up.

It does **not** own:
- business rules;
- tenant authorization;
- synchronization semantics;
- central API credentials beyond what is strictly needed for its own lifecycle role;
- ORM/database business access;
- document generation;
- printing business decisions;
- Worker scheduling;
- rule/workflow evaluation.

If Guard requires a large dependency graph, the boundary has failed.

## 3. Guard must not create restart loops

Crash recovery is bounded.

Conceptual states:

```text
Healthy
SuspectedHung
Recovering
RestartBudgetExceeded
SafeModeRequired
UpdateRecovery
```

Repeated crashes trigger backoff and a user/support-visible safe mode rather than an endless restart loop.

Do not restart while the Workstation is intentionally updating/shutting down.

## 4. Resource envelope

Guard should remain a tiny, mostly idle process. Exact MB targets are qualification measurements, not a reason to hand-optimize prematurely.

The Workstation/Guard/helpers together obey a bounded resource policy:
- no worker/cache growth merely because RAM exists;
- helpers terminate and release memory after work;
- process-tree RSS/handles/threads are observable;
- sustained runaway resource growth is diagnosed, not normalized.

## 5. Printer integration is baseline device scope

Printing is a real SquiFlow business requirement and therefore receives a stable adapter boundary.

Conceptually:

```text
PrintRequest
→ Device/Printer capability selection
→ OS/driver adapter
→ PrintAttempt
→ status/evidence
```

The business order/invoice transaction is not rolled back because a printer/spooler/driver fails.

A print attempt records enough to support:
- requested document/version;
- printer/device target;
- attempt/correlation ID;
- submitted/failed/completed-where-detectable status;
- safe failure category;
- retry/alternate-printer action.

Do not claim physical paper output is guaranteed merely because the Windows spooler accepted a job.

## 6. Printer discovery/configuration

Workstation setup may discover available OS printers, but device choice is user/tenant/workstation configuration.

Required UX:
- no printer configured;
- configured printer missing/offline;
- driver/spooler error;
- alternate printer selection;
- test print;
- default-per-document type only if a real need appears.

Printer names/driver identifiers are local device configuration, not globally authoritative business identifiers.

## 7. Other peripherals are requirement-driven

Scanner, barcode reader, cash drawer, weighing device, cutter/plotter or other hardware are **not baseline merely because retail/print software sometimes supports them**.

When a real journey requires one, add it through a capability/adapter contract and answer:
- OS/device API;
- exclusive/shared access;
- disconnect/reconnect behavior;
- permission/safety implications;
- whether operation is business truth or a retryable side effect;
- driver/native process isolation requirement;
- support/diagnostic surface.

Keyboard-wedge barcode scanners may require no custom driver integration; do not overengineer them.

## 8. Native/helper isolation

Use a helper process when a library/driver can:
- crash the runtime;
- leak native memory;
- hang without cooperative cancellation;
- require architecture-specific/native dependencies;
- consume large temporary memory.

The helper receives a narrow job contract and no broad tenant/server authority.

A helper crash leaves the durable business state intact and produces a retryable/permanent attempt result.

## 9. Local disk/staging protection

The Workstation must not fill the customer's disk silently.

Bound:
- local DB growth;
- pending attachment staging;
- temp/helper output;
- logs/diagnostics;
- update packages;
- exported files under application control.

When space is low:
- preserve already committed local business work;
- stop optional large processing;
- explain what is consuming SquiFlow-managed space;
- never delete unsynced attachments/business data merely to make the warning disappear.

## 10. Shared PC and session/user switching

A Windows PC may be used by more than one person.

Test and define:
- whether SquiFlow supports multiple Windows accounts/profiles;
- whether two SquiFlow users can share one Windows profile;
- local session lock/sign-out behavior;
- which locally stored business data remains on disk after sign-out;
- preventing User B from inheriting User A's authenticated session/unsent form context;
- device-level enrollment versus user-level authorization.

Signing out must not delete pending durable business work simply because current credentials expired.

## 11. Update/restart with pending work

Before update/restart:
- durable local transactions/outbox remain intact;
- active helper work is checkpointed/aborted according to its safe contract;
- update does not silently migrate/delete an unsupported local DB;
- rollback/version-compatibility behavior is explicit.

Guard may assist lifecycle recovery but is not allowed to `repair` business state by guessing.

## 12. Accessibility

Device and recovery UX follows `docs/ux/ACCESSIBILITY_AND_INTERACTION_QUALITY.md`.

Printer failure, offline/sync state, safe mode and update recovery must be understandable by keyboard/screen-reader users and cannot rely only on tray icons/colors.

## 13. Hostile qualification cases

Test:
- Workstation crash with pending outbox;
- Guard crash while Workstation remains healthy;
- both processes terminate unexpectedly;
- repeated Workstation startup crash;
- OS sleep/hibernate during heartbeat and print;
- clock jump;
- printer removed/renamed while selected;
- spooler service unavailable;
- native helper hang/crash;
- disk becomes full during staging/helper output;
- user signs out with unsynced work;
- update starts with pending work;
- stale/mismatched helper version after update.
