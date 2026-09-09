# Workstation and Server Logging Pipeline

**Version:** v0.0.16  
**Status:** Accepted implementation direction

## 1. Different runtime responsibilities

The server and Workstation share the same telemetry vocabulary and OpenTelemetry boundary, but they do **not** use identical retention/export behavior.

```text
Server      → central-first, bounded buffering/export
Workstation → local-durable-first, selective central export
```

The Workstation must remain diagnosable while offline or when telemetry providers are unavailable. The server is normally connected and should favor centralized correlation and operator visibility.

## 2. Shared application boundary

```text
ILogger / ActivitySource / Meter
            │
            ▼
    SquiFlow.Observability
            │
            ▼
     OpenTelemetry SDK
       ┌────┼────┐
       │    │    │
     Logs Metrics Traces
       └────┼────┘
            ▼
        OTLP/Collector
```

Application/domain code does not depend directly on New Relic, OpenSearch, Backtrace, Grafana, or another provider API.

## 3. Server pipeline

Recommended server path:

```text
ASP.NET Core / Worker
        │
        ▼
structured telemetry
        │
        ▼
OpenTelemetry SDK
        │
        ▼
OTLP Collector
 ├─ enrich
 ├─ redact
 ├─ filter
 ├─ sample
 ├─ batch/buffer
 └─ route
        │
        ├─ New Relic metrics/traces/APM
        ├─ Aiven OpenSearch operational logs
        └─ future provider(s) by deployment policy
```

Provider failure or quota exhaustion must not roll back committed business transactions.

Server local files/spools, if used, are bounded operational buffers rather than the authoritative audit ledger.

## 4. Workstation pipeline

Recommended Workstation path:

```text
SquiFlow.Workstation / Guard
           │
           ▼
SquiFlow.Observability
      ┌────┴────┐
      │         │
      ▼         ▼
local bounded   OTLP/export
telemetry       when allowed/available
      │
      ├─ rolling structured logs
      ├─ crash/recovery evidence
      └─ diagnostic bundle inputs
```

Local evidence must survive normal application restart and temporary loss of network/collector access.

Guard remains able to supervise/recover locally when remote observability is unavailable.

## 5. Local Workstation storage policy

Use an application-owned diagnostics area with ACLs appropriate to the installation model. Exact Windows paths are packaging decisions.

Conceptual layout:

```text
Diagnostics/
├── Logs/
├── Crashes/
├── Bundles/
├── Spool/
└── State/
```

Rules:
- bounded total size;
- rotating files;
- age and size retention;
- compression for older local evidence where useful;
- atomic/robust writes where feasible;
- do not place authoritative business state in the log directory;
- do not let diagnostics consume the disk reserve needed by SQLite/OS/update recovery.

## 6. Diagnostic disk reserve

Diagnostics must shed low-value data before they endanger the Workstation.

Conceptual pressure stages:

```text
Normal
→ trim Debug/Trace first
→ reduce successful-operation retention
→ stop non-essential bundles
→ preserve recent Error/Critical + Guard crash/recovery evidence
→ expose local diagnostics pressure
```

Never busy-loop trying to write telemetry to a full disk.

The exact reserve/retention values are benchmarked and deployment-configurable.

## 7. What is centrally promoted from Workstation

Central export is policy driven.

Prefer stronger capture for:
- Error/Critical;
- crashes/restart loops;
- sync conflicts/reconciliation failures;
- repeated retries/retry-budget exhaustion;
- security/authorization anomalies that are safe to export;
- data-integrity warnings;
- resource-limit breaches;
- slow/rare traces selected by sampling policy.

Routine successful Info/Debug traffic may remain local or be sampled/filtered to protect bandwidth, storage and provider quotas.

## 8. Diagnostic bundles

A diagnostic bundle is a deliberately assembled support artifact, not a zip of the entire application directory.

Conceptual content:

```text
manifest.json
runtime.json
system.json
application.json
configuration-redacted.json
recent-logs.jsonl
crash.json
trace-summary.json
sync-state.json
resource-summary.json
```

Depending on the incident, some files may be absent.

Default bundle policy:
- no secrets/tokens/passwords;
- no unrestricted customer database;
- no arbitrary customer files/documents;
- bounded size/time window;
- explicit manifest of included evidence;
- tenant/support access controls;
- upload only according to support/privacy policy.

## 9. Server and Workstation share vocabulary

Do not define separate failure naming for equivalent distributed operations.

Example:

```text
Workstation: SYNC.ACK.OUTCOME_UNKNOWN
Server:      SYNC.APPLY.SUCCESS
Reconciler:  SYNC.RECONCILIATION.RECOVERED
```

The same CorrelationId/SyncChangeId lets support reconstruct the operation across sides.

## 10. Telemetry-provider outages

### Server
- continue business processing according to dependency policy;
- bounded retry/buffer/drop;
- expose exporter health;
- never fill disk/RAM indefinitely.

### Workstation
- retain bounded local evidence;
- continue local-capable work;
- retry/upload later according to policy;
- Guard remains operational.

## 11. Authoritative audit remains separate

Security/business history required for product correctness remains in durable SquiFlow state.

Operational logs may explain an admin/security/business event but do not replace the authoritative audit record.

## 12. Acceptance

Test at minimum:
- Workstation offline for an extended period with local logs rotating correctly;
- provider/collector unavailable;
- local diagnostic area near/full;
- crash while network is unavailable;
- export recovery after network returns;
- bundle redaction and size limits;
- server exporter queue saturation;
- duplicate/replayed export behavior does not affect business correctness;
- correlation across Workstation → Sync API → DB/outbox/worker paths.
