# Observability Baseline

**Version:** v0.0.15

## 1. Stable instrumentation boundary

OpenTelemetry/OTLP is the stable provider-neutral instrumentation boundary.

Current managed targets:
- New Relic free service for metrics, distributed traces and APM.
- Aiven OpenSearch free service for searchable structured operational logs.
- Backtrace for crash-oriented diagnostics where appropriate.

Managed observability is intentionally relied upon. SquiFlow does not reimplement those services merely because providers can theoretically have outages.

Business transaction correctness remains independent from telemetry export.

## 2. Free/managed tiers are capacity-limited dependencies

Do not treat a provider's current free tier as unlimited or permanent infrastructure.

Before production, record and periodically verify for each selected provider:
- ingest/quota limits;
- retention period;
- alerting limits;
- account/project limits;
- region/residency where relevant;
- export/API access needed for incident investigation;
- behavior after quota exhaustion;
- current contractual/privacy requirements.

If a free-tier limit changes, that is an operational/configuration event, not a reason for application transactions to fail.

The exact current provider limits are deployment/vendor data and should not be copied into architecture as timeless constants.

## 3. Bounded telemetry behavior

Telemetry uses bounded queues/buffers/sampling and may degrade/drop according to policy rather than blocking committed business operations.

Bound:
- in-process telemetry buffer;
- local log/spool disk usage;
- batch size;
- retry duration/attempts;
- crash dump/diagnostic bundle size;
- high-cardinality labels/attributes.

When export is unavailable or quota is exhausted:
- keep authoritative audit/business state intact;
- degrade telemetry according to policy;
- surface exporter/quota health to Platform Admin;
- do not busy-loop or fill local disk indefinitely.

## 4. Authoritative audit is separate

Authoritative security/business audit remains in SquiFlow's durable data model where transactional integrity is required.

A managed log provider is not the only copy of:
- permission/role changes;
- Owner transfer;
- high-risk platform-admin actions;
- payment/refund/reversal evidence;
- rule/workflow publication;
- other business/security events whose history is part of product correctness.

## 5. Tenant/privacy isolation

Telemetry is another multi-tenant data path.

Required:
- TenantId/context only where useful and safe;
- no customer file/content/body/free-form notes by default;
- secrets/tokens/passwords never logged;
- sensitive identifiers minimized/pseudonymized where possible;
- tenant-scoped diagnostics/support views cannot leak another tenant;
- crash dumps/attachments have stricter access/retention than normal metrics;
- custom-domain/HTTP headers are redacted/allowlisted where they can carry secrets/private data.

Cross-tenant negative tests include logs, diagnostics and support tooling, not only the business database.

## 6. Root-cause-oriented signals

Do not stop at `logs exist`.

Important operational paths should correlate:

```text
User/OperationId
→ API request
→ tenant/resource/action
→ DB transaction/idempotency
→ outbox/job
→ Worker attempt
→ external provider/object/helper
→ final result/reconciliation
```

Useful metrics include:
- request rate/error/latency by safe work class;
- DB pool usage/wait/saturation;
- Worker queue depth **and oldest age**;
- retry volume and retry budget exhaustion;
- sync pending/conflict/rejection age;
- rule evaluation latency/failures;
- object storage usage/capacity trend;
- object transfer backlog/bandwidth;
- Workstation/Guard crash/resource anomalies;
- telemetry exporter drop/quota state.

## 7. Health semantics

Separate:
- liveness: should the process be restarted?;
- readiness: should it receive new traffic/work?;
- dependency/degraded health: what capability is impaired?;
- business/support diagnostics: what does an operator need to investigate?

Do not expose privileged dependency details on a public unauthenticated health endpoint.

## 8. Support-facing diagnosis

For important failure classes, support/admin surfaces should aim to show:

```text
symptom
→ correlated evidence
→ likely failure class
→ confidence/unknowns
→ safe recovery/retry/reconciliation action
```

Examples include:
- payment `OutcomeUnknown`;
- Worker lease/no-progress failure;
- sync `AuthorizationChanged`;
- object metadata/object mismatch;
- rule publication failure;
- storage capacity pressure;
- printer/helper crash.

Self-healing is limited to deterministic low-risk recovery. Do not auto-correct ambiguous money/stock/security/rule state merely because telemetry suggests a likely cause.

## 9. Deployment qualification

Observability qualification includes:
- provider unavailable;
- provider quota exhausted;
- local telemetry spool full/near limit;
- redaction test;
- tenant-isolation diagnostic test;
- trace context survives API → outbox → Worker;
- crash report size/access/retention test;
- alert reaches the actual operator/owner defined by the operations model.

See `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md` for physical capacity/operations ownership.