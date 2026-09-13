# Workstation Desktop Process Model

**Status:** Accepted implementation direction

## 1. Product boundary versus process boundary

The Windows desktop product is one SquiFlow application composed of multiple process boundaries where fault isolation, lifecycle independence, resource reclamation, or recovery requires them.

A separate executable does **not** imply a separate product, microservice, business authority, or independently modeled domain.

```text
SquiFlow Desktop Application
│
├── SquiFlow.Workstation     always-running UI + local-first application runtime
├── SquiFlow.Guard           always-running supervision/recovery companion
└── capability processes     normally stopped; started only when their work exists
    ├── SquiFlow.Diagnostics
    ├── SquiFlow.Maintenance
    ├── SquiFlow.Sync
    └── SquiFlow.Document
```

Only process boundaries whose first real capability has been implemented should exist as projects in the repository. This document reserves ownership/lifecycle semantics; it does not authorize empty placeholder projects.

## 2. Repository placement

Desktop executables belong under `apps/desktop/` because they are runtime parts of the desktop application:

```text
apps/
└── desktop/
    ├── workstation/
    │   └── SquiFlow.Workstation/
    ├── guard/
    │   └── SquiFlow.Guard/
    ├── diagnostics/          # create when first diagnostic capability is implemented
    │   └── SquiFlow.Diagnostics/
    ├── maintenance/          # create when first backup/maintenance capability is implemented
    │   └── SquiFlow.Maintenance/
    ├── sync/                 # create when first isolated sync process is implemented
    │   └── SquiFlow.Sync/
    └── document/             # create when heavy document isolation is implemented
        └── SquiFlow.Document/
```

Shared code is different from executable ownership:

```text
foundation/
├── observability/
│   └── SquiFlow.Observability/
└── workstation-runtime/      # create when the first stable IPC contract requires it
    └── SquiFlow.Workstation.Runtime.Contracts/
```

`SquiFlow.Observability` is a reusable logging/tracing/metrics foundation. `SquiFlow.Diagnostics` is an executable capability process. They must not be conflated.

## 3. Process lifetime classes

| Process | Normal lifetime | Responsibility |
|---|---|---|
| `SquiFlow.Workstation` | Always while desktop product is running | UI, local application/runtime, local-first interaction |
| `SquiFlow.Guard` | Always/mostly idle | process supervision, heartbeat/recovery, update lifecycle coordination |
| `SquiFlow.Diagnostics` | On demand | diagnostic bundle creation, retained-log promotion, crash evidence packaging/upload |
| `SquiFlow.Maintenance` | On demand | recovery checkpoint/backup and bounded maintenance work |
| `SquiFlow.Sync` | Triggered/periodic if process isolation is implemented | local/server synchronization execution |
| `SquiFlow.Document` | On demand | memory/CPU/native-heavy document/PDF/image work |

A capability process should exit after its accepted durable result/state is committed. Process exit is a resource-reclamation mechanism, not a correctness mechanism.

## 4. Guard relationship

Guard supervises processes; it does not absorb their work.

```text
Guard
├── launch / observe / stop process
├── heartbeat and liveness evidence
├── bounded restart/backoff/crash-loop policy
├── update/recovery coordination
└── trigger capability when recovery policy requires it

Guard does NOT
├── build diagnostic bundles
├── upload large artifacts
├── implement SQLite backup
├── execute schema migrations
├── perform document rendering
└── own business synchronization semantics
```

Guard may launch a capability process itself or supervise a capability launch requested by the Workstation, according to the final IPC/lifecycle design.

## 5. Diagnostics process

`SquiFlow.Diagnostics` is a desktop capability process, not a server Worker and not a foundation library.

Typical triggers:
- crash or repeated crash-loop;
- failed update/migration;
- corruption/integrity investigation;
- resource-limit breach requiring a bounded snapshot;
- explicit support/admin diagnostic request;
- offline retained-log/spool promotion when a dedicated process is justified.

Conceptual lifecycle:

```text
Workstation / Guard
       │
       │ versioned request + approved artifact references
       ▼
SquiFlow.Diagnostics
       │
       ├─ collect only approved evidence
       ├─ apply redaction/privacy policy
       ├─ build manifest
       ├─ compress/encrypt when required
       ├─ persist or upload according to policy
       └─ emit DiagnosticResult
       │
       ▼
      exit
```

The process must remain bounded in memory, disk, time window and artifact size.

## 6. Normal telemetry versus heavy diagnostics

Do not start `SquiFlow.Diagnostics` for every ordinary log batch.

Normal online telemetry:

```text
Workstation / Guard
       ↓
Serilog
       ↓
Serilog OpenTelemetry sink
       ↓
OTLP/HTTP + Protobuf
       ↓
SquiFlow telemetry gateway / OTel Collector
```

Heavy/offline diagnostic path:

```text
Workstation / Guard
       ↓ trigger
SquiFlow.Diagnostics
       ↓
redacted bundle / retained-log promotion / crash artifact
       ↓
HTTPS diagnostic API or approved durable artifact endpoint
```

Large dumps/bundles are not OTLP log records.

## 7. IPC and runtime contracts

When cross-process communication is implemented, contracts must be narrow, versioned and implementation-independent. `System.Threading.Channels` remains process-local and must not be used as a cross-process transport.

A future `SquiFlow.Workstation.Runtime.Contracts` project may contain only contract DTOs/enums such as:

```text
Guard/
  GuardCommand
  GuardStatus
  GuardEvent

Heartbeats/
  HeartbeatMessage
  ComponentIdentity

Capabilities/
  CapabilityKind
  StartCapabilityRequest
  StopCapabilityRequest
  CapabilityStatus

Diagnostics/
  DiagnosticTriggerRequest
  DiagnosticResult

Lifecycle/
  ShutdownReason
  UpdatePhase
  RecoveryState
```

It must not reference Avalonia, EF Core/SQLite, Proto.Actor, Quartz, Serilog provider sinks, document libraries, or business modules.

For Windows local IPC, named pipes are the preferred first implementation candidate because they support local-only communication and ACL control with low overhead. The semantic contract remains transport-independent until the Windows POC closes the choice.

## 8. Dependency rules

Executable processes may reference shared SquiFlow contracts/foundation libraries, but must not reference another executable implementation project merely to call internal classes.

```text
Workstation ─┐
Guard ───────┼──► shared runtime contracts
Diagnostics ─┤
Maintenance ─┘

Workstation/Guard/Diagnostics ─► SquiFlow.Observability
```

The shared contract direction points inward; executable implementation dependencies do not point sideways into each other.

## 9. Scheduling and wake-up ownership

Guard is not the durable scheduler.

- server scheduling remains owned by the accepted Worker/Quartz/SquiFlow durable-job architecture;
- Workstation durable scheduling belongs to the Workstation/runtime capability when implemented;
- Guard may use simple monotonic timers/timeouts for supervision only;
- `System.Threading.Channels` may be used inside a process as a bounded wake-up/backpressure mechanism;
- a Channel is never cross-process transport or durable authority.

## 10. Acceptance rules

Before a capability process is added as production code, prove why process isolation is useful and test at least:
- caller/Guard crash before process start;
- capability crash during work;
- caller crash while capability continues;
- duplicate launch/request behavior;
- bounded restart/retry behavior;
- durable result/recovery state after process loss;
- version mismatch between caller and capability;
- IPC timeout/cancellation;
- disk/resource pressure;
- privacy/redaction for diagnostic artifacts;
- normal Workstation correctness when the optional capability is unavailable.
