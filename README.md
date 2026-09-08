# SquiFlow

**Current architecture/documentation version: `v0.0.15`**

Start with [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md).

> **Implementation state:** pre-Phase-0. The repository currently contains the audited architecture/implementation baseline. A directory described in a document is not implemented merely because it appears in a target tree.

## Current baseline

- C# / modern .NET.
- Avalonia for the Windows Workstation.
- Blazor Web App for tenant Web and the future separate Platform Admin Web.
- ASP.NET Core Core API.
- Modular-monolith business code; network/process boundaries are added only when they solve a real deployment/fault/security problem.
- Small-team-first tenant model: Owner + Staff by default, with Owner-controlled granular permissions.
- Tenant role/permission and rule/workflow/form administration is Web-only; Desktop consumes published authority/configuration and never grants it.
- Platform-critical application controls belong to the separate Platform Admin surface when that surface is implemented.
- Workstation is local-first/offline; Web is online-only for business operations in v0.0.15.
- Workstation login uses system-browser OpenID Connect Authorization Code + PKCE.
- Pooled multi-tenancy is the ordinary baseline; tenant isolation is separate from authentication/authorization.
- Retryable mutations use semantic idempotency keys; at-least-once delivery is handled by idempotent/reconcilable effects.
- PostgreSQL is the strongest central reference candidate; SQLite + WAL and libSQL are Workstation-store candidates. Exact DB products remain open until their phase POCs.
- Currency is configurable/not hardcoded. v0.0.15 does not build a multi-currency/FX subsystem.
- SquiFlow-native bounded rules/workflow remain in-process capabilities unless real isolation/scale proves otherwise.
- **Primary bootstrap object storage:** private Hugging Face Storage Bucket, with the current private-storage envelope of about 100 GB treated as finite.
- **Bootstrap off-site backup carrier:** private Kaggle Dataset containing encrypted opaque backup archives only.
- Planned object/backup-provider migration trigger: the first paying customer, or earlier if capacity, privacy/compliance, reliability or restore requirements demand it.
- Current server environment is lower-spec/desktop-class rack hardware; `stateless` does not mean automatic failover or zero downtime.
- OpenTelemetry remains the provider-neutral telemetry boundary; New Relic + Aiven OpenSearch are current managed targets and Backtrace remains the crash-diagnostics direction.

## Lean implementation rule

Do not create infrastructure or abstractions merely because they might become useful later.

In particular, v0.0.15 does **not** require:

```text
SquiFlow.Guard
one-interface-per-class
IRepository<T> / IUnitOfWork
provider-wrapper hierarchy
packages/ or contracts/ dumping grounds
an empty Worker project before Worker work exists
an empty Platform Admin project before platform-control UI exists
```

Start concrete and contained. Extract an interface/project/process only when a real dependency-inversion, multiple-production-implementation, stable wire/process contract, or fault-isolation need earns it.

## Documentation map

### Start here
- [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md) — current implementation plan.
- [`docs/review/DECISION_AUDIT.md`](docs/review/DECISION_AUDIT.md) — current decision audit: KEEP / SIMPLIFY / DEFER / REMOVE / OPEN.
- [`docs/decisions/CURRENT_DECISIONS.md`](docs/decisions/CURRENT_DECISIONS.md) — accepted current direction.
- [`docs/decisions/OPEN_DECISIONS.md`](docs/decisions/OPEN_DECISIONS.md) — genuinely unresolved/load-bearing decisions.
- [`docs/implementation/PHASES_AND_GATES.md`](docs/implementation/PHASES_AND_GATES.md) — sequential implementation order.

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

### Workstation/sync
- [`docs/workstation/LOCAL_FIRST_DESKTOP.md`](docs/workstation/LOCAL_FIRST_DESKTOP.md)
- [`docs/sync/SYNC_AND_AUTHORITY.md`](docs/sync/SYNC_AND_AUTHORITY.md)

### Security/admin/identity
- [`docs/security/TENANT_PERMISSIONS.md`](docs/security/TENANT_PERMISSIONS.md)
- [`docs/security/IDENTITY_AND_SESSIONS.md`](docs/security/IDENTITY_AND_SESSIONS.md)
- [`docs/admin/ADMIN_SURFACES.md`](docs/admin/ADMIN_SURFACES.md)

### Web/data/integrations
- [`docs/web/WEB_RUNTIME_AND_STORAGE.md`](docs/web/WEB_RUNTIME_AND_STORAGE.md)
- [`docs/web/CUSTOM_DOMAINS.md`](docs/web/CUSTOM_DOMAINS.md)
- [`docs/data/PERSISTENCE_SELECTION.md`](docs/data/PERSISTENCE_SELECTION.md)
- [`docs/data/FILES_AND_OBJECT_STORAGE.md`](docs/data/FILES_AND_OBJECT_STORAGE.md)
- [`docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md`](docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md)
- [`docs/observability/OBSERVABILITY.md`](docs/observability/OBSERVABILITY.md)

### Verification/review
- [`docs/testing/VERIFICATION_STRATEGY.md`](docs/testing/VERIFICATION_STRATEGY.md)
- [`docs/review/SKEPTICAL_IMPLEMENTATION_GATES.md`](docs/review/SKEPTICAL_IMPLEMENTATION_GATES.md)
- [`docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md`](docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md)
- [`docs/review/RELIABILITY_API_AND_PATTERN_SOURCE_REVIEW.md`](docs/review/RELIABILITY_API_AND_PATTERN_SOURCE_REVIEW.md)
- [`docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md`](docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md)

## Source-of-truth rule

Generated CSV inventories/review ledgers are not architecture authority. Markdown can drift too, so each detailed topic has one owning document. Review/source files explain reasoning but do not override accepted decisions.

## Versioning

The current baseline remains `v0.0.15`. Documentation corrections do not manufacture a new semantic version unless the actual product/architecture baseline version changes.
