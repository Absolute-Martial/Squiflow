# Observability Baseline

**Version:** v0.0.16

This document is the high-level observability owner. Detailed implementation contracts live in:

- [`OBSERVABILITY_IMPLEMENTATION_CONTRACT.md`](OBSERVABILITY_IMPLEMENTATION_CONTRACT.md)
- [`STRUCTURED_LOGGING_AND_FAILURE_CODES.md`](STRUCTURED_LOGGING_AND_FAILURE_CODES.md)
- [`WORKSTATION_SERVER_LOG_PIPELINE.md`](WORKSTATION_SERVER_LOG_PIPELINE.md)
- [`OBSERVABILITY_VERIFICATION_ACCEPTANCE.md`](OBSERVABILITY_VERIFICATION_ACCEPTANCE.md)

## 1. Stable instrumentation boundary

OpenTelemetry/OTLP is the stable instrumentation boundary.

Current managed targets:
- New Relic for metrics, distributed traces and APM;
- Aiven OpenSearch for searchable structured operational logs;
- Backtrace for crash-oriented diagnostics where appropriate.

SquiFlow owns a small `SquiFlow.Observability` boundary on top of standard .NET/OpenTelemetry primitives. Application/domain code does not depend directly on provider APIs.

Managed observability is intentionally relied upon. Business transaction correctness remains independent from telemetry export.

## 2. Shared execution/correlation context

Important request/job/sync paths carry a common runtime context where applicable:

```text
TenantContext
Security/UserContext
CorrelationId
CausationId
CancellationToken
Deadline
IdempotencyKey / OperationId
Trace context
Retry/Diagnostic context
Rule/config version
```

Keep identities distinct:

```text
TraceId       = distributed telemetry execution
CorrelationId = SquiFlow business/conversation grouping
CausationId   = operation/message/event that caused this one
```

Telemetry context is not authorization authority. Tenant/security identity is derived and verified by the runtime/security boundary.

## 3. Stable structured events

Significant operational events use stable:

```text
EventId
EventName
FailureCode (for abnormal/failure outcomes)
```

Exception/message text remains diagnostic evidence rather than the only stable identity.

Sync, outbox, worker, Guard recovery and other operational state machines log meaningful state transitions.

The registry is source-controlled and protected by uniqueness/compatibility tests. High-value FailureCodes should identify an owning component and recovery/reconciliation guidance.

## 4. Server versus Workstation behavior

The vocabulary/instrumentation boundary is shared, but retention/export behavior differs:

```text
Server      → central-first telemetry with bounded buffering/export
Workstation → local-durable-first evidence + selective central export
```

The Workstation must remain diagnosable when offline or when remote telemetry is unavailable. Guard must continue local supervision/recovery independently of providers.

Local diagnostics are bounded/rotated and protect an explicit disk reserve so telemetry cannot endanger SQLite, OS operation or update/recovery capability.

## 5. Managed/free tiers are finite

Do not treat current free tiers as unlimited/permanent infrastructure.

Periodically verify:
- ingest/quota limits;
- retention;
- alerting/account/project limits;
- region/residency where relevant;
- export/API access needed for incidents;
- behavior after quota exhaustion;
- applicable privacy/contractual requirements.

Exact provider quotas belong to deployment/vendor data rather than timeless architecture constants.

## 6. Bounded telemetry behavior

Telemetry uses bounded queues/buffers/sampling and may degrade/drop rather than blocking committed business operations.

Bound:
- in-process telemetry buffer;
- collector/export queue;
- local log/spool disk use;
- batch size;
- retry duration/attempts;
- Guard/Workstation crash/diagnostic bundle size;
- high-cardinality attributes.

If export is unavailable/quota exhausted:
- keep business/audit truth intact;
- degrade telemetry according to policy;
- expose exporter/quota/drop health to operator/Admin surfaces when implemented;
- do not busy-loop or fill local disk/RAM indefinitely.

## 7. Authoritative audit is separate

Where history is part of product correctness, keep it in SquiFlow durable state rather than only an external log provider.

Examples include:
- tenant role/permission change requests and OpenFGA application/reconciliation outcome;
- Owner transfer;
- high-risk platform actions;
- payment/refund/reversal evidence;
- rule/workflow publication.

ZITADEL/OpenFGA/provider logs are not the only SquiFlow audit copy where SquiFlow business/security history is required.

## 8. Tenant/privacy isolation

Telemetry is another multi-tenant data path.

Required:
- tenant/context attributes only where useful/safe;
- no customer file/body/free-form content by default;
- never log secrets/tokens/passwords;
- minimize/pseudonymize sensitive identifiers where practical;
- OpenFGA tuple identifiers use opaque IDs rather than PII;
- tenant-scoped diagnostics/support views cannot leak another tenant;
- Guard crash/diagnostic artifacts have bounded access/retention and do not automatically package unrestricted customer data.

High-cardinality identifiers such as TenantId/UserId/WorkstationId/entity/job/correlation/trace IDs are not ordinary metric dimensions. Use controlled logs/traces for instance-specific investigation.

## 9. Root-cause-oriented signals

Do not stop at `logs exist`.

Correlate important paths:

```text
operation/request
→ ZITADEL-backed authentication/session
→ authoritative TenantContext
→ ASP.NET requirement
→ OpenFGA authorization check/model ID/consistency class
→ domain/workflow validation
→ DB transaction/idempotency
→ outbox/Worker when present
→ object/provider/native work when present
→ final result/reconciliation
```

For authorization changes:

```text
AuthorizationChangeId
→ requested role/grant diff
→ OpenFGA tuple write/delete attempt
→ provider response/unknown outcome
→ reconciliation
→ applied SquiFlow authorization revision
```

Diagnosis should distinguish:

```text
Observed symptom
Probable failure class
Evidence
Confidence/unknowns
Safe recovery/retry/reconciliation
```

Telemetry may support a recovery recommendation but must not automatically mutate ambiguous money/stock/security state.

## 10. Metrics and cardinality

Useful bounded measures can include:
- request rate/error/latency by safe work class;
- ZITADEL auth/session dependency failures/latency without logging tokens;
- OpenFGA check latency/error by safe operation class;
- OpenFGA authorization-change reconciliation backlog/age;
- DB pool use/wait/saturation;
- Worker queue depth + oldest age when Worker exists;
- retry volume/budget exhaustion;
- sync pending/conflict/rejection/OutcomeUnknown age;
- object usage/capacity/transfer trend;
- Guard restart/crash/hang/safe-mode events;
- Workstation process-tree resource anomalies;
- telemetry exporter/drop/quota/spool state.

Do not implement every metric before its component exists, but do not omit evidence needed to distinguish provider outage, authorization failure, business conflict, and process failure.

## 11. Sampling

High-volume successful traces may be sampled. Errors, slow requests, rare paths, resource breaches and reconciliation/unknown-outcome paths receive stronger capture.

Sampling operational telemetry must never delete authoritative audit/business records because those records are not telemetry substitutes.

## 12. Observability of observability

The telemetry pipeline itself is observable:
- dropped log/span counts;
- collector queue/buffer fill;
- exporter failures;
- local spool pressure;
- sampling ratios/decisions;
- redaction/processor failures where measurable;
- provider quota state where available.

Without this, “no failures visible” can mean “telemetry is broken.”

## 13. Alert storm control

Crash loops, shared network/provider outages or tenant-wide sync failures can create telemetry/alert storms.

Alerting/logging infrastructure must use grouping/deduplication/rate controls so incident reporting does not materially amplify resource pressure or page operators thousands of times for one underlying incident.

## 14. Time semantics

For telemetry:
- wall-clock timestamps are UTC;
- elapsed durations use monotonic timing;
- severe Workstation clock skew should be observable;
- client wall clock is not distributed ordering/idempotency authority.

Business timezone/effective-date/calendar semantics remain a separate domain policy and should be finalized before broad financial/scheduling production use.

## 15. Guard observability contract

Guard provides bounded lifecycle evidence such as:
- Workstation start/exit reason;
- crash/restart count;
- restart-budget/safe-mode transition;
- heartbeat/hang state;
- Guard/Workstation version mismatch;
- update/recovery state;
- bounded process/resource summary;
- diagnostic artifact reference where enabled.

Guard is not required to export directly to every telemetry vendor. It can write a bounded local diagnostic stream/evidence that normal SquiFlow telemetry/support paths consume when available.

## 16. Diagnostic bundles

Bundles are deliberate bounded support artifacts, not full application-directory/database dumps.

Typical content may include:

```text
manifest.json
runtime/system summary
application/configuration-redacted summary
recent structured logs
crash evidence
trace/resource summary
sync state summary
```

Default bundle policy excludes secrets, raw tokens/passwords/keys, unrestricted customer DB/files and unnecessary PII.

## 17. Identity/authorization dependency health

Dependency failure must be diagnosable without becoming accidental authorization success.

Distinguish:
- ZITADEL unreachable versus invalid/expired user session;
- OpenFGA unreachable versus explicit deny;
- OpenFGA model/config mismatch versus tuple absence;
- stale/lower-consistency concern versus higher-consistency request failure;
- authorization-change `OutcomeUnknown`/reconciliation from normal permission denial.

Do not expose privileged/internal details to ordinary unauthenticated clients; user-facing errors remain safe/stable.

## 18. Health semantics

Separate:
- liveness: should the process restart?;
- readiness: should it receive new traffic/work?;
- degraded dependency/capability health;
- privileged operator diagnosis.

Define health per capability rather than one global green/red flag.

## 19. Support-facing diagnosis

For important implemented failures, aim for:

```text
symptom
→ correlated evidence
→ likely failure class
→ unknowns/confidence
→ safe recovery/retry/reconciliation
```

Examples include:
- ZITADEL login/provider failure;
- OpenFGA authorization-change reconciliation;
- sync `AuthorizationChanged`/`OutcomeUnknown`;
- payment `OutcomeUnknown`;
- object metadata mismatch;
- storage/resource pressure;
- Guard crash loop/update recovery;
- printing/device failure.

## 20. Qualification

At minimum test:
- provider/collector unavailable/quota exhausted;
- local telemetry/Guard diagnostic spool near/full;
- secret/customer-data redaction;
- cross-tenant diagnostic isolation;
- metric cardinality under many tenants/entities;
- trace/correlation across identity → API → authorization → DB/outbox paths without leaking tokens;
- Workstation/Guard recovery while remote observability is unavailable;
- state-transition event/failure-code compatibility;
- alert deduplication/storm behavior;
- clock jump/skew/duration behavior;
- observability overhead on actual low-end deployment hardware.

See:
- `docs/observability/OBSERVABILITY_VERIFICATION_ACCEPTANCE.md`
- `docs/testing/VERIFICATION_STRATEGY.md`
- `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.
