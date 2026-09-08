# Observability Baseline

**Version:** v0.0.15

OpenTelemetry/OTLP is the stable provider-neutral instrumentation boundary.

Current managed targets:
- New Relic free service for metrics, distributed traces and APM.
- Aiven OpenSearch free service for searchable structured operational logs.
- Backtrace for crash-oriented diagnostics where appropriate.

Managed observability is intentionally relied upon. SquiFlow does not reimplement those services merely because providers can theoretically have outages.

Business transaction correctness remains independent from telemetry export. Telemetry uses bounded queues/buffers/sampling and may degrade/drop according to policy rather than blocking committed business operations.

Authoritative security/business audit remains in SquiFlow's durable data model where transactional integrity is required.
