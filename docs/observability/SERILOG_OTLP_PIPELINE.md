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

## 2. Why this boundary exists

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

Provider SDKs and credentials must not leak into domain/application code.

## 3. Workstation and Guard defaults

`SquiFlow.Workstation` and `SquiFlow.Guard` use the shared `SquiFlow.Observability` bootstrap.

Default local path is resolved under the machine-wide SquiFlow diagnostics area and is bounded by rolling files. Exact packaging ACLs remain an installer/deployment concern.

Current configuration inputs:

```text
DOTNET_ENVIRONMENT
SQUIFLOW_LOG_DIRECTORY
SQUIFLOW_OTLP_LOGS_ENDPOINT
```

If `SQUIFLOW_OTLP_LOGS_ENDPOINT` is absent, local structured logging remains enabled and remote export is disabled. Remote observability availability must never determine business correctness or Guard supervision correctness.

For HTTP/Protobuf, the configured endpoint must be the OTLP logs endpoint expected by the deployed collector/gateway (for example a `/v1/logs` endpoint where required by the deployment).

## 4. Required common resource/event context

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

## 5. Local durability policy

The local Serilog file sink is a bounded recovery trail, not an authoritative audit ledger.

Rules:
- JSON Lines structured output;
- rolling files;
- hard size/count retention;
- no unbounded retry queues in process memory;
- preserve recent Error/Critical and Guard recovery evidence under pressure where later spool policy supports priority;
- authoritative security/business audit data remains in durable SquiFlow state.

A future Diagnostics capability worker may promote retained local segments after connectivity returns. Guard monitors/triggers that capability but does not own network log transport.

## 6. Transport ownership

Ownership is explicit:

```text
Application/Guard
    └─ produce structured events

SquiFlow.Observability
    └─ formatting/enrichment/local sink/OTLP client boundary

Diagnostics/Telemetry capability
    └─ durable spool promotion, diagnostic bundles, retries, upload policy

OTel Collector / SquiFlow telemetry gateway
    └─ batching, redaction, sampling/filtering, routing and provider credentials
```

Guard must not contain New Relic/OpenSearch credentials and must not become the central log shipper.

## 7. Sensitive-data rules

Never log by default:
- passwords;
- access/refresh tokens;
- session secrets/cookies;
- encryption/private keys;
- raw payment credentials;
- unrestricted customer documents;
- arbitrary request/response bodies;
- unnecessary PII/free-form business data.

Prefer opaque identifiers and centralized redaction tests.

## 8. Heavy diagnostics are a separate plane

Crash dumps and diagnostic bundles are not OTLP log records.

```text
Crash/severe failure
      │
      ▼
Diagnostics worker
      │ collect/redact/compress/encrypt
      ▼
HTTPS diagnostic upload
      │
      ▼
diagnostic API / durable object storage
```

The OTLP logging path may emit metadata/reference IDs for such artifacts, but large binary artifacts remain outside OTLP.

## 9. Acceptance

Before this path is considered production-qualified, verify:
- Guard and Workstation start with collector unavailable;
- bounded files rotate and retain correctly;
- OTLP/HTTP-Protobuf export reaches the collector;
- structured fields remain queryable without parsing rendered message text;
- Activity TraceId/SpanId appears on logs created within traced operations;
- provider outage does not block Workstation startup or Guard recovery;
- sensitive-data redaction tests pass;
- telemetry overhead stays within Workstation/Guard resource budgets.
