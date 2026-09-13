# Observability Implementation Contract

**Version:** v0.0.16  
**Status:** Accepted implementation direction

## 1. Product-owned boundary

SquiFlow owns an observability boundary built on Serilog plus standard .NET/OpenTelemetry primitives.

```text
SquiFlow runtime
      │
      ▼
SquiFlow.Observability
      │
      ├─ structured logs → Serilog → Serilog OTel sink → OTLP
      ├─ traces          → Activity → OpenTelemetry SDK → OTLP
      └─ metrics         → Meter    → OpenTelemetry SDK → OTLP
                                          │
                                          ▼
                               Collector / deployment routing
```

The stable `Serilog.Sinks.OpenTelemetry` sink exports logs directly as OTLP LogRecords; logs do not need to pass through the OpenTelemetry .NET SDK. Provider SDKs must not leak into domain/application contracts.

The detailed logging transport contract is defined in `SERILOG_OTLP_PIPELINE.md`.

## 2. Suggested responsibility structure

Conceptual project layout:

```text
SquiFlow.Observability
├── Context/
│   ├── ExecutionContext
│   └── DiagnosticContext
├── Logging/
│   ├── StructuredLogging
│   ├── EventRegistry
│   ├── LogEnrichment
│   └── SensitiveDataRedactor
├── Tracing/
│   ├── ActivityNames
│   └── Propagation
├── Metrics/
│   ├── MeterNames
│   └── Instruments
├── Health/
├── Diagnostics/
└── Hosting/
    └── ObservabilityExtensions
```

Names are illustrative; responsibilities are the contract.

## 3. Execution context

A request/job/change carries the context that materially applies:

```text
TenantContext
Security/UserContext
CorrelationId
CausationId
CancellationToken
Deadline
IdempotencyKey / OperationId
Trace context
Retry context
Diagnostic context
Rule/config version where relevant
```

Tenant authority is always derived/verified from the security/runtime boundary; telemetry context is not authorization authority.

## 4. Automatic enrichment

Middleware/hosting/worker infrastructure enriches telemetry once with governed fields such as:
- service name/version;
- deployment/environment;
- component/module/operation;
- trace/span/correlation/causation;
- verified tenant/workstation identity where privacy policy permits;
- job/message/sync identifiers when diagnostically useful;
- rule/config version when relevant.

Developers should not repeat TenantId/CorrelationId boilerplate manually at every call site.

## 5. Trace boundaries

Create spans around meaningful boundaries, not every method:
- inbound HTTP/gRPC;
- command/query handler when diagnostically useful;
- DB external dependency/transaction;
- sync batch/item apply;
- outbox dispatch/message consumption;
- durable worker job;
- rule evaluation;
- object storage/external HTTP;
- document/native-heavy work;
- important admin/config operation.

A trace should answer where latency/failure occurred.

## 6. Metrics

Initial bounded families include:

### API
- request rate/count;
- request duration;
- active requests;
- error/status class.

### DB
- dependency latency/error;
- pool usage/wait/saturation;
- transaction/query classes only where cardinality is bounded.

### Worker/outbox
- jobs started/completed/failed;
- active jobs;
- job duration;
- retry/defer/throttle;
- queue/outbox depth and oldest age.

### Sync
- upload/download backlog;
- conflicts/rejections;
- retry count;
- cursor/projection lag;
- outcome-unknown/reconciliation backlog.

### Runtime/resource
- RSS/working set;
- managed heap/allocation/GC;
- CPU;
- process/thread/handle metrics where useful;
- crash/restart/recovery counts.

### Object/document/external
- latency/errors;
- resource-budget breaches;
- ambiguous external outcomes/reconciliation backlog where measurable.

## 7. Cardinality

Do not casually use these as ordinary metric dimensions:

```text
TenantId
UserId
WorkstationId
EntityId
JobId
MessageId
CorrelationId
TraceId
```

Use logs/traces for instance-specific investigation. Tenant-level SLO/billing metrics require a purpose-built bounded aggregation if later needed.

## 8. Sampling

Sampling policy protects cost/resource budgets without destroying rare failure evidence.

Preferred behavior:
- aggressively sample high-volume fast success where necessary;
- retain/error-biased sampling for failures;
- retain slow/rare paths more strongly;
- never sample away authoritative audit/business receipts because those are not telemetry in the first place.

Collector-side/tail-aware policies are preferred where deployed and practical.

## 9. Root-cause evidence model

Diagnosis should distinguish:

```text
Observed symptom
Probable failure class
Evidence
Confidence/unknowns
Safe recovery/retry/reconciliation
```

Do not claim certainty when telemetry only shows correlation.

Useful failure classes include:
- application defect;
- database problem;
- identity/authorization dependency problem;
- configuration/version mismatch;
- resource exhaustion;
- network/external dependency;
- sync conflict/unknown outcome;
- storage/object inconsistency;
- process crash/hang.

Telemetry may recommend recovery but must not automatically mutate ambiguous financial/stock/security state.

## 10. Health semantics

Separate:
- **liveness** — should the process restart?;
- **readiness** — should it receive new traffic/work?;
- **degraded capability/dependency** health;
- **privileged diagnosis** for operators.

Avoid one global green/red health flag.

## 11. Observability of observability

The pipeline itself is monitored:
- dropped log/span counts;
- collector queue/buffer fill;
- exporter failures;
- local spool pressure;
- sampling ratios/decisions;
- redaction/processor failures;
- provider quota state where available.

No visible errors can otherwise mean the telemetry pipeline itself failed.

## 12. Alert behavior

Alerting must include deduplication/grouping/rate control. Crash loops or wide outages must not create an alert storm that amplifies the incident.

High-value alerts/failure codes should identify an owner and recovery/reconciliation runbook.

## 13. Provider routing

Current managed targets remain:
- New Relic for APM/metrics/traces;
- Aiven OpenSearch for searchable operational logs;
- Backtrace for crash diagnostics where appropriate.

The OTLP boundary allows replacement/augmentation without changing business code.

## 14. Definition of implemented

The observability foundation is not complete when packages are installed. It is complete when a representative vertical slice proves:
- trace/log correlation across request → authorization → DB → outbox/worker;
- stable EventId/EventName/FailureCode;
- secret/PII redaction;
- bounded metric cardinality;
- provider outage behavior;
- Workstation offline/local-durable evidence;
- diagnostic bundle behavior;
- observability-pipeline self-health;
- resource overhead within the agreed server/Workstation budgets.
