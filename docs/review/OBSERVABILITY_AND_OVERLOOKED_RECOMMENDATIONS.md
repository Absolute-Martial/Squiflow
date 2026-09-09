# Observability Review and Overlooked Recommendations

**Version:** v0.0.16  
**Status:** Review findings. Items explicitly marked Accepted are part of the current implementation direction; Recommendation/Open items are not silently promoted to accepted product behavior.

The architecture is now stronger on observability. The remaining risk is mostly implementation ambiguity and operational maturity rather than missing high-level boxes.

## P0 — foundation / first vertical slice

### Stable event/failure registry
**Status:** Accepted.

Use source-controlled EventId/EventName/FailureCode identifiers with uniqueness/compatibility tests and stable semantic meaning across releases.

### Time semantics for telemetry
**Status:** Accepted for telemetry; broader business-time policy remains Open.

Use UTC wall time, monotonic duration timing and server authority for distributed ordering. Detect severe Workstation clock skew.

### Workstation diagnostic disk reserve
**Status:** Accepted.

Logging/bundles must shed low-value data before diagnostics endanger SQLite, OS operation or update recovery.

### Telemetry self-observability
**Status:** Recommendation.

Track dropped logs/spans, collector queue fill, exporter failure, spool pressure, sampling ratios and processor/redaction failure where measurable.

### Alert storm control
**Status:** Recommendation.

Group/deduplicate/rate-limit alerts from crash loops, shared outages and wide sync failures so incident telemetry does not amplify the incident.

### Runbook linkage
**Status:** Recommendation.

High-value FailureCodes/alerts should identify an owner plus safe recovery/reconciliation runbook.

### Unknown-outcome evidence
**Status:** Accepted architecture; implementation detail still needs proof.

Sync/external jobs with ambiguous timeout require durable idempotency/receipt/reconciliation evidence. Logging alone is never the receipt.

## P1 — before broad production

### Money, rounding, currency and unit policy
**Status:** Recommendation / Open discovery.

Before freezing invoices/payments/quotations define:
- decimal precision;
- rounding mode;
- line-vs-document rounding;
- quantity/unit precision for continuous materials;
- tax-included/excluded behavior;
- immutable issued-document totals;
- any future multi-currency/FX semantics before enabling them.

### Tenant/business timezone policy
**Status:** Recommendation / Open.

Store instants in UTC, but explicitly define the business timezone controlling business-day boundaries, quotation expiry, scheduled jobs, reports, invoice dates and audit display.

### Restore qualification
**Status:** Architecture accepted; numeric RPO/RTO remains Open.

A backup is not proven until download, integrity verification and restore have been exercised. Production profiles should eventually define RPO/RTO and recurring restore drills.

### Update/signing trust chain
**Status:** Open/high priority.

Windows installer/update authenticity, signed metadata/binaries, staged rollout and rollback are a primary security boundary before customer rollout.

### Long-offline sync qualification
**Status:** Accepted requirement; technology choice remains evidence-driven.

Any sync engine/POC must prove months-old clients, retained pending local intent, schema/protocol/rule/config evolution, ambiguous failures and safe resnapshot/rebase behavior—not only happy-path live sync.

### Framework choice by vertical-slice measurement
**Status:** Recommendation.

Resolve ABP/Orchard/custom-runtime decisions through a representative slice measuring startup/RSS, dependency graph, tenancy/security fit, module lifecycle, trimming/AOT impact, upgrade burden and implementation clarity.

### Process isolation stays evidence-driven
**Status:** Accepted principle.

Document/native/heavy import workloads are strong isolation candidates, but do not pre-split every feature/process without measured fault/resource value.

## P1/P2 — operational maturity

### Tenant data-placement migration runbook
**Status:** Recommendation.

If SquiFlow later supports shared/isolated/dedicated placement, define backup, quiesce/capture, copy, validation, cutover, rollback and observability without changing domain semantics.

### Provider quota/cost metadata
**Status:** Accepted concept; exact values Open.

Free-tier/pricing numbers change. Keep them out of timeless application logic and record verified provider limits/dates in deployment operations data.

### Observability offboarding/deletion
**Status:** Recommendation / partially covered.

Tenant deletion/export/offboarding must include operational logs/traces/bundles where policy requires it while preserving legally/product-required audit history separately.

### External side-effect registry
**Status:** Recommendation.

For payment/email/ERP/storage integrations document provider idempotency, request/receipt identity, timeout meaning, reconciliation API/process, retry owner and duplicate-side-effect risk.

### Resource-budget regression
**Status:** Accepted architecture; release-process recommendation.

Track Workstation/server idle/active RSS, startup, disk I/O, telemetry overhead and heavy-job peaks across releases. Material regression requires explicit budget review rather than being accepted because functional tests pass.

### Support/operator access model
**Status:** Recommendation / partially covered.

Formalize support access to tenant diagnostics with scope, purpose, expiry/least privilege and audit. Avoid indefinite cross-tenant support access and provider-wide credentials for tenant admins.

## Deliberately not recommended

Do not add these solely because they could solve a hypothetical future problem:
- Kafka;
- YugabyteDB;
- Kubernetes;
- mandatory Redis/RabbitMQ;
- a NoSQL database;
- observability microservice;
- per-tenant infrastructure by default;
- full event sourcing;
- 100% production tracing.

Each remains evidence/workload driven.
