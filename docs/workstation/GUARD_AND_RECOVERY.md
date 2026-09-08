# Workstation Guard and Recovery Boundary

**Version:** v0.0.15

`SquiFlow.Guard` is an accepted companion process for the Windows Workstation. It exists because Workstation supervision, crash/hang recovery, update recovery, and diagnostic evidence are real product responsibilities. Removing the process merely to reduce component count would weaken the local-first Workstation.

The design goal is **small enough to remain reliable, but complete enough to perform its job**. Resource restraint must never remove required monitoring/recovery behavior.

## 1. Process model

Baseline desktop process tree:

```text
SquiFlow.Guard
└── SquiFlow.Workstation

optional narrow helper later only when a specific native/heavy component earns isolation
```

Guard may launch/relaunch the Workstation and coordinate its lifecycle. The Workstation remains the business/UI/local-first application.

## 2. Guard responsibilities

Guard owns desktop process/recovery concerns that must survive or observe a Workstation failure:

- launch and supervise the Workstation process;
- distinguish normal shutdown/update from unexpected exit;
- detect crash and bounded heartbeat/hang conditions;
- apply bounded restart/backoff policy rather than an infinite crash loop;
- enter/support safe-start or recovery mode after repeated startup failure;
- coordinate update handoff, failed-update recovery, and version compatibility checks that require an external process;
- terminate/reap orphan child/helper processes created under the Workstation lifecycle;
- collect bounded crash/error evidence and process/resource observations useful for support;
- expose a small local health/status contract to the Workstation/support path;
- preserve enough evidence to explain whether failure was crash, hang, update problem, resource exhaustion, or deliberate shutdown.

Guard must continue working under ordinary Workstation failure modes; making it so minimal that it cannot monitor/recover those cases defeats the boundary.

## 3. Guard does not own business authority

Guard must not become a second business application. It does not own:

- customer/order/payment/inventory rules;
- tenant role or OpenFGA authorization decisions;
- business synchronization semantics;
- central database credentials;
- rule/workflow evaluation;
- normal document/printing business decisions;
- Worker/platform-control responsibilities;
- ad-hoc repair of business rows.

If business data needs repair, Guard can start an approved recovery flow or package diagnostics; it must not invent business truth.

## 4. Guard ↔ Workstation contract

Use a narrow versioned local IPC/lifecycle contract rather than broad internal APIs. It may carry:

- heartbeat/liveness state;
- current application version/update phase;
- intentional shutdown/restart reason;
- safe-mode request/result;
- bounded health/resource summary;
- crash/diagnostic artifact references;
- helper-process lifecycle identifiers where a helper exists.

Do not send customer records, role grants, provider secrets, or general business commands through Guard IPC.

The exact IPC mechanism is an implementation choice for the Windows POC; the semantic contract is the boundary.

## 5. Restart and hang policy

Conceptual Guard states:

```text
Starting
Healthy
SuspectedHung
Recovering
Backoff
RestartBudgetExceeded
SafeModeRequired
Updating
UpdateRecovery
Stopped
```

A single missed heartbeat must not automatically kill the Workstation. Hang detection uses bounded timing and corroborating evidence appropriate to the UI/process behavior.

Repeated failure triggers backoff and a visible recovery path. Never create an endless restart loop that continuously consumes CPU/disk or hides a deterministic startup crash.

## 6. Resource monitoring

Guard may observe process-tree indicators such as:

- Workstation/helper exit/crash status;
- sustained memory growth or extreme RSS;
- thread/handle/process count anomalies where practical;
- repeated restart frequency;
- persistent startup/update failure;
- local disk pressure signals needed for safe recovery/diagnostics.

These signals are diagnostic evidence, not permission to kill healthy work merely because a threshold was briefly crossed.

Guard itself is expected to be low-resource and mostly idle, but there is no arbitrary tiny-memory target that overrides required functionality. Measure it on supported low-spec hardware and bound leaks/retry loops.

## 7. Updates

Guard is valuable specifically because an application cannot reliably replace/recover its own executable while it is running.

Update lifecycle must preserve:

- local database and pending outbox;
- staged unsynced attachments;
- compatible rollback path where supported;
- old/new Workstation/Guard protocol compatibility during handoff;
- diagnostic evidence when update/startup fails.

Guard may select the last known good executable/version according to the update design, but it never rolls back business data by guessing.

## 8. Diagnostics and privacy

Guard can package bounded technical evidence such as:

- process exit/error code;
- application/Guard version;
- crash marker/dump references where enabled;
- recent bounded lifecycle events;
- resource summary;
- update state.

Do not automatically upload raw customer business data or unrestricted local files. Diagnostics follow the same tenant/privacy/redaction rules as the rest of SquiFlow.

## 9. Failure independence

Test both directions:

- Guard crashes while Workstation remains healthy: Workstation business work must continue; restarting Guard must not corrupt it.
- Workstation crashes while Guard remains healthy: Guard captures evidence and applies bounded recovery.
- both terminate: the next launch recovers from durable local state rather than in-memory assumptions.

Guard is supervision/recovery, not a single point whose transient failure destroys local-first correctness.

## 10. Qualification cases

At minimum test:

- Workstation crash immediately after a durable local commit;
- Workstation startup crash loop;
- UI/process hang and recovery timeout;
- OS sleep/hibernate and clock jump during heartbeat;
- Guard crash/restart;
- both processes killed unexpectedly;
- update interrupted before/after executable replacement;
- version mismatch between Guard and Workstation;
- disk low/full while diagnostics/update files are being written;
- runaway helper process where a helper exists;
- user sign-out/shutdown with unsynced business work;
- network outage during ordinary Workstation operation (Guard must not misclassify it as process failure).

## 11. Complexity rule

Guard is justified; **arbitrary helper layers are not**.

Do not add `GuardManager`, `GuardService`, `GuardCoordinator`, or one interface per Guard class merely for layering. Add code/boundaries only where they support the supervision/recovery contract above.
