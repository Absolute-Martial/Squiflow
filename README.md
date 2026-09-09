# SquiFlow

**Current architecture/documentation version: `v0.0.15`**

Start with [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md).

> **Implementation state:** pre-Phase-0. The repository currently contains the audited architecture/implementation baseline. A directory described in a document is not implemented merely because it appears in a target tree.

## Current baseline

- C# / modern .NET.
- Avalonia for the Windows Workstation.
- Blazor Web App for tenant Web and future separate Platform Admin Web.
- ASP.NET Core Core API for tenant/business operations.
- **Platform Admin uses a separate `services/admin-api` backend**, independent of Core API for normal super-admin/control-plane operations.
- `apps/admin-web → services/admin-api`; Core API is not the normal downstream backend for platform administration.
- Modular-monolith business code; network/process boundaries are added when they protect a real deployment/fault/security/recovery responsibility.
- Ordinary business modules communicate in-process; HTTP/gRPC between modules is not baseline.
- **SquiFlow.Guard** is a baseline Workstation companion for launch/supervision, bounded crash/hang recovery, update recovery, child/helper cleanup and diagnostic/resource evidence. It does not own business logic.
- Small-team-first tenant model: Owner + Staff by default, with Owner-controlled granular permissions.
- Tenant role/permission and rule/workflow/form administration is Web-only; Desktop consumes published authority/configuration and never grants it.
- **ZITADEL** is the selected identity/authentication platform.
- **OpenFGA** is the selected application-authorization engine for roles/custom roles/assignments/resource relationships where applicable.
- ASP.NET Core authorization integrates OpenFGA checks; SquiFlow domain/workflow/concurrency rules and DB tenant isolation remain separate.
- Tenant and platform authorization scopes remain separate; a tenant role can never become super-admin authority.
- Workstation login uses system-browser OIDC Authorization Code + PKCE against ZITADEL.
- Pooled multi-tenancy is the ordinary baseline; tenant isolation is separate from authentication/OpenFGA authorization.
- Shared compute is tenant-aware: expensive jobs/reports/provider work use bounded/fair operational protection and scoped limits where needed to prevent resource takeover.
- **Durable/reconcilable resource-consumption accounting and application-level scoped limits are baseline capabilities when an implemented resource requires them.** Usage needed for enforcement is application state, not optional analytics/telemetry.
- Limits may be platform/provider, workload/resource, tenant, integration/destination, or another explicit bounded scope. A tenant-specific limit does not itself imply a subscription plan.
- **No commercial tenant plan/tier/pricing/default-allowance model is accepted in v0.0.15.** A future commercial plan may map onto the same versioned limit policies without changing the usage-accounting foundation.
- Cross-cutting non-functional requirements are modeled as hard invariants, measurement-driven operational targets, and degraded-mode contracts rather than generic quality slogans.
- Workstation is local-first/offline; Web is online-only for business operations in v0.0.15.
- Commands and queries have separate responsibilities in code, but separate CQRS databases/services/event sourcing are not baseline.
- Short authoritative work remains synchronous; after-commit consequences may use transactional outbox + Worker.
- Queue/job, pub/sub, event stream, and direct synchronous calls are treated as different tools selected from the real semantic need; Kafka/event-stream infrastructure is not baseline.
- Retryable mutations use semantic idempotency keys; duplicate defense covers caller/producer retry, transport redelivery, consumer/effect replay, and metered usage where one semantic effect must count once.
- At-least-once delivery is handled by idempotent/reconcilable effects; system-wide `exactly once` is not claimed from one local transaction/broker feature.
- **Consistency is selected per invariant**, not globally: payments/stock/credit/tenant isolation/current sensitive authorization/hard limits use current/strong authority; caches/notifications/derived reports may be eventually updated only with explicit freshness/rebuild rules.
- PostgreSQL is the strongest central reference candidate; SQLite + WAL and libSQL are Workstation-store candidates. Exact DB products remain open until their POCs.
- Authoritative relational state is normalized first; denormalized/materialized read structures are derived optimizations with explicit source/freshness/rebuild contracts.
- Database indexes are workload-driven and measured for both query benefit and write/WAL/storage/migration/sync cost.
- Core API, Admin API, and Worker may share the central DB as hosts of the same modular-monolith business core; shared access still has explicit module/data ownership and common invariants.
- **REST/task-oriented HTTP is the v0.0.15 API baseline, without claiming strict REST purity.** Ordinary resources are resource-oriented; semantic command subresources remain valid for approvals/refunds/publications/etc.
- POST is not automatically retry-safe; retryable POST commands depend on the SquiFlow semantic-idempotency contract.
- API compatibility/versioning is mandatory for skipped Workstation releases and independently deployed backends.
- Database/API/sync/durable-work evolution must tolerate supported old and new readers/writers; additive expand-migrate-switch-contract changes are preferred before destructive contraction.
- GraphQL/GraphQL Federation are deferred until a real query-composition requirement proves them worthwhile.
- Rate limiting/admission is multi-dimensional where required rather than one global RPS number; authorization and throttling are separate decisions. Durable quota/usage accounting is separate from transient rate-limit telemetry where authoritative usage is required.
- Pagination, bounded connection pooling, selective caching/compression, and bounded async telemetry export are evidence-driven API performance techniques.
- A cache is always bounded, tenant-safe, disposable and non-authoritative; cache outage/cold start may reduce performance but cannot grant access, lose truth, or turn stale payment/stock/credit/permission state into authority.
- An edge reverse proxy/API-gateway capability may route/TLS/WAF/coarsely limit traffic, but it never replaces backend authorization/usage authority and never collapses Admin API into Core API.
- A heavyweight API-management product is not baseline until concrete management/routing requirements justify it.
- A service mesh is not baseline; revisit only if real east-west service traffic justifies the runtime/operational cost.
- Production external traffic uses HTTPS/TLS; ZITADEL uses OIDC/OAuth over HTTPS. HTTP/1.1/2/3 negotiation is transport detail, not business semantics.
- WebSocket/SignalR, if used, is live signaling only; durable truth remains in DB/outbox/state. DNS/hostnames route traffic but are never tenant authority. SSH/private access is infrastructure operations only.
- Currency is configurable/not hardcoded. v0.0.15 does not build a multi-currency/FX subsystem.
- SquiFlow-native bounded rules/workflow remain in-process capabilities unless real isolation/scale proves otherwise.
- Practical domain limits remain authoritative: no forced ready-made/custom-design or separate social category, Owner-adjustable pricing, outsourced print-only work, informal supplier ordering/partial payables, damaged-stock adjustments, and no universal reservation/MRP/banner-wastage engine.
- **Primary bootstrap object storage:** private Hugging Face Storage Bucket, current private-storage envelope about 100 GB. This is a provider/account capacity fact; retained bytes are a likely first consumption meter, not an automatically divided tenant allowance.
- **Object provider boundary:** `IObjectStore`, bootstrap implementation `HuggingFaceObjectStore`.
- **Bootstrap off-site backup carrier:** private Kaggle Dataset containing encrypted opaque backup archives only.
- **Backup provider boundary:** infrastructure-level `IBackupTarget`, bootstrap implementation `KaggleBackupTarget`.
- Planned object/backup-provider migration trigger: first paying customer, or earlier if capacity/privacy/compliance/reliability/restore requirements demand it.
- Backup covers all state required to reconstruct a usable deployment according to topology, including implemented usage/limit state needed for enforcement; it is not merely an application-row export.
- Current server environment is lower-spec/desktop-class rack hardware; `stateless` does not mean automatic failover or zero downtime.
- The initial release process promotes one verified immutable artifact and proves migration, health/smoke, rollback/roll-forward, and honest maintenance-window behavior; blue-green/canary deployment is not assumed on a single node.
- Browser/API/data/file/build security follows a layered application-security baseline in addition to ZITADEL, OpenFGA, tenant isolation and edge controls.
- OpenTelemetry remains the provider-neutral telemetry boundary; New Relic + Aiven OpenSearch are current managed targets and Backtrace remains the crash-diagnostics direction. Telemetry is not the authoritative source for enforced consumption.

## Complexity rule: disciplined completeness

SquiFlow is not optimizing for the smallest number of files/processes/interfaces. It is optimizing for the smallest **correct** structure.

Keep a boundary when it protects a real capability or committed replacement:

```text
SquiFlow.Guard     process supervision/recovery
services/admin-api independent platform/super-admin backend
IObjectStore       known near-term primary-storage migration
IBackupTarget      known near-term backup-provider migration
ZITADEL            identity/authentication
OpenFGA            application authorization
Consumption/limits durable usage + scoped resource-policy correctness
```

Apply clean-code/SOLID principles pragmatically: meaningful business names, cohesive responsibilities, explicit policy/config values, narrow provider interfaces, and contract-compatible replacements. Do not turn DRY/SOLID into helper/interface proliferation; small duplication is preferable to a wrong shared abstraction.

Avoid ceremony that does not protect anything:

```text
IRepository<T> / IUnitOfWork by default
one-interface-per-class
generic Manager → Service → Handler forwarding chains
HTTP/gRPC between ordinary modules
arbitrary helper processes
empty projects/directories for future architecture
generic data warehouse that records every click as "metering"
```

Minimalism must never remove required offline durability, recovery, authorization freshness, tenant isolation, backup restore, retry/idempotency guarantees, usage/limit correctness, control-plane independence, consistency/freshness behavior, or edge-case handling.

## Documentation map

### Start here
- [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md) — current implementation plan.
- [`docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md`](docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md) — business/platform NFRs, degraded modes, operational targets and capability-completeness gates.
- [`docs/requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md`](docs/requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md) — durable usage accounting, scoped limit policy/enforcement, retry/concurrency/offline/reconciliation behavior, and separation from analytics/commercial plans.
- [`docs/review/NFR_DECISION_CHALLENGE.md`](docs/review/NFR_DECISION_CHALLENGE.md) — common/edge/failure/recovery challenge of major decisions, including usage/limits, consistency, gateway/protocol and no-commercial-plan handling.
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
- [`docs/security/APPLICATION_SECURITY_BASELINE.md`](docs/security/APPLICATION_SECURITY_BASELINE.md) — browser/API/injection/file/SSRF/build/container security baseline.
- [`docs/admin/ADMIN_SURFACES.md`](docs/admin/ADMIN_SURFACES.md) — separate Admin Web/Admin API control plane.

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
- [`docs/review/BYTEBYTEGO_CODE_CONSISTENCY_DATA_API_SOURCE_REVIEW.md`](docs/review/BYTEBYTEGO_CODE_CONSISTENCY_DATA_API_SOURCE_REVIEW.md)
- [`docs/review/BYTEBYTEGO_API_GATEWAY_SERVICE_PROTOCOL_SOURCE_REVIEW.md`](docs/review/BYTEBYTEGO_API_GATEWAY_SERVICE_PROTOCOL_SOURCE_REVIEW.md)
- [`docs/review/BYTEBYTEGO_ARCHIVE_SEQUENTIAL_REVIEW_001_015.md`](docs/review/BYTEBYTEGO_ARCHIVE_SEQUENTIAL_REVIEW_001_015.md)
- [`docs/review/BYTEBYTEGO_ARCHIVE_SEQUENTIAL_REVIEW_016_123.md`](docs/review/BYTEBYTEGO_ARCHIVE_SEQUENTIAL_REVIEW_016_123.md) — completes all 123 selected archive entries.
- [`docs/review/BYTEBYTEGO_WEB_CONTENT_REVIEW_023_064.md`](docs/review/BYTEBYTEGO_WEB_CONTENT_REVIEW_023_064.md) — completes the remaining supplied Web-content entries; entries 001-022 are mapped to the thematic reviews above.
- [`docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md`](docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md)

## Source-of-truth rule

Generated CSV inventories/review ledgers are not architecture authority. Markdown can drift too, so each detailed topic has one owning document. Review/source files explain reasoning but do not override accepted decisions.

## Versioning

The current baseline remains `v0.0.15`. Documentation corrections do not manufacture a new semantic version unless the actual product/architecture baseline version changes.
