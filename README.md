# SquiFlow

**Current architecture/documentation version: `v0.0.15`**

Start with [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md).

> **Current implementation state:** pre-Phase-0. This repository currently contains the curated architecture/implementation baseline; the target `apps/`, `services/`, `modules/`, provider adapters and executable test/CI structure are not yet implemented merely because the docs describe them.

The repository is intentionally curated: current implementation-relevant design stays directly browsable instead of committing hundreds of generated/historical files.

## Current baseline

- C# / modern .NET.
- **Avalonia** for Windows Workstation UI.
- **Blazor Web App** for tenant Web and separate Platform Admin Web projects/security surfaces.
- ASP.NET Core Core API and a separate durable Worker runtime.
- Small-team-first tenant model: Owner + Staff by default, with tenant-owned granular permissions.
- Role/permission assignment and tenant rules/workflow/forms are Web-administered; Desktop consumes published authority/config but never grants it.
- Platform-critical application control operations are Platform-Admin-Web only during normal operation; physical/application-control-plane failure uses a separate private infrastructure recovery runbook.
- Ordinary tenants use pooled shared application compute + pooled authoritative tenant data with explicit tenant discriminators; dedicated data/stack isolation is evidence-driven, not baseline.
- Authentication, authorization and tenant isolation are separate concerns.
- Workstation is the local-first/offline client; hosted Web is online-only for business operations in v0.0.15.
- Valuable Web forms can use online server-side drafts without introducing browser offline sync.
- Workstation login uses system-browser OpenID Connect Authorization Code + PKCE.
- `SquiFlow.Guard` is a tiny lifecycle/recovery companion; printing is the baseline physical-device integration.
- Retryable mutations use semantic idempotency keys; at-least-once Worker/message delivery is handled through idempotent/reconcilable effects.
- PostgreSQL is a central reference candidate; SQLite + WAL and libSQL are local-store candidates. Exact persistence products remain open until POC gates are met.
- SquiFlow-native bounded rule engine; local evaluation respects fact authority/freshness.
- Current object-storage envelope is approximately **100 GB** and is treated as a real capacity constraint; primary object storage is not assumed to be its own only backup.
- Current server environment is lower-spec/desktop-class rack hardware; `stateless` does not mean automatic failover or zero downtime.
- Accessibility is a release-level requirement across implemented Web/Admin/Workstation journeys.
- OpenTelemetry remains provider-neutral instrumentation; New Relic + Aiven OpenSearch are current managed targets and Backtrace is the crash-diagnostics direction. Managed/free tiers are capacity-limited dependencies.
- Full Web offline sync, Kafka, mandatory Redis, global CRDTs, event-sourced/CQRS/Saga core architecture, per-tenant infrastructure and microservice-per-module design are not baseline.

## Documentation map

### Start here
- [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md) — consolidated current implementation plan and planning-stop rule.
- [`docs/decisions/CURRENT_DECISIONS.md`](docs/decisions/CURRENT_DECISIONS.md) — accepted current direction.
- [`docs/decisions/OPEN_DECISIONS.md`](docs/decisions/OPEN_DECISIONS.md) — genuinely unresolved/load-bearing decisions.
- [`docs/implementation/PHASES_AND_GATES.md`](docs/implementation/PHASES_AND_GATES.md) — sequential WIP-limited implementation phases and phase-start decision gates.

### Architecture/runtime/operations
- [`docs/architecture/REPOSITORY_STRUCTURE.md`](docs/architecture/REPOSITORY_STRUCTURE.md)
- [`docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`](docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md)
- [`docs/architecture/MULTI_TENANCY_ISOLATION.md`](docs/architecture/MULTI_TENANCY_ISOLATION.md)
- [`docs/server/CORE_API_AND_WORKER.md`](docs/server/CORE_API_AND_WORKER.md)
- [`docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`](docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md)
- [`docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`](docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md)

### Business/domain
- [`docs/domain/BUSINESS_MODEL.md`](docs/domain/BUSINESS_MODEL.md)
- [`docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`](docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md)
- [`docs/workflow/WORKFLOW_DESIGN.md`](docs/workflow/WORKFLOW_DESIGN.md)
- [`docs/rules/NATIVE_RULE_ENGINE.md`](docs/rules/NATIVE_RULE_ENGINE.md)

### Workstation/sync/devices
- [`docs/workstation/LOCAL_FIRST_DESKTOP.md`](docs/workstation/LOCAL_FIRST_DESKTOP.md)
- [`docs/workstation/GUARD_AND_DEVICE_INTEGRATION.md`](docs/workstation/GUARD_AND_DEVICE_INTEGRATION.md)
- [`docs/sync/SYNC_AND_AUTHORITY.md`](docs/sync/SYNC_AND_AUTHORITY.md)

### Security/admin/identity
- [`docs/security/TENANT_PERMISSIONS.md`](docs/security/TENANT_PERMISSIONS.md)
- [`docs/security/IDENTITY_AND_SESSIONS.md`](docs/security/IDENTITY_AND_SESSIONS.md)
- [`docs/admin/ADMIN_SURFACES.md`](docs/admin/ADMIN_SURFACES.md)

### Web/UX
- [`docs/web/WEB_RUNTIME_AND_STORAGE.md`](docs/web/WEB_RUNTIME_AND_STORAGE.md)
- [`docs/web/CUSTOM_DOMAINS.md`](docs/web/CUSTOM_DOMAINS.md)
- [`docs/ux/ACCESSIBILITY_AND_INTERACTION_QUALITY.md`](docs/ux/ACCESSIBILITY_AND_INTERACTION_QUALITY.md)

### Data/integrations/observability
- [`docs/data/PERSISTENCE_SELECTION.md`](docs/data/PERSISTENCE_SELECTION.md)
- [`docs/data/FILES_AND_OBJECT_STORAGE.md`](docs/data/FILES_AND_OBJECT_STORAGE.md)
- [`docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md`](docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md)
- [`docs/observability/OBSERVABILITY.md`](docs/observability/OBSERVABILITY.md)

### Testing/review
- [`docs/testing/VERIFICATION_STRATEGY.md`](docs/testing/VERIFICATION_STRATEGY.md)
- [`docs/review/COMPREHENSIVE_ADVERSARIAL_GAP_REVIEW.md`](docs/review/COMPREHENSIVE_ADVERSARIAL_GAP_REVIEW.md)
- [`docs/review/SKEPTICAL_IMPLEMENTATION_GATES.md`](docs/review/SKEPTICAL_IMPLEMENTATION_GATES.md)
- [`docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md`](docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md)
- [`docs/review/RELIABILITY_API_AND_PATTERN_SOURCE_REVIEW.md`](docs/review/RELIABILITY_API_AND_PATTERN_SOURCE_REVIEW.md)
- [`docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md`](docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md)

## Documentation/source rule

Old generated CSV inventory/review ledgers are not architecture authority. Markdown can drift too, so each topic has one focused owner document; the master/current-decision files summarize and link rather than creating independent conflicting specifications.

## Versioning

The current baseline remains `v0.0.15`. Architecture corrections inside this baseline update `VERSION`/`CURRENT_VERSION.txt` only when the actual semantic version changes; this review does not manufacture a new version number just because documentation became more complete.
