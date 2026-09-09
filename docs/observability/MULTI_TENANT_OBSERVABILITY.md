# Multi-Tenant Observability

**Version:** v0.0.16  
**Status:** Accepted implementation direction

## 1. Principle

Observability is a multi-tenant data path and must follow the same isolation/privacy discipline as application data.

Tenant context is useful for incident investigation, but blindly attaching tenant/user/entity identifiers to every metric, log and trace creates privacy, cost and cardinality problems.

## 2. Where tenant identity belongs

### Logs/traces
A verified opaque TenantId may be included when it materially improves diagnosis and policy allows it.

Use opaque IDs rather than customer names/contact details whenever IDs are sufficient.

### Metrics
TenantId is **not** an ordinary metric label.

Do not create unbounded series such as:

```text
http_requests_total{tenant_id="every-tenant"}
```

If later product requirements need per-tenant SLO/billing/resource accounting, design a bounded aggregation/accounting path intentionally.

## 3. Verified context only

Telemetry enrichment must use the authoritative runtime TenantContext, not a request-body tenant ID supplied by an untrusted client.

```text
authenticated principal
→ tenant membership/context resolution
→ authorization/data isolation
→ telemetry enrichment
```

Telemetry must not become a side channel allowing a client to spoof another tenant into logs/traces.

## 4. Support/admin access

Tenant-scoped support surfaces must not leak another tenant’s logs, traces, bundles or identifiers.

Cross-tenant/platform support access requires a deliberate privileged workflow with:
- authenticated operator;
- scope/purpose;
- bounded duration where appropriate;
- audit trail;
- least privilege;
- no provider-wide credentials exposed to tenant administrators.

## 5. Diagnostic bundles

A tenant/user support bundle contains only the evidence authorized for that support operation.

Do not automatically include:
- full local/central database;
- arbitrary customer documents;
- unrelated tenant traces/logs;
- authentication tokens/secrets;
- unrestricted machine-wide files.

## 6. Offboarding/deletion

Tenant deletion/export/offboarding policy must include observability artifacts where applicable:
- external operational logs/traces;
- local/remote diagnostic bundles;
- crash artifacts;
- archived operational telemetry.

Legally/product-required audit evidence is handled separately from disposable operational telemetry and follows its own retention rules.

## 7. Noisy-tenant protection

One tenant’s workload should not make observability unusable for everyone else.

Apply bounded policies such as:
- trace/log sampling;
- per-work-class limits;
- collector/export queue limits;
- diagnostic bundle limits;
- alert grouping/rate controls.

Do not silently drop authoritative business/audit history as a noisy-tenant mitigation.

## 8. Tenant-aware root cause

When investigating a specific operation, correlation may use controlled high-cardinality fields in logs/traces:

```text
TenantId
CorrelationId
OperationId / IdempotencyKey
WorkstationId
SyncChangeId / JobId
TraceId
```

These enable precise investigation without turning them into global metric dimensions.

## 9. Acceptance

Test:
- Tenant A cannot view Tenant B support telemetry;
- spoofed request tenant IDs cannot poison authoritative telemetry context;
- many tenants do not create unbounded metric series;
- one noisy tenant cannot fill the Workstation/server telemetry budget indefinitely;
- offboarding/retention behavior is documented and executable;
- privileged support access is auditable.
