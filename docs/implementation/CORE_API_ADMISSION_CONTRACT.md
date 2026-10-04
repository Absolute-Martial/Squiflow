# Core API concurrent request admission

**Owner:** `CoreApiAdmission` in the CoreApi composition root.
**Product version:** v0.0.1.
**Scope:** process-local simultaneous requests on explicitly protected endpoints.
**State:** `PRODUCTION_HONEST` for this scope; the 2026-10-01 full parallel
`eng/verify.sh` gate passed locked restore, format, Release build and 324 tests.

The ASP.NET concurrency limiter acquires one permit before tenant selection,
authentication, authorization and endpoint execution. It uses the same endpoint
access classification as the central no-store policy. One fixed partition covers
protected endpoints; public bootstrap, OpenAPI, liveness and readiness are
excluded. Client IDs, tenant candidates and credentials cannot create unbounded
limiter partitions. No queue is retained.

When no permit is available, the request returns `503` Problem Details with
`api_capacity_exceeded`, a server-generated request identifier in `traceId`, and
`Cache-Control: no-store`. It does not enter the protected handler. There is no
invented `Retry-After` or automatic retry. Retryable mutations retain their
original semantic command and idempotency key. ASP.NET owns release of the permit
when execution completes, throws or is cancelled.

## Deployment input

`Admission__MaximumConcurrentProtectedRequests` must be an integer from 1 through
256; missing/out-of-range/malformed values fail startup. Checked-in value 32 is
an initial bounded operating setting, not a throughput promise or a measured
capacity target. Qualify the deployed value against the workload, database pool,
authorization provider and replica resources before production use. Existing
dependency timeouts and body/page limits still apply. Native ASP.NET rate-limit
metrics are available to a later owned exporter; this change does not introduce
an exporter or alerting pipeline.

This is not a per-tenant entitlement, rate-per-minute limit, shared cross-replica
quota, fairness guarantee, authorization decision, hard CPU/memory isolation or
transport-level denial-of-service control. Replicas have independent permits.
Tenant-aware resource fairness and edge/transport policy remain separately owned
work. Public endpoint exclusion does not qualify public traffic protection.

## Regression and requalification

`AdmissionTests` drives the real host with a deterministic account-lookup gate.
It proves immediate rejection before handler entry at capacity, public health
and bootstrap availability, preserved public caching, and permit recovery after
success, exception and cancellation. It also proves startup rejects unsafe
configuration. OpenAPI describes `503` for protected endpoints.
`eng/verify.sh` is the permanent gate; qualification requires that gate to pass.

Requalify after middleware ordering, endpoint classification, ASP.NET limiter
major version, queue policy, deployment permit value or replica topology changes.
The source reference is the native [.NET rate limiting middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).
No custom scheduler, broker, cache, container-per-request or runtime framework is
introduced.

## Verified-tenant concurrency boundary (2026-10-03)

A second native `ConcurrencyLimiter` bounds simultaneous work for each verified
tenant within this process. `Admission__MaximumConcurrentRequestsPerTenant` is a
required integer from 1–256, initially 8. Account lookup and current membership
run under the outer global limit; only the resulting `TenantContext.TenantId`
can allocate a tenant partition. OpenFGA/business work runs after admission.
No wait queue or client-controlled candidate partition is retained.

The registry retains only partitions with active leases and has an explicit
partition ceiling equal to the configured global cap. That independent ceiling
is necessary because request-scoped disposal can briefly follow the outer
middleware's permit release. Acquisition/removal are synchronized; the final
lease releases before its native limiter is removed and disposed. Request DI
releases on success, exception and cancellation; host shutdown disposes remaining
limiters and permits late idempotent lease disposal.

Rejected tenant work returns no-store `429` / `tenant_capacity_exceeded` with a
request trace ID and no fabricated Retry-After. Public bootstrap caching and
health routes remain unchanged. Existing HTTP diagnostics can distinguish these
status/problem outcomes; no exporter, tenant-label metric cardinality, durable
usage meter or quota ledger is introduced.

`TenantAdmissionTests` exercises the real host with gated authorization: another
account in the same tenant is rejected before OpenFGA, another tenant progresses,
and completion/failure/cancellation release capacity. It also verifies missing
membership creates no partitions, the registry bound, shutdown/late disposal,
startup validation and OpenAPI 429 responses. These regressions complement
`AdmissionTests` in `eng/verify.sh`.

This cap does not reserve capacity, guarantee fairness, limit membership lookups,
provide hard CPU/memory isolation, meter usage or coordinate replicas. If its
value exceeds the outer cap, the outer cap dominates. Qualify both settings
against real database/authorization capacity. Source admission uses the existing
ASP.NET/.NET limiter APIs; the registry adds only verified-key retention and the
request-owned lease lifecycle.
