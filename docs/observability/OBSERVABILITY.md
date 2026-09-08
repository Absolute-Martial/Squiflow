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

Periodically verify:
- ingest/quota limits;
- retention;
- alerting/account/project limits;
- region/residency where relevant;
- export/API access needed for incidents;
- behavior after quota exhaustion;
- applicable privacy/contractual requirements.

Exact provider quotas belong to deployment/vendor data rather than timeless architecture constants.

## 3. Bounded telemetry behavior

Telemetry uses bounded queues/buffers/sampling and may degrade/drop rather than blocking committed business operations.

Bound:
- in-process telemetry buffer;
- local log/spool disk use;
- batch size;
- retry duration/attempts;
- Guard/Workstation crash/diagnostic bundle size;
- high-cardinality attributes.

If export is unavailable/quota exhausted:
- keep business/audit truth intact;
- degrade telemetry according to policy;
- expose exporter/quota health to operator/Admin surfaces when implemented;
- do not busy-loop or fill local disk indefinitely.

## 4. Authoritative audit is separate

Where history is part of product correctness, keep it in SquiFlow durable state rather than only an external log provider.

Examples include:
- tenant role/permission change requests and OpenFGA application/reconciliation outcome;
- Owner transfer;
- high-risk platform actions;
- payment/refund/reversal evidence;
- rule/workflow publication.

ZITADEL/OpenFGA provider logs are not the only SquiFlow audit copy where SquiFlow business/security history is required.

## 5. Tenant/privacy isolation

Telemetry is another multi-tenant data path.

Required:
- tenant/context attributes only where useful/safe;
- no customer file/body/free-form content by default;
- never log secrets/tokens/passwords;
- minimize/pseudonymize sensitive identifiers where practical;
- OpenFGA tuple identifiers use opaque IDs rather than PII;
- tenant-scoped diagnostics/support views cannot leak another tenant;
- Guard crash/diagnostic artifacts have bounded access/retention and do not automatically package unrestricted customer data.

## 6. Root-cause-oriented signals

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

Useful measures can include:
- request rate/error/latency by safe work class;
- ZITADEL auth/session dependency failures/latency without logging tokens;
- OpenFGA check latency/error by safe operation class;
- OpenFGA authorization-change reconciliation backlog/age;
- DB pool use/wait/saturation;
- future Worker queue depth + oldest age;
- retry volume/budget exhaustion;
- sync pending/conflict/rejection age;
- Hugging Face object usage/capacity trend;
- transfer backlog/bandwidth;
- Guard restart/crash/hang-detection/safe-mode events;
- Workstation process-tree resource anomalies;
- telemetry exporter drop/quota state.

Do not implement every metric before its component exists, but do not omit evidence needed to distinguish provider outage, authorization failure, business conflict, and process failure.

## 7. Guard observability contract

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

If network/telemetry is unavailable, Guard must still be able to supervise/recover locally.

## 8. Identity/authorization dependency health

Dependency failure must be diagnosable without becoming accidental authorization success.

Distinguish:
- ZITADEL unreachable versus invalid/expired user session;
- OpenFGA unreachable versus explicit deny;
- OpenFGA model/config mismatch versus tuple absence;
- stale/lower-consistency concern versus higher-consistency request failure;
- authorization-change `OutcomeUnknown`/reconciliation from normal permission denial.

Do not expose those privileged/internal details to ordinary unauthenticated clients; user-facing errors remain safe/stable.

## 9. Health semantics

Separate:
- liveness: should the process restart?;
- readiness: should it receive new traffic/work?;
- degraded dependency/capability health;
- privileged operator diagnosis.

Example: OpenFGA being unavailable might make authorization-dependent Core API operations not ready/degraded, while Guard/Workstation local-capable work can remain locally usable. Define per capability rather than one global green/red flag.

## 10. Support-facing diagnosis

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
- sync `AuthorizationChanged`;
- payment `OutcomeUnknown`;
- object metadata mismatch;
- storage pressure;
- Guard crash loop/update recovery;
- printing/device failure.

Do not auto-correct ambiguous money/stock/security state merely because telemetry suggests a likely cause.

## 11. Qualification

As relevant, test:
- observability provider unavailable/quota exhausted;
- local telemetry/Guard diagnostic spool near/full;
- secret/customer-data redaction;
- cross-tenant diagnostic isolation;
- trace/correlation across ZITADEL-session → API → OpenFGA → DB paths without leaking tokens;
- Guard recovery while remote observability is unavailable;
- alert reaches the actual operator defined by the deployment model.

See `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.
