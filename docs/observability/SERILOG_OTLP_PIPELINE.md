# Serilog and OpenTelemetry Log Pipeline

**Status:** Accepted implementation direction

## 1. Decision

SquiFlow application processes use **Serilog** for structured application logging and the stable `Serilog.Sinks.OpenTelemetry` sink for OTLP log export.

The log path is intentionally distinct from the metrics/tracing SDK path:

```text
Application code
      │
      │ Serilog message templates + structured properties
      ▼
Serilog
      ├──────────────► bounded local JSONL sink
      │                 offline/recovery evidence
      │
      ▼
Serilog.Sinks.OpenTelemetry
      │
      │ OTLP/HTTP + Protobuf + TLS in production
      ▼
SquiFlow telemetry gateway / OpenTelemetry Collector
      │
      ├─ redact/filter/batch/route
      ├─ New Relic
      └─ OpenSearch
```

`Serilog.Sinks.OpenTelemetry` converts Serilog events directly to OTLP LogRecords. It does **not** require the OpenTelemetry .NET SDK to export logs.

Metrics and traces use the normal OpenTelemetry .NET SDK:

```text
System.Diagnostics.Metrics   ──► OpenTelemetry SDK ──► OTLP
System.Diagnostics.Activity  ──► OpenTelemetry SDK ──► OTLP
Serilog logs                 ──► Serilog OTel sink ──► OTLP
```

All three signals converge at the collector/gateway and are correlated using resource attributes plus standard TraceId/SpanId context.

## 2. Product/process ownership

`SquiFlow.Observability` and `SquiFlow.Diagnostics` are different boundaries:

```text
foundation/observability/SquiFlow.Observability
= shared logging/tracing/metrics instrumentation library

apps/desktop/diagnostics/SquiFlow.Diagnostics
= future on-demand desktop executable for heavy/offline diagnostic work
```

`SquiFlow.Guard` is also part of the desktop application but runs independently so it can supervise the Workstation. Diagnostics follows the same product ownership model while normally remaining stopped until triggered.

Detailed process model: `docs/workstation/DESKTOP_PROCESS_MODEL.md`.

## 3. Why this boundary exists

Serilog owns the application logging experience:
- message templates;
- structured properties;
- enrichers;
- local sinks;
- process-specific filtering.

OpenTelemetry owns the vendor-neutral observability transport/model:
- OTLP;
- log record interoperability;
- TraceId/SpanId correlation;
- metrics and traces;
- collector-side routing.

The Diagnostics executable owns heavy/offline diagnostic workflows when implemented:
- diagnostic bundle creation;
- retained-log/spool promotion;
- crash dump/reference processing;
- bounded redaction/compression/encryption;
- diagnostic artifact upload/retry policy.

Provider SDKs and credentials must not leak into domain/application code or Guard.

## 4. Workstation and Guard defaults

`SquiFlow.Workstation` and `SquiFlow.Guard` use the shared `SquiFlow.Observability` bootstrap.

Default local path is resolved under the SquiFlow diagnostics area and is bounded by rolling files. Exact packaging ACLs remain an installer/deployment concern.

Current configuration inputs:

```text
DOTNET_ENVIRONMENT
SQUIFLOW_LOG_DIRECTORY
SQUIFLOW_OTLP_LOGS_ENDPOINT
```

If `SQUIFLOW_OTLP_LOGS_ENDPOINT` is absent, local structured logging remains enabled and remote export is disabled. Remote observability availability must never determine business correctness or Guard supervision correctness.

For HTTP/Protobuf, the configured endpoint must be the OTLP logs endpoint expected by the deployed collector/gateway (for example a `/v1/logs` endpoint where required by the deployment).

## 5. Required common resource/event context

The shared bootstrap supplies low-cardinality process/resource context such as:

```text
service.name
service.version
deployment.environment
squiflow.component
```

Operation-level code adds only fields that materially apply, for example:

```text
EventName
FailureCode
OperationId
CorrelationId
JobId
SyncBatchId
UpdateId
MigrationId
CheckpointId
WorkstationId
```

TraceId and SpanId are taken from the current .NET `Activity` by the OpenTelemetry sink when trace context exists.

Do not use high-cardinality instance identifiers as ordinary metric labels.

## 6. Local durability policy

The local Serilog file sink is a bounded recovery trail, not an authoritative audit ledger.

Rules:
- JSON Lines structured output;
- rolling files;
- hard size/count retention;
- no unbounded retry queues in process memory;
- preserve recent Error/Critical and Guard recovery evidence under pressure where later spool policy supports priority;
- authoritative security/business audit data remains in durable SquiFlow state.

When the Diagnostics capability is implemented, it may promote retained local segments after connectivity returns. Guard monitors/triggers that capability but does not own network log transport.

## 7. Normal telemetry path

Do **not** launch `SquiFlow.Diagnostics` for ordinary online log export.

```text
SquiFlow.Workstation / SquiFlow.Guard
               │
               ▼
             Serilog
               │
               ▼
     Serilog OpenTelemetry sink
               │
          OTLP/HTTP Protobuf
               │
               ▼
Telemetry gateway / OTel Collector
```

This keeps ordinary logging low-overhead and avoids turning a helper process into a permanent logging daemon.

## 8. Heavy/offline diagnostics path

Heavy diagnostic work is deliberately separate:

```text
Crash / repeated failure / support request / retained spool
               │
               ▼
      Guard or Workstation trigger
               │
               ▼
       SquiFlow.Diagnostics
               │
               ├─ collect approved sources
               ├─ redact
               ├─ build manifest
               ├─ compress/encrypt
               ├─ persist/upload
               └─ emit DiagnosticResult
               │
               ▼
              exit
```

Large dumps and bundles are not OTLP LogRecords.

Diagnostic artifacts use an approved HTTPS diagnostic API or durable artifact/object-storage endpoint. The exact server-side endpoint is a deployment/application decision and must not expose provider credentials to the workstation.

## 9. Transport ownership

Ownership is explicit:

```text
Application/Guard
    └─ produce structured events

SquiFlow.Observability
    └─ formatting/enrichment/local sink/direct OTLP client boundary

SquiFlow.Diagnostics (on demand)
    └─ heavy/offline bundle + retained-spool promotion + diagnostic upload policy

OTel Collector / SquiFlow telemetry gateway
    └─ batching, redaction, sampling/filtering, routing and provider credentials
```

Guard must not contain New Relic/OpenSearch credentials and must not become the central log shipper.

## 10. Sensitive-data rules

Never log by default:
- passwords;
- access/refresh tokens;
- session secrets/cookies;
- encryption/private keys;
- raw payment credentials;
- unrestricted customer documents;
- arbitrary request/response bodies;
- unnecessary PII/free-form business data.

Prefer opaque identifiers and centrally testable redaction.

Diagnostic bundles apply the same rule and additionally require an explicit manifest of included evidence.

## 11. Failure behavior

- OTLP/collector outage must not prevent Workstation or Guard startup.
- Local log failure must use a bounded writable fallback where possible rather than crash the product.
- A failed diagnostic upload leaves a bounded recoverable diagnostic artifact/state according to policy; it must not create an unbounded retry loop.
- Diagnostics process failure must not corrupt authoritative business state or disable ordinary local-first Workstation use.
- Telemetry loss never becomes business/audit authority loss because authoritative audit/business state is separate.

## 12. Acceptance

Before this path is considered production-qualified, verify:
- Guard and Workstation start with collector unavailable;
- bounded files rotate and retain correctly;
- OTLP/HTTP-Protobuf export reaches the collector;
- structured fields remain queryable without parsing rendered message text;
- Activity TraceId/SpanId appears on logs created within traced operations;
- provider outage does not block Workstation startup or Guard recovery;
- sensitive-data redaction tests pass;
- telemetry overhead stays within Workstation/Guard resource budgets;
- Diagnostics, once implemented, runs on demand rather than permanently;
- heavy bundle upload is distinct from ordinary OTLP logs;
- Diagnostics crash/retry behavior remains bounded and recoverable.
