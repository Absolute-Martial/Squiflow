# Observability Baseline

**Version:** v0.0.15

## 1. Stable instrumentation boundary

OpenTelemetry/OTLP is the stable instrumentation boundary.

Current managed targets:
- New Relic for metrics, distributed traces and APM;
- Aiven OpenSearch for searchable structured operational logs;
- Backtrace for crash-oriented diagnostics where appropriate.

Managed observability is intentionally relied upon. Business transaction correctness remains independent from telemetry export.

## 2. Managed/free tiers are finite

Do not treat current free tiers as unlimited/permanent infrastructure.

Periodically verify for each selected provider:
- ingest/quota limits;
- retention;
- alerting/account/project limits;
- region/residency where relevant;
- export/API access needed for incidents;
- behavior after quota exhaustion;
- applicable privacy/contractual requirements.

Exact current provider quotas belong to deployment/vendor data rather than timeless architecture constants.

## 3. Bounded telemetry behavior

Telemetry uses bounded queues/buffers/sampling and may degrade/drop rather than blocking committed business operations.

Bound:
- in-process telemetry buffer;
- local log/spool disk use;
- batch size;
- retry duration/attempts;
- crash/diagnostic bundle size;
- high-cardinality attributes.

If export is unavailable/quota exhausted:
- keep business/audit truth intact;
- degrade telemetry according to policy;
- expose exporter/quota health to the operator/Admin surface when implemented;
- do not busy-loop or fill local disk indefinitely.

## 4. Authoritative audit is separate

Where history is part of product correctness, keep it in SquiFlow durable state rather than only an external log provider.

Examples can include permission/Owner changes, high-risk platform actions, payment/refund/reversal evidence and rule/workflow publication.

## 5. Tenant/privacy isolation

Telemetry is another multi-tenant data path.

Required:
- tenant/context attributes only where useful/safe;
- no customer file/body/free-form content by default;
- never log secrets/tokens/passwords;
- minimize/pseudonymize sensitive identifiers where practical;
- tenant-scoped diagnostics/support views cannot leak another tenant;
- crash/diagnostic artifacts have bounded access/retention.

## 6. Root-cause-oriented signals

Do not stop at `logs exist`.

Correlate important paths as they are implemented:

```text
operation/request
→ API
→ tenant/resource/action
→ DB transaction/idempotency
→ outbox/Worker when present
→ external provider/object/native work when present
→ final result/reconciliation
```

Useful measures can include:
- request rate/error/latency by safe work class;
- DB pool use/wait/saturation;
- future Worker queue depth + oldest age;
- retry volume/budget exhaustion;
- sync pending/conflict/rejection age;
- object storage usage/capacity trend;
- transfer backlog/bandwidth;
- Workstation crash/resource anomalies;
- telemetry exporter drop/quota state.

Do not implement every metric before its component exists.

## 7. Health semantics

Separate:
- liveness: should the process restart?;
- readiness: should it receive new traffic/work?;
- degraded capability/dependency health;
- privileged operator diagnosis.

Do not expose privileged dependency details on public health endpoints.

## 8. Support-facing diagnosis

For important implemented failure classes, aim for:

```text
symptom
→ correlated evidence
→ likely failure class
→ unknowns/confidence
→ safe recovery/retry/reconciliation
```

Examples eventually include payment `OutcomeUnknown`, sync `AuthorizationChanged`, object metadata mismatch, rule publication failure, storage pressure and printing/device failures.

Do not auto-correct ambiguous money/stock/security state merely because telemetry suggests a likely cause.

## 9. Qualification

As relevant, test:
- provider unavailable/quota exhausted;
- local telemetry spool near/full;
- secret/customer-data redaction;
- cross-tenant diagnostic isolation;
- trace context across runtime boundaries that actually exist;
- alert reaches the real operator defined by the deployment model.

See `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.
