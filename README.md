# SquiFlow

**Current architecture/documentation version: `v0.0.15`**

Start with [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md).

> **Implementation state:** pre-Phase-0. The repository currently contains the audited architecture/implementation baseline. A directory described in a document is not implemented merely because it appears in a target tree.

## Current baseline

- C# / modern .NET.
- Avalonia for the Windows Workstation.
- Blazor Web App for tenant Web and future separate Platform Admin Web.
- ASP.NET Core Core API.
- Modular-monolith business code; network/process boundaries are added when they protect a real deployment/fault/security/recovery responsibility.
- **SquiFlow.Guard** is a baseline Workstation companion for launch/supervision, bounded crash/hang recovery, update recovery, child/helper cleanup and diagnostic/resource evidence. It does not own business logic.
- Small-team-first tenant model: Owner + Staff by default, with Owner-controlled granular permissions.
- Tenant role/permission and rule/workflow/form administration is Web-only; Desktop consumes published authority/configuration and never grants it.
- **ZITADEL** is the selected identity/authentication platform.
- **OpenFGA** is the selected application-authorization engine for roles/custom roles/assignments/resource relationships where applicable.
- ASP.NET Core authorization integrates OpenFGA checks; SquiFlow domain/workflow/concurrency rules and DB tenant isolation remain separate.
- Workstation login uses system-browser OIDC Authorization Code + PKCE against ZITADEL.
- Pooled multi-tenancy is the ordinary baseline; tenant isolation is separate from authentication/OpenFGA authorization.
- Shared compute is still tenant-aware: expensive jobs/reports/provider work use bounded/fair limits where needed to prevent noisy-neighbor takeover.
- Workstation is local-first/offline; Web is online-only for business operations in v0.0.15.
- Commands and queries have separate responsibilities in code, but separate CQRS databases/services/event sourcing are not baseline.
- Short authoritative work remains synchronous; after-commit consequences may use transactional outbox + Worker.
- Queue/job, pub/sub, event stream, and direct synchronous calls are treated as different tools selected from the real semantic need; Kafka/event-stream infrastructure is not baseline.
- Retryable mutations use semantic idempotency keys; duplicate defense covers caller/producer retry, transport redelivery, and consumer/effect replay.
- At-least-once delivery is handled by idempotent/reconcilable effects; system-wide `exactly once` is not claimed from one local transaction/broker feature.
- PostgreSQL is the strongest central reference candidate; SQLite + WAL and libSQL are Workstation-store candidates. Exact DB products remain open until their POCs.
- Currency is configurable/not hardcoded. v0.0.15 does not build a multi-currency/FX subsystem.
- SquiFlow-native bounded rules/workflow remain in-process capabilities unless real isolation/scale proves otherwise.
- **Primary bootstrap object storage:** private Hugging Face Storage Bucket, current private-storage envelope about 100 GB.
- **Object provider boundary:** `IObjectStore`, bootstrap implementation `HuggingFaceObjectStore`.
- **Bootstrap off-site backup carrier:** private Kaggle Dataset containing encrypted opaque backup archives only.
- **Backup provider boundary:** infrastructure-level `IBackupTarget`, bootstrap implementation `KaggleBackupTarget`.
- Planned object/backup-provider migration trigger: first paying customer, or earlier if capacity/privacy/compliance/reliability/restore requirements demand it.
- Backup covers all state required to reconstruct a usable deployment according to topology; it is not merely an application-row export.
- Current server environment is lower-spec/desktop-class rack hardware; `stateless` does not mean automatic failover or zero downtime.
- OpenTelemetry remains the provider-neutral telemetry boundary; New Relic + Aiven OpenSearch are current managed targets and Backtrace remains the crash-diagnostics direction.

## Complexity rule: disciplined completeness

SquiFlow is not optimizing for the smallest number of files/processes/interfaces. It is optimizing for the smallest **correct** structure.

Keep a boundary when it protects a real capability or committed replacement:

```text
SquiFlow.Guard     process supervision/recovery
IObjectStore       known near-term primary-storage migration
IBackupTarget      known near-term backup-provider migration
ZITADEL            identity/authentication
OpenFGA            application authorization
```

Avoid ceremony that does not protect anything:

```text
IRepository<T> / IUnitOfWork by default
one-interface-per-class
generic Manager → Service → Handler forwarding chains
arbitrary helper processes
empty projects/directories for future architecture
```

Minimalism must never remove required offline durability, recovery, authorization freshness, tenant isolation, backup restore, retry/idempotency guarantees, or edge-case handling.

## Documentation map

### Start here
- [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md) — current implementation plan.
- [`docs/review/DECISION_AUDIT.md`](docs/review/DECISION_AUDIT.md) — KEEP / SIMPLIFY / DEFER / REMOVE / OPEN / RESTORE decision audit.
- [`docs/decisions/CURRENT_DECISIONS.md`](docs/decisions/CURRENT_DECISIONS.md) — accepted direction.
- [`docs/decisions/OPEN_DECISIONS.md`](docs/decisions/OPEN_DECISIONS.md) — unresolved implementation details.
- [`docs/implementation/PHASES_AND_GATES.md`](docs/implementation/PHASES_AND_GATES.md) — sequential implementation order and edge/failure gates.

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
- [`docs/workstation/GUARD_AND_RECOVERY.md`](docs/workstation/GUARD_AND_RECOVERY.md)
- [`docs/sync/SYNC_AND_AUTHORITY.md`](docs/sync/SYNC_AND_AUTHORITY.md)

### Security/admin/identity
- [`docs/security/TENANT_PERMISSIONS.md`](docs/security/TENANT_PERMISSIONS.md) — OpenFGA authorization model/role boundary.
- [`docs/security/IDENTITY_AND_SESSIONS.md`](docs/security/IDENTITY_AND_SESSIONS.md) — ZITADEL/OIDC/session/device boundary.
- [`docs/admin/ADMIN_SURFACES.md`](docs/admin/ADMIN_SURFACES.md)

### Web/data/integrations
- [`docs/web/WEB_RUNTIME_AND_STORAGE.md`](docs/web/WEB_RUNTIME_AND_STORAGE.md)
- [`docs/web/CUSTOM_DOMAINS.md`](docs/web/CUSTOM_DOMAINS.md)
- [`docs/data/PERSISTENCE_SELECTION.md`](docs/data/PERSISTENCE_SELECTION.md)
- [`docs/data/FILES_AND_OBJECT_STORAGE.md`](docs/data/FILES_AND_OBJECT_STORAGE.md) — `IObjectStore`, `IBackupTarget`, Hugging Face/Kaggle bootstrap and migration.
- [`docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md`](docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md)
- [`docs/observability/OBSERVABILITY.md`](docs/observability/OBSERVABILITY.md)

### Verification/review
- [`docs/testing/VERIFICATION_STRATEGY.md`](docs/testing/VERIFICATION_STRATEGY.md)
- [`docs/review/SKEPTICAL_IMPLEMENTATION_GATES.md`](docs/review/SKEPTICAL_IMPLEMENTATION_GATES.md)
- [`docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md`](docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md)
- [`docs/review/RELIABILITY_API_AND_PATTERN_SOURCE_REVIEW.md`](docs/review/RELIABILITY_API_AND_PATTERN_SOURCE_REVIEW.md)
- [`docs/review/BYTEBYTEGO_DISTRIBUTED_SYSTEMS_SOURCE_REVIEW.md`](docs/review/BYTEBYTEGO_DISTRIBUTED_SYSTEMS_SOURCE_REVIEW.md)
- [`docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md`](docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md)

## Source-of-truth rule

Generated CSV inventories/review ledgers are not architecture authority. Markdown can drift too, so each detailed topic has one owning document. Review/source files explain reasoning but do not override accepted decisions.

## Versioning

The current baseline remains `v0.0.15`. Documentation corrections do not manufacture a new semantic version unless the actual product/architecture baseline version changes.
