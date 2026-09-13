# SquiFlow Application Kernel, Capability Cores, Modules, Settings, and Features

**Version:** v0.0.18  
**Status:** Accepted architecture direction; Phase 0 must prove the smallest implementation.  
**Authority:** This document owns application composition, module/feature lifecycle, dependency injection, settings, application-service and transaction conventions, capability-provided endpoints/UI/background work, data seeding, and framework-adoption boundaries. Business-meaning ownership is defined by `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`. Permission semantics remain owned by `docs/security/TENANT_PERMISSIONS.md`; identity remains owned by `docs/security/IDENTITY_AND_SESSIONS.md`.

## 1. Decision

SquiFlow builds a small **SquiFlow-owned application kernel** on standard .NET and ASP.NET Core primitives.

ABP Framework and Orchard Core are reference designs. They are not runtime foundations and neither becomes application, tenant, settings, permission, workflow, persistence, audit, or background-work authority.

The selected shape is:

```text
Foundation
   ↓
Capability Cores
   ↓
module/composition descriptors + host adapters
   ↓
standard Microsoft.Extensions dependency injection/hosting
   ↓
versioned tenant feature/settings/permission snapshots
```

The term **Capability Core** is the common business point. The term **module** in this document means reviewed composition metadata/contributions around a capability; it does not mean a second shared-business layer and does not imply one deployable service per module.

Workstation, Web, WebApi/CoreApi, SyncApi, Worker and Admin hosts can compose different adapters/contributions around the same Capability Cores without depending on `Volo.Abp.*` or `OrchardCore.*`.

An optional Orchard CMS may still be evaluated later as a separate content host for accepted editorial/public-content responsibilities. It does not become the business application kernel.

## 2. Foundation, Capability Core, and module descriptor

Use these terms precisely:

```text
Foundation
= small product-wide primitives/abstractions with no business-capability ownership

Capability Core
= one capability's business meaning + deterministic decisions

Module descriptor
= stable composition metadata describing where/how the capability is available

Host adapter
= Workstation/Web/API/Sync/persistence/provider-specific presentation/facts/effects
```

Do not create:

```text
Orders.WorkstationBusiness
Orders.WebBusiness
Orders.ServerBusiness
```

when the underlying business decision is the same. Prefer one `Orders.Core` decision processor supplied with host-specific facts and followed by host-specific effects.

The current compact `modules/customers/SquiFlow.Customers` project acts as the Customers Capability Core and also owns its small descriptor. This compactness is intentional; split projects only when dependency pressure earns them.

## 3. What is borrowed and what is not

| Concern | SquiFlow direction | Useful reference idea | Explicitly not adopted |
|---|---|---|---|
| Dependency injection | Microsoft.Extensions.DependencyInjection with one composition root per executable | ABP/Orchard registration conventions | service locator, automatic property injection, per-tenant container forests |
| Capability composition | trusted C# descriptor, dependency DAG, ordered startup and host contributions around Capability Cores | ABP dependency/lifecycle graph; Orchard module/feature split | two runtimes, arbitrary untrusted DLL loading, module-per-service |
| Tenant composition | versioned effective feature/settings/permission snapshot resolved from TenantContext | Orchard tenant feature profiles | separate app/service provider/database per tenant by default |
| DDD/application services | use-case-oriented services and selective aggregates/value objects/domain services | ABP DDD guidance | mandatory layer/project/type for every CRUD feature |
| Transactions | explicit transaction per authoritative command where one store can own it | unit-of-work intent | generic IUnitOfWork baseline or cross-store ACID fiction |
| Data access | EF Core candidate for aggregate writes/migrations; Dapper candidate for measured read paths | ABP provider options | generic IRepository<T>, framework entities, unrestricted ad-hoc SQL |
| HTTP/API clients | explicit reviewed endpoints; generated OpenAPI/Protobuf clients when valuable | ABP generation convenience | automatic controller exposure for every application method |
| Settings | typed definitions, ordered value sources, validation, sensitivity and UI metadata | ABP settings providers | security ceilings/invariants overridable by tenant values |
| Features | capability-declared features, dependencies, host support, release channel and tenant activation | Orchard enable/disable/dependency model | hot unloading CLR assemblies; features as authorization |
| Permissions | capability-owned stable definitions, SquiFlow evaluator, OpenFGA relationship decisions | ABP definition/catalog concept | ABP/Orchard role store; ZITADEL claims as business permission truth |
| Background work | capability-declared handlers plus SquiFlow durable Worker lifecycle | pluggable job-provider idea | ABP and Orchard queues concurrently; losing outbox/reconciliation semantics |
| Seeding | idempotent C# system seeds plus validated declarative JSON recipes | ABP contributors; Orchard recipes | arbitrary JSON-selected CLR types/scripts or unaudited production mutation |
| Audit | explicit security/administrative/business evidence with bounded technical tracing | ABP interception and Orchard content history as references | blanket payload/property capture or content revisions as business audit truth |
| UI contributions | reviewed navigation/page/block descriptors per host | Orchard admin/navigation composition | generated UI as authorization enforcement or universal CRUD product |

These are pattern choices, not copied framework implementations.

## 4. Module descriptor and dependency graph

Each built-in capability may declare a stable descriptor containing only composition metadata it owns:

```text
ModuleId and ModuleVersion
direct capability/module dependencies
feature definitions and feature dependencies
supported host kinds
permission definitions
setting definitions
migration/seed contributors
application use-case contributions
endpoint/generated-contract contributions
UI/navigation contributions
background handler contributions
compatibility requirements
```

The kernel validates the graph before serving traffic for missing dependencies, cycles, duplicate identifiers, incompatible versions, or invalid contributions.

A descriptor dependency is a code/capability dependency, not a permission and not a tenant entitlement.

Module IDs, feature IDs, permission IDs, setting keys, job kinds, schema ownership and public contract names are stable compatibility vocabulary. Renaming/removing one requires explicit compatibility/migration handling.

## 5. Trusted loading and runtime enable/disable

Initial releases load only assemblies shipped in the verified SquiFlow artifact. Adding/replacing assemblies is a deployment and normally a process restart.

Runtime tenant feature publication changes availability data, not the process service graph:

```text
host-supported features
∩ platform capability ceiling
∩ tenant-published activation
∩ dependency closure
∩ release-channel / compatibility / rollout constraints
= immutable effective feature snapshot for the operation
```

Feature availability, release channel, experiment assignment, permission and domain validity remain separate systems. See `docs/architecture/FEATURE_RELEASE_AND_EXPERIMENTS.md`.

Do not build a child `IServiceProvider` per tenant. Tenant-aware services receive immutable `TenantContext` plus relevant configuration/feature revisions through scoped context.

A feature publication is validated, versioned, audited and atomic from the tenant perspective. A request, command, sync batch or claimed job uses one effective revision rather than observing half of a publication.

Disabling a feature:
- prevents new entry into its endpoints/commands/UI contributions;
- resolves dependent features according to explicit dependency rules;
- never deletes authoritative data automatically;
- never silently abandons/reinterprets pending work;
- follows declared drain/finish/cancel/quarantine/migrate policy;
- cannot disable an always-required safety/core feature;
- marks related grants dormant/visible rather than deleting them;
- requires authorized confirmation before dormant grants reactivate;
- increments relevant configuration/authorization evidence as defined.

A loaded assembly is not hot-unloaded. Untrusted micro-plugins, dynamic replacement and arbitrary tenant scripting remain deferred until signing, compatibility, isolation/sandboxing, upgrade, rollback, resource and support responsibilities are proven.

## 6. Dependency injection

Each executable owns one ordinary .NET composition root. Capability adapters contribute registrations only for hosts they support.

Rules:
- constructor injection by default;
- no ambient service locator in Capability Core/domain/application code;
- no per-tenant mutable singleton;
- no retaining one tenant's service/context across requests/jobs;
- provider SDK types behind narrow adapters;
- replacement interfaces only for real provider/strategy boundaries;
- decorators/pipelines may own cross-cutting validation/authorization/transaction/idempotency/audit only with explicit ordering/failure semantics.

Per-tenant behavior comes from scoped context and policy/data, not rebuilt DI containers.

## 7. Capability Core and application-service pattern

A Capability Core owns deterministic business meaning where it is actually common across hosts.

Conceptually:

```text
Intent + Facts + Rule/Policy Snapshot
              ↓
        Capability Core
              ↓
           Decision
```

Hosts differ in fact providers and effects:

```text
Workstation local facts / SQLite snapshots
       ↓
Capability Core
       ↓
allowed provisional/local effect

Server authoritative facts / PostgreSQL + current authority
       ↓
Capability Core
       ↓
server-only authority/concurrency checks + authoritative effect
```

Use DDD tactical patterns only where they protect real rules:
- aggregate root for one consistency boundary;
- entity for identity/lifecycle;
- value object for constrained meaning;
- domain service for domain logic belonging to no aggregate;
- application service for one named use case, authority, loading, transaction and consequences.

Simple reference data/read-only projections need not become elaborate aggregates.

Application services expose SquiFlow contracts; they do not inherit ABP application-service bases or use Orchard content items as business entities.

## 8. Execution modes and host authority

The application kernel vocabulary includes:

```text
DeviceLocal
LocalProvisional
ServerAuthoritative
```

- `DeviceLocal`: device-only behavior with no server business effect.
- `LocalProvisional`: Workstation may execute/store a provisional local result and later submit semantic intent for authoritative admission.
- `ServerAuthoritative`: current server authority required; cannot become authoritative offline.

The Workstation/server two-stage contract is `Provisional Execution + Authoritative Admission`, not blind double execution. See `docs/decisions/DUAL_PROCESSING_AND_IN_PROCESS_COORDINATION.md` and `docs/sync/SYNC_AND_AUTHORITY.md`.

Web normally enters authoritative application use cases directly. SyncApi receives semantic Workstation operations and performs current admission/reconciliation. Both reach the same Capability Core semantics rather than parallel business implementations.

## 9. Persistence, repositories, and unit of work

The **unit-of-work concept is accepted; a generic unit-of-work framework abstraction is not**.

For one authoritative command:

```text
authorize current attempt
→ load tenant-scoped/current authoritative facts
→ validate expected/dependency versions and domain rules
→ begin/use explicit store transaction
→ persist mutation + idempotency receipt + outbox where co-owned
→ commit
→ dispatch only after commit
```

An EF Core DbContext/transaction may implement that boundary inside an adapter. A direct ADO.NET/Dapper transaction may implement a focused path. Application code must not call `SaveChanges` at arbitrary hidden layers.

Use EF Core when change tracking, relational mapping, migrations, aggregate persistence and concurrency support reduce risk. Use Dapper for measured query/read-model paths or focused SQL where explicit mapping is clearer. Do not split one authoritative write across unrelated transactions.

Create a domain-named repository only when an aggregate/query boundary benefits. Generic `IRepository<T>` and `IUnitOfWork` are not baseline.

PostgreSQL remains central transactional state and SQLite/WAL the Workstation local store under `docs/data/PERSISTENCE_SELECTION.md`. Dedicated NoSQL and libSQL remain deferred; extensibility does not itself justify another database.

No transaction coordinator pretends PostgreSQL, SQLite, OpenFGA, ZITADEL, object storage and external providers form one ACID transaction. Cross-system effects use durable operation state, idempotency, verification and reconciliation.

## 10. Concurrency and dependency revisions

Expected-version optimistic concurrency is the ordinary edit contract.

Each mutable aggregate/configuration publication defines:
- concurrency token/version;
- command expected version;
- conflict result/user recovery;
- whether stronger isolation/constraint/lock is needed;
- how bounded whole-transaction retry remains idempotent.

ModuleFeatureRevision, SettingRevision, TenantAuthorizationRevision, workflow/rule/form revisions and data-row versions are distinct evidence. Do not merge them into one magic global revision.

For Workstation provisional operations, retain only material dependency revisions needed for authoritative admission. The server may use unchanged revisions for a safe fast path and selectively re-evaluate affected decisions when material facts changed; mandatory authority/concurrency checks still run.

## 11. Settings system

Capabilities declare typed setting definitions with:
- stable key and owner;
- type/default/validation;
- allowed scopes;
- sensitivity/client visibility;
- restart/reload behavior;
- display/localization/help metadata;
- compatibility/migration rules.

Effective precedence is narrow/deterministic:

```text
code default
→ deployment/platform value or policy
→ tenant value where explicitly allowed
→ user preference only for personal/presentation settings
```

A lower scope cannot exceed platform/provider/security ceiling or hard invariant. Secrets are references/protected values and never general client configuration.

Metadata may generate basic Admin/Settings editors for simple safe values. Complex, destructive, security-sensitive or workflow-changing configuration uses purpose-built UI and preview/diff/validation/publication flow.

## 12. Features, release channels, experiments, permissions, and domain validity

```text
feature availability = is capability exposed here?
release channel      = what maturity audience may receive it?
experiment           = which safe approved variant is assigned?
setting              = how is it configured?
permission           = may this actor attempt it on this resource/scope?
domain rule          = is it valid for current business state?
limit/admission      = may it consume bounded resources now?
```

All applicable checks pass independently. Feature targeting does not grant permission. UI hiding is never authorization.

Release channels currently use `Internal`, `Preview`, `Beta`, `Stable`, `Deprecated`; `Removed` means the feature definition/compatibility path is no longer exposed rather than a runnable channel.

Workstation may consume a versioned effective feature snapshot for local/offline UX according to `SnapshotAllowed`, `StableOnly`, or `ServerRequired` policy. Security/admin operations remain server-authoritative regardless of cached feature state.

A/B testing is limited to safe product/presentation alternatives and stable subject assignment; security, tenant isolation, financial/accounting correctness, inventory integrity and durability/concurrency invariants are not experiments.

Detailed owner: `docs/architecture/FEATURE_RELEASE_AND_EXPERIMENTS.md`.

## 13. Permission integration

Capabilities publish stable permission definitions to the SquiFlow catalog. Definitions may include group/parent metadata, supported hosts/scopes, feature dependency, delegation risk and freshness class.

The effective authoritative path is:

```text
ZITADEL-authenticated identity/session/device context
→ authoritative TenantContext
→ capability/feature available at one published revision
→ permission definition exists/applies
→ current OpenFGA relationship/role/resource check
→ delegation/platform/tenant checks
→ SquiFlow domain/workflow/concurrency/limit validation
```

ZITADEL supplies identity/authentication strength/recency. It is not the current business authorization store.

OpenFGA remains relationship/authorization engine. SquiFlow owns permission vocabulary/catalog, configuration revisions, delegation ceilings, domain checks and durable/reconcilable admin workflows.

An online authorized Workstation may expose selected tenant administration as a host adapter, but it never grants authority locally/offline and never receives OpenFGA administrative credentials. Platform administration remains Admin Web/Admin API only. See `docs/admin/ADMIN_SURFACES.md`.

## 14. HTTP, Sync API, gRPC, GraphQL, and generated clients

REST/task-oriented HTTP remains ordinary Web/external application API. Automatic REST controller exposure is off by default.

A capability contributes an endpoint only through an explicit reviewed contract stating tenant context, permission, idempotency, concurrency, validation, versioning, rate/limit, audit and failure behavior as applicable.

Generated clients are allowed from reviewed OpenAPI/Protobuf contracts when generation reduces drift. Generation never decides what is safe to expose.

Interactive Web/API traffic and Workstation sync traffic are accepted as separate future ingress/workload hosts when the split is implemented:

```text
WebApi  → interactive low-latency Web/tenant operations
SyncApi → workstation batching/idempotency/cursor/revision/backpressure workload
```

Both use the same authoritative application/Capability Core semantics and PostgreSQL; they are not two business backends.

The current `CoreApi` remains the compact early host until real implementation pressure earns the split. Do not create empty host projects.

gRPC remains a preferred candidate for the Workstation sync boundary when the POC proves value; sync semantics remain transport-independent. GraphQL remains deferred until a concrete query-composition use case pays for its cost/authorization/complexity.

Inside one host, capabilities call in-process application contracts.

Detailed owner: `docs/architecture/WEB_AND_SYNC_INGRESS.md`.

## 15. Background work

Capabilities declare job/handler kinds and compatibility metadata. The SquiFlow Worker owns durable execution when the first real workload exists.

The job envelope identifies at least tenant/scope, capability/handler kind/version, semantic operation/idempotency identity, payload/schema version, material configuration/feature revision, attempts/timing and trace/audit correlation.

Disabling a capability cannot make durable work disappear. Accepted work explicitly finishes, pauses, migrates, cancels with outcome, or moves to quarantine/reconciliation according to policy.

Do not run multiple overlapping queue frameworks merely because they exist. The selected mechanism preserves SquiFlow outbox, claim/lease, retry, poison-work, fairness, reconciliation, observability and recovery requirements.

## 16. Seeding and recipes

Use three distinct paths:

| Path | Use |
|---|---|
| Idempotent C# migration/seed contributor | invariant system records, schema-linked reference data, permission/setting definitions requiring compile-time ownership |
| Validated JSON recipe | reviewable declarative environment/tenant starter configuration, feature activation, sample/demo setup |
| Authenticated application/Admin command | interactive production changes with authorization, validation, versioning, audit and reconciliation |

A JSON recipe is data. It cannot execute arbitrary code, select arbitrary CLR types, contain secrets, bypass permissions, or mutate outside allow-listed handlers. Recipe schema/version and dry-run/diff are required before production use.

Seeds are repeatable and must not overwrite intentional tenant changes merely because an application restarts.

## 17. Audit and history

Keep distinct evidence:
- security/administrative audit;
- business history;
- technical telemetry;
- content revision history only for a future content system.

Do not enable blanket request/body/entity-property capture. Redact secrets/sensitive fields, bound payload/cardinality/retention and measure overhead. High-risk capability/feature/setting/permission changes require durable audit even when technical traces are sampled.

## 18. Host composition

| Host | Kernel/capability use |
|---|---|
| Workstation | compose local-capable UI/store/fact/effect adapters; consume published feature/permission/settings/rule snapshots; execute only approved DeviceLocal/LocalProvisional operations |
| Guard | supervision/update/diagnostic trigger contributions only; no business Capability Cores, tenant authorization or central DB |
| CoreApi | current compact authoritative tenant/business composition host until WebApi/SyncApi split is implemented |
| WebApi | future interactive authoritative ingress for Web/tenant operations; no duplicate business model |
| SyncApi | future Workstation sync/admission ingress with device/batch/backpressure semantics; no duplicate business model |
| Tenant Web | tenant business UI + broader Owner/Settings contributions; presentation only, server authority remains backend |
| Worker | created for first durable workload; background handlers without UI/controllers |
| Admin API/Web | separate platform-control host/surface with platform permission scope |
| Customer Web | least-privilege customer-facing adapter subset if boundary is accepted |
| Optional Orchard CMS | separate content authority only; no orders/payments/stock/roles as Orchard content |

One Capability Core can have different host adapters/contributions without forcing every host to reference every adapter/UI package.

## 19. Phase-0 proof obligations

Phase 0 implements only the kernel slice needed by the first launchable hosts and one sample capability.

Prove:
- graph ordering, missing/cyclic dependency failure and host filtering;
- Foundation/Capability Core stay free of host/provider dependencies;
- standard DI composition without per-tenant containers;
- one tenant feature publication with dependency closure, host filtering and revision stability;
- release-channel filtering and stable experiment assignment;
- one typed setting with platform ceiling and tenant override;
- one capability-owned permission definition evaluated through SquiFlow/OpenFGA adapter boundary;
- disabled feature cannot be reached through endpoint/direct app-service/background enqueue/UI route;
- disabling never deletes data or loses accepted durable work;
- Workstation does not reference ABP/Orchard/OpenFGA administration/central persistence packages;
- no empty future WebApi/SyncApi/Worker/process projects exist only to complete a diagram;
- startup/memory cost measured before adding reflection scanning/dynamic loading/UI auto-generation.

## 20. Alternatives and revisit triggers

Rejected as initial application kernel:
- full ABP application foundation;
- full Orchard application foundation;
- ABP + Orchard together in the same business host;
- maximum custom scripting/plugin loading;
- separate per-tenant service containers;
- feature flags implemented as permission checks;
- ZITADEL application roles as business authorization source.

Revisit framework/package adoption only if measured implementation/support burden is materially lower than dependency, upgrade, security and replacement cost, and an executable proof shows it preserves current SquiFlow authority/offline/tenancy/persistence/host boundaries.

## Source basis

- ABP modularity: https://abp.io/docs/latest/framework/architecture/modularity/basics
- ABP authorization: https://abp.io/docs/latest/framework/fundamentals/authorization
- ABP settings: https://abp.io/docs/latest/framework/infrastructure/settings
- Orchard Core features: https://docs.orchardcore.net/en/latest/reference/modules/Features/
- Orchard Core tenants: https://docs.orchardcore.net/en/latest/reference/modules/Tenants/
- Microsoft .NET dependency injection: https://learn.microsoft.com/dotnet/core/extensions/dependency-injection
- ASP.NET Core authorization: https://learn.microsoft.com/aspnet/core/security/authorization/introduction
- OpenFGA custom roles: https://openfga.dev/docs/modeling/custom-roles
- OpenFGA consistency: https://openfga.dev/docs/interacting/consistency
- ZITADEL OIDC: https://zitadel.com/docs/guides/integrate/login/oidc
