# Observability Verification and Acceptance

**Version:** v0.1.0
**Status:** Required verification contract

Observability is a runtime capability with correctness, privacy, resource, and failure behavior. Package installation or a dashboard screenshot is not acceptance.

## 1. Correlation tests

Prove correlation through representative paths:

```text
Workstation local change
→ sync upload
→ API/auth/tenant resolution
→ DB transaction/idempotency
→ outbox/worker when present
→ acknowledgement/reconciliation
```

Assert the appropriate TraceId plus stable SquiFlow CorrelationId/CausationId/operation identifiers are preserved without being treated as authorization authority.

## 2. Structured-event tests

Verify:
- EventId uniqueness;
- EventName uniqueness;
- FailureCode uniqueness;
- retired identifiers are not silently reused;
- message-text changes do not break queries/tests based on stable identifiers;
- state transitions emit the expected semantic event;
- exception text is evidence, not the only failure identity.

## 3. Redaction/privacy tests

Inject representative secrets/PII into:
- headers;
- query/body models;
- exception messages;
- configuration;
- external-provider errors;
- activity tags/log scopes.

Prove that tokens/passwords/keys/payment-sensitive data/unrestricted customer content do not leave through the normal external telemetry pipeline.

Run cross-tenant negative tests for support/diagnostic views and bundles.

## 4. Cardinality tests

Review/automate checks so ordinary metric labels do not include unbounded identifiers such as TenantId, UserId, WorkstationId, entity/job/correlation/trace IDs.

Load tests must show stable series growth under many tenants/entities/requests.

## 5. Sampling tests

Verify:
- high-volume success is sampled according to policy;
- errors/slow paths receive stronger capture;
- sampling never affects business/audit truth;
- reported sampling/drop ratios are themselves observable;
- provider/collector changes cannot silently enable 100% expensive tracing without explicit configuration review.

## 6. Provider/collector outage

Inject:
- OTLP collector unavailable;
- exporter timeout;
- provider rejects/429/quota exhaustion;
- DNS/network failure;
- collector queue saturation.

Expected:
- committed business transactions remain committed;
- bounded retry/buffer/drop behavior;
- no unbounded memory/disk growth;
- exporter/drop health becomes observable when the path recovers or through local/operator evidence.

## 7. Workstation offline/local evidence

Test:
- long network outage;
- application restart while offline;
- Guard restart while offline;
- crash with remote telemetry unavailable;
- local log rotation/expiration;
- upload/export recovery when connectivity returns.

Business/local DB durability must not depend on telemetry export.

## 8. Disk-pressure tests

Drive the Workstation diagnostic area toward/full.

Expected:
- low-value telemetry is shed first;
- recent Error/Critical + crash/recovery evidence receives priority according to policy;
- no busy loop;
- SQLite/OS/update reserve is protected;
- diagnostics pressure is visible;
- local business correctness is unaffected.

## 9. Diagnostic-bundle tests

Prove:
- bounded time window/size;
- manifest of contents;
- configuration is redacted;
- no raw tokens/passwords/keys;
- no unrestricted DB/customer file dump by default;
- tenant/support authorization is enforced;
- failed upload does not delete the only local bundle before retention policy permits it.

## 10. Time/clock tests

Verify:
- timestamps use UTC;
- elapsed durations use monotonic timing;
- sleep/hibernate/clock jumps do not create negative/absurd durations;
- severe Workstation clock skew is detectable;
- client wall clock cannot define server ordering/idempotency correctness.

## 11. Alert-storm tests

Simulate:
- repeated crash/restart loop;
- shared provider outage;
- large tenant sync failure;
- DB pool saturation.

Expected:
- alerts group/deduplicate/rate-limit;
- operator still receives the important incident signal;
- alert/log generation does not materially worsen resource pressure.

## 12. Root-cause quality tests

For selected incidents, verify support can distinguish at least:
- authentication/session failure vs identity-provider outage;
- explicit authorization deny vs OpenFGA dependency error;
- business conflict vs DB failure;
- server-applied sync operation with lost ACK vs genuinely rejected operation;
- resource exhaustion vs application exception;
- object metadata mismatch vs storage-provider outage.

Do not assert a fabricated root cause when evidence is insufficient; record unknowns/confidence.

## 13. Resource-budget regression

Measure observability overhead on representative low-end server and Workstation hardware:
- idle RSS/working set;
- active RSS/working set;
- allocation/GC impact;
- CPU;
- startup impact;
- disk-write rate;
- local telemetry disk footprint;
- network egress/ingest volume.

Resource budgets are release properties, not one-time POC observations.

## 14. Acceptance gate

A vertical slice passes the observability gate only when:
1. correlated structured logs/traces/metrics exist at the meaningful boundaries;
2. stable events/failure codes are in use;
3. privacy/cardinality rules pass hostile tests;
4. telemetry outage cannot break business correctness;
5. Workstation remains diagnosable offline;
6. diagnostic evidence is bounded;
7. pipeline self-health is observable;
8. resource overhead remains within approved budgets.
