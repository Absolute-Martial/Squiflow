# Workstation Guard and Recovery Boundary

**Version:** v0.0.18

`SquiFlow.Guard` is an accepted companion process for the Windows Workstation. It is part of the SquiFlow desktop application, while running as an independent process so it can observe and recover failures outside the Workstation process itself.

The design goal is the **smallest production-honest supervision/recovery scope**: keep Guard low-resource and reliable while fully satisfying the monitoring/recovery contract it claims. Resource restraint must never remove required monitoring, failure handling, recovery, evidence, or compatibility behavior.

## 1. Product and process model

Baseline desktop runtime:

```text
SquiFlow Desktop Application
│
├── SquiFlow.Guard              always-running/mostly idle
└── SquiFlow.Workstation        always-running while the desktop app is in use

on-demand capability processes when implemented:
├── SquiFlow.Diagnostics
├── SquiFlow.Maintenance
├── SquiFlow.Sync
└── SquiFlow.Document
```

A separate executable is a fault/lifecycle/resource boundary, not a separate product, microservice, or business authority.

Detailed desktop process ownership is defined in `docs/workstation/DESKTOP_PROCESS_MODEL.md`.

## 2. Guard responsibilities

Guard owns desktop process/recovery concerns that must survive or observe a Workstation failure:

- launch and supervise the Workstation process;
- distinguish normal shutdown/update from unexpected exit;
- detect crash and bounded heartbeat/hang conditions;
- apply bounded restart/backoff policy rather than an infinite crash loop;
- enter/support safe-start or recovery mode after repeated startup failure;
- coordinate update handoff, preflight, failed-update recovery, and version compatibility checks that require an external process;
- require/track a verified recovery checkpoint before a destructive migration/update when policy requires it;
- evaluate post-update health/readiness gates before committing the new version as healthy;
- coordinate rollback/recovery according to an explicit compatibility plan;
- terminate/reap orphan child/capability processes created under the Workstation lifecycle;
- collect only lightweight bounded crash/error/resource evidence directly;
- trigger `SquiFlow.Diagnostics` when heavy diagnostic collection/package/upload is required;
- expose a small local health/status contract to the Workstation/support path;
- preserve enough evidence to explain whether failure was crash, hang, update problem, resource exhaustion, or deliberate shutdown.

Guard must continue working under ordinary Workstation failure modes; making it so minimal that it cannot monitor/recover those cases defeats the boundary.

## 3. Guard does not own business or heavy capability implementation

Guard must not become a second business application or general worker host. It does not own:

- customer/order/payment/inventory rules;
- tenant role or OpenFGA authorization decisions;
- business synchronization semantics;
- central database credentials;
- rule/workflow evaluation;
- normal document/printing business decisions;
- server Worker/platform-control responsibilities;
- ad-hoc repair of business rows;
- SQLite backup implementation;
- schema/data migration implementation;
- diagnostic bundle compression/encryption/upload;
- large crash dump processing;
- New Relic/OpenSearch/provider credentials;
- durable business scheduling.

If business data needs repair, Guard can start an approved recovery flow or capability process; it must not invent business truth.

## 4. Guard ↔ Workstation/capability contract

Use a narrow versioned local IPC/lifecycle contract rather than broad internal APIs. It may carry:

- heartbeat/liveness state;
- current application version/update phase;
- intentional shutdown/restart reason;
- safe-mode request/result;
- bounded health/resource summary;
- crash/diagnostic artifact references;
- capability-process lifecycle identifiers/status;
- diagnostic trigger/result metadata;
- update/checkpoint/migration identifiers and lifecycle state where required.

Do not send customer records, role grants, provider secrets, unrestricted logs/files, or general business commands through Guard IPC.

`System.Threading.Channels` is process-local and is not a Guard/Workstation IPC transport. For Windows local IPC, named pipes are the preferred first implementation candidate; the semantic contract remains transport-independent until the POC closes the mechanism.

A future `SquiFlow.Workstation.Runtime.Contracts` project is created when the first stable IPC implementation needs it. It contains DTOs/enums/contracts only and must not reference Avalonia, SQLite/EF, Proto.Actor, Quartz, document libraries, or business modules.

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

- Workstation/capability exit/crash status;
- sustained memory growth or extreme RSS;
- thread/handle/process count anomalies where practical;
- repeated restart frequency;
- persistent startup/update failure;
- local disk pressure signals needed for safe recovery/diagnostics.

These signals are diagnostic evidence, not permission to kill healthy work merely because a threshold was briefly crossed.

Guard itself is expected to be low-resource and mostly idle. Measure it on supported low-spec hardware and bound leaks/retry loops rather than inventing an arbitrary memory promise.

## 7. Update/migration lifecycle

Guard is valuable specifically because an application cannot reliably replace/recover its own executable while it is running.

The accepted lifecycle is:

```text
verify update/package
→ preflight
→ enter update/maintenance state
→ request recovery checkpoint
→ verify checkpoint
→ stage/replace binaries
→ run migration through owning capability
→ start target version
→ readiness/health gates
→ commit update OR rollback/recovery
```

The lifecycle must preserve:

- local database and pending outbox;
- staged unsynced attachments/payloads;
- compatible rollback/forward-recovery path where supported;
- old/new Workstation/Guard protocol compatibility during handoff;
- migration journal/recovery evidence;
- diagnostic evidence when update/startup fails.

Guard may select the last known good executable/version according to the update design, but it never rolls back business data by guessing.

Detailed owner: `docs/workstation/UPDATE_MIGRATION_RECOVERY_OWNERSHIP.md`.

## 8. Diagnostics ownership

Guard owns **triggering and supervision**, not heavy diagnostics implementation.

Normal telemetry does not require the Diagnostics process:

```text
Workstation / Guard
       ↓
Serilog
       ↓
Serilog OTel sink
       ↓
OTLP/HTTP + Protobuf
       ↓
Telemetry gateway / OTel Collector
```

Heavy/offline diagnostic work uses the on-demand capability boundary:

```text
Guard / Workstation
       ↓ trigger
SquiFlow.Diagnostics
       ├─ collect approved evidence
       ├─ redact
       ├─ build manifest
       ├─ compress/encrypt when required
       ├─ persist/upload according to policy
       └─ emit DiagnosticResult
       ↓
      exit
```

Typical Diagnostics inputs may include bounded recent lifecycle logs, crash marker/dump references, update/migration state, resource summary and sync-state metadata. It must not automatically include unrestricted customer databases/files.

Large diagnostic artifacts use the diagnostic HTTPS/object-storage path, not OTLP log records.

## 9. Failure independence

Test both directions:

- Guard crashes while Workstation remains healthy: Workstation business work must continue; restarting Guard must not corrupt it.
- Workstation crashes while Guard remains healthy: Guard captures bounded evidence and applies bounded recovery.
- Diagnostics/capability process crashes: Guard/Workstation remain usable; accepted durable work/evidence remains recoverable.
- both Guard and Workstation terminate: next launch recovers from durable local state rather than in-memory assumptions.

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
- capability process crash during diagnostic/checkpoint work;
- duplicate capability launch/request;
- user sign-out/shutdown with unsynced business work;
- network outage during ordinary Workstation operation (Guard must not misclassify it as process failure);
- collector/provider unavailable (Guard and local recovery still work).

These qualification cases are evidence obligations, not a one-time checklist. Once Guard exists, cheap lifecycle/state-machine/architecture cases remain blocking regression tests; process/update/migration fault cases receive an explicit recurring/pre-release cadence under `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`.

## 11. Complexity rule

Guard is justified; **arbitrary helper layers are not**.

Do not add `GuardManager`, `GuardService`, `GuardCoordinator`, or one interface per Guard class merely for layering. Add code/boundaries only where they support the supervision/recovery contract above.

Similarly, do not create empty Diagnostics/Maintenance/Sync/Document projects solely to make the repository tree resemble an architecture diagram. Create each executable when its first real isolated capability is implemented.

## 12. Guard internal structure direction

Guard should stay one small executable project plus a future narrow runtime-contract project when IPC is implemented. It should be organized by operational responsibility rather than full Domain/Application/Infrastructure layering.

Preferred shape as implementation grows:

```text
SquiFlow.Guard/
├── Program.cs
├── Hosting/
├── Supervision/
├── Recovery/
├── Monitoring/
│   ├── Heartbeats/
│   └── Resources/
├── Ipc/
├── Diagnostics/          # trigger/reference handling only; not bundle engine
├── Updates/
├── Platform/
│   └── Windows/
└── State/
```

Do not put Avalonia, EF Core/SQLite, Proto.Actor, Quartz, MassTransit, document/image libraries, or business modules into Guard merely because those technologies exist elsewhere in SquiFlow.
