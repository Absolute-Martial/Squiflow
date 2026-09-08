# SquiFlow

**Current architecture/documentation version: `v0.0.15`**

Start with [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md).

The GitLab repository is intentionally curated: it keeps the current implementation-relevant architecture directly browsable instead of committing hundreds of generated/historical files.

## Current baseline

- Small-team-first tenant model: Owner + Staff by default, with tenant-owned granular permissions.
- Role/permission assignment is Web-only through tenant administration.
- Tenant rules/workflow/stages are authored/published through Web administration.
- Platform-critical server/Worker/control-plane actions are Web-only through the separate Platform Admin application.
- ASP.NET Core Core API and Worker are separate runtime/deployment boundaries.
- Windows Workstation is the local-first/offline client.
- Hosted Web is online-only for business operations in v0.0.15; browser partial-offline business sync is deferred.
- Workstation login uses the system browser against a canonical SquiFlow identity authority with authorization-code + PKCE semantics.
- Tenant custom domains are supported with ownership verification, TLS lifecycle, audit and fallback.
- ASP.NET Core policy/resource authorization is the runtime primitive; SquiFlow does not add a separate authorization service baseline.
- Retryable mutating commands use semantic caller-provided idempotency keys; same key + different intent is rejected.
- At-least-once Worker/message delivery is assumed and handled with idempotent/reconcilable effects.
- Long-running HTTP operations use durable asynchronous status instead of holding request threads indefinitely.
- Retry is finite, classified, budgeted and uses backoff/jitter where appropriate; retry storms are treated as a failure mode.
- Central/local persistence products remain open: PostgreSQL/SQLite are reference candidates and libSQL is an explicit local-store candidate.
- SquiFlow-native bounded rule engine is baseline.
- OpenTelemetry remains provider-neutral instrumentation; New Relic + Aiven OpenSearch are current managed observability targets; Backtrace is the crash-diagnostics direction.
- Server application nodes remain stateless/disposable for authoritative business state.
- Full CQRS/event sourcing/Saga/BFF/sharding/leader-election/multi-region patterns are not baseline merely because they exist in architecture catalogs.

## Documentation map

### Start here
- [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md) — consolidated current implementation plan.
- [`docs/decisions/CURRENT_DECISIONS.md`](docs/decisions/CURRENT_DECISIONS.md) — current locked direction.
- [`docs/decisions/OPEN_DECISIONS.md`](docs/decisions/OPEN_DECISIONS.md) — only decisions that still affect the current baseline.
- [`docs/implementation/PHASES_AND_GATES.md`](docs/implementation/PHASES_AND_GATES.md) — sequential implementation order.

### Architecture/runtime
- [`docs/architecture/REPOSITORY_STRUCTURE.md`](docs/architecture/REPOSITORY_STRUCTURE.md)
- [`docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`](docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md)
- [`docs/server/CORE_API_AND_WORKER.md`](docs/server/CORE_API_AND_WORKER.md)
- [`docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`](docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md)

### Business/domain
- [`docs/domain/BUSINESS_MODEL.md`](docs/domain/BUSINESS_MODEL.md)
- [`docs/workflow/WORKFLOW_DESIGN.md`](docs/workflow/WORKFLOW_DESIGN.md)
- [`docs/rules/NATIVE_RULE_ENGINE.md`](docs/rules/NATIVE_RULE_ENGINE.md)

### Workstation/sync
- [`docs/workstation/LOCAL_FIRST_DESKTOP.md`](docs/workstation/LOCAL_FIRST_DESKTOP.md)
- [`docs/sync/SYNC_AND_AUTHORITY.md`](docs/sync/SYNC_AND_AUTHORITY.md)

### Security/admin/identity
- [`docs/security/TENANT_PERMISSIONS.md`](docs/security/TENANT_PERMISSIONS.md)
- [`docs/security/IDENTITY_AND_SESSIONS.md`](docs/security/IDENTITY_AND_SESSIONS.md)
- [`docs/admin/ADMIN_SURFACES.md`](docs/admin/ADMIN_SURFACES.md)

### Web/custom domains
- [`docs/web/WEB_RUNTIME_AND_STORAGE.md`](docs/web/WEB_RUNTIME_AND_STORAGE.md)
- [`docs/web/CUSTOM_DOMAINS.md`](docs/web/CUSTOM_DOMAINS.md)

### Data/operations
- [`docs/data/PERSISTENCE_SELECTION.md`](docs/data/PERSISTENCE_SELECTION.md)
- [`docs/data/FILES_AND_OBJECT_STORAGE.md`](docs/data/FILES_AND_OBJECT_STORAGE.md)
- [`docs/observability/OBSERVABILITY.md`](docs/observability/OBSERVABILITY.md)

### Review/quality
- [`docs/review/SKEPTICAL_IMPLEMENTATION_GATES.md`](docs/review/SKEPTICAL_IMPLEMENTATION_GATES.md)
- [`docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md`](docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md)
- [`docs/review/RELIABILITY_API_AND_PATTERN_SOURCE_REVIEW.md`](docs/review/RELIABILITY_API_AND_PATTERN_SOURCE_REVIEW.md)
- [`docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md`](docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md)

## Why the old CSVs are not here

Earlier ZIPs contained generated CSV inventory/review ledgers. They became stale and could encode old classifications as if they were decisions. GitLab now keeps semantic current decisions in Markdown; machine inventory/hash reports can be generated by CI when needed.

## Versioning

The current baseline is `v0.0.15`. Architecture changes update `VERSION`, `CURRENT_VERSION.txt`, this README, the master plan and decision records together.
