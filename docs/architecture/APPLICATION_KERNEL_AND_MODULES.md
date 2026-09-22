# SquiFlow Application Kernel, Capability Cores, Modules, Settings, and Features

**Version:** v0.1.0
**Status:** Accepted architecture direction. The first compact Branding capability and CoreApi composition root now exist; there is still no general `SquiFlow.ApplicationKernel` or module runtime. Shared kernel/module types are introduced only when current capability/application work earns them.
**Authority:** This document owns application composition, module/feature lifecycle, dependency injection, settings, application-service and transaction conventions, capability-provided endpoints/UI/background work, data seeding, and framework-adoption boundaries. Business-meaning ownership is defined by `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`. Permission semantics remain owned by `docs/security/TENANT_PERMISSIONS.md`; identity remains owned by `docs/security/IDENTITY_AND_SESSIONS.md`.

## 1. Decision

When current capability/application work earns shared composition semantics, SquiFlow uses a small **SquiFlow-owned application kernel** on the .NET Generic Host, standard Microsoft.Extensions primitives and ASP.NET Core where applicable.

The source-first application-base comparison selected FullStackHero as the complete pinned source-owned backend starting base and the other frameworks as focused reference designs/donors. SquiFlow selectively adapts reviewed source rather than running the FSH generator or importing an FSH runtime. None becomes application, tenant, settings, permission, workflow, persistence, audit or background-work authority.

The accepted dependency/composition direction is:

```text
Foundation
   ↓
Capability Cores
   ↓
module/composition descriptors + host adapters
   ↓
standard Microsoft.Extensions dependency injection/hosting
   ↓
versioned tenant feature/settings/permission snapshots when those responsibilities exist
```

This is architecture direction, not proof that every layer or contract currently exists.

The term **Capability Core** is the common business point. The term **module** in this document means reviewed composition metadata/contributions around a capability; it does not mean a second shared-business layer and does not imply one deployable service per module.

Future earned Workstation, Web, compact authoritative API, workload-specific ingress, Worker, and Admin executables can compose different adapters/contributions around the same Capability Cores without depending on `Volo.Abp.*` or `OrchardCore.*`.

Executable/process names are **not** stable application-kernel vocabulary. The kernel must not maintain an enum of current/future executable names. Each executable composition root chooses the capability modules and adapters it actually references once that executable exists.

An optional Orchard CMS may still be evaluated later as a separate content host for accepted editorial/public-content responsibilities. It does not become the business application kernel.

## 2. Foundation, Capability Core, module descriptor, and adapter

Use these terms precisely:

```text
Foundation
= small product-wide primitives/abstractions with no business-capability ownership

Capability Core
= one capability's business meaning + deterministic decisions

Module descriptor
= stable capability-owned composition metadata: identity/version, dependencies,
  features, permissions, settings and other host-neutral contribution contracts
  only for responsibilities actually introduced

Host adapter
= Workstation/Web/API/Sync/persistence/provider-specific presentation/facts/effects
  only when that host/provider responsibility exists

Executable composition root
= the place that selects which modules/adapters are loaded into an executable
  once the executable has been earned
```

Do not create:

```text
Orders.WorkstationBusiness
Orders.WebBusiness
Orders.ServerBusiness
```

when the underlying business decision is the same. Prefer one capability-owned deterministic decision implementation supplied with host-specific facts and followed by host-specific effects when real cross-host reuse exists.

A small capability may remain one compact project and own a small descriptor in that project if composition metadata is actually needed. A future Customers capability could use this shape, but there is no current Customers project after the reset. Split projects only when dependency/reuse/provider/platform pressure earns them.

Host/process applicability is not encoded by a process-name set on the descriptor. Illustratively, if a future Workstation loads Customers, its composition root references Customers and the Workstation adapter; if an authoritative API host loads Customers, that composition root references Customers; Guard references no business capability module.

## 3. What is borrowed and what is not

| Concern | SquiFlow direction | Useful reference idea | Explicitly not adopted |
|---|---|---|---|
| Dependency injection | one explicit composition root per executable; standard Microsoft.Extensions DI is current, and Autofac is selected for the first profile-aware implementation path | ABP/Orchard registration conventions; Autofac multitenant lifetime scopes and ASP.NET Core integration | service locator, automatic property injection, rebuilding containers per request, or treating a child scope as resource isolation |
| Capability composition | trusted C# descriptor, dependency DAG, ordered startup and explicit executable/adaptor composition around Capability Cores when composition metadata is needed | ABP dependency/lifecycle graph; Orchard module/feature split | two runtimes, arbitrary untrusted DLL loading, module-per-service, process-name registry in Foundation |
| Tenant composition | immutable, versioned Tenant Application Profile covering capability selection, settings, permissions, rules, workflows, forms, extensible information, integrations, placement and optional trusted implementation variants | Orchard tenant feature profiles; Autofac only for proven implementation variation | arbitrary tenant code, tenant-authored assemblies, or separate app/service provider/database per tenant by default |
| DDD/application services | use-case-oriented services and selective aggregates/value objects/domain services | ABP DDD guidance | mandatory layer/project/type for every CRUD feature |
| Transactions | explicit transaction per authoritative command where one store can own it | unit-of-work intent | generic IUnitOfWork baseline or cross-store ACID fiction |
| Data access | EF Core candidate for aggregate writes/migrations; Dapper candidate for measured read paths | ABP provider options | generic IRepository<T>, framework entities, unrestricted ad-hoc SQL |
| HTTP/API clients | explicit reviewed endpoints; generated OpenAPI/Protobuf clients when valuable | ABP generation convenience | automatic controller exposure for every application method |
| Settings | typed definitions, ordered value sources, validation, sensitivity and UI metadata when settings exist | ABP settings providers | security ceilings/invariants overridable by tenant values |
| Features | capability-declared features, dependencies, release channel and tenant activation when feature publication exists | Orchard enable/disable/dependency model | hot unloading CLR assemblies; features as authorization; backend process names on definitions |
| Permissions | capability-owned stable definitions, SquiFlow evaluator, OpenFGA relationship decisions when authorization scope exists | ABP definition/catalog concept | ABP/Orchard role store; ZITADEL claims as business permission truth; process-name permission filtering |
| Background work | capability-declared handlers plus SquiFlow durable Worker lifecycle when a durable workload exists | pluggable job-provider idea | ABP and Orchard queues concurrently; losing outbox/reconciliation semantics |
| Seeding | idempotent C# system seeds plus validated declarative JSON recipes when seeding/configuration scope exists | ABP contributors; Orchard recipes | arbitrary JSON-selected CLR types/scripts or unaudited production mutation |
| Audit | explicit security/administrative/business evidence with bounded technical tracing when the owning responsibility exists | ABP interception and Orchard content history as references | blanket payload/property capture or content revisions as business audit truth |
| UI contributions | reviewed navigation/page/block descriptors in the adapter that owns the presentation surface when contribution composition is needed | Orchard admin/navigation composition | generated UI as authorization enforcement or universal CRUD product |

The detailed source admission and framework comparison is `docs/review/APPLICATION_BASE_FRAMEWORK_ADMISSION_RESEARCH.md`; the active FSH decisions are in `docs/review/FULLSTACKHERO_BACKEND_ADOPTION_LEDGER.md`. The base is a controlled hybrid: the pinned FSH tree supplies the starting source map, standard .NET supplies runtime mechanics, SquiFlow owns product semantics, and focused packages own bounded mechanisms when earned.

These rows define ownership and candidate reference ideas; they do not require blank-page implementation or authorize every concern at once. Before custom infrastructure, create the source-admission record required by `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md`: immutable revision, exact source, license, entry mode, retained SquiFlow authority, framework assumptions, gaps, exit path, and SquiFlow-owned evidence. A framework may supply a focused mechanism without becoming the product runtime or business authority.

## 4. Module descriptor and dependency graph

A built-in capability may declare a stable descriptor when real executable composition or another current consumer needs composition metadata. The descriptor contains only metadata that capability owns, such as:

```text
ModuleId and ModuleVersion when stable module identity/version is needed
direct capability/module dependencies
feature definitions and feature dependencies when features exist
permission definitions when permissions exist
setting definitions when settings exist
migration/seed contributor contracts when actually needed
application use-case contribution contracts when actually needed
endpoint/generated-contract contribution contracts in the owning adapter
UI/navigation contribution contracts in the owning adapter
background handler contribution contracts when a Worker workload exists
compatibility requirements for contracts that actually exist
```

The descriptor must not enumerate `Workstation`, `Guard`, `CoreApi`, `WebApi`, `SyncApi`, `Worker`, `AdminApi`, `AdminWeb`, or any future executable as a stable capability property. Those are deployment/composition decisions and can change without changing business-capability meaning.

When a dependency graph is introduced, its owning composition boundary validates it before serving applicable runtime work for missing dependencies, cycles, duplicate identifiers, incompatible versions, or invalid contributions.

A descriptor dependency is a code/capability dependency, not a permission and not a tenant entitlement.

Module IDs, feature IDs, permission IDs, setting keys, job kinds, schema ownership and public contract names become stable compatibility vocabulary only when those identifiers/contracts are actually introduced. Renaming/removing an introduced stable identifier requires explicit compatibility/migration handling.

## 5. Trusted loading and runtime enable/disable

When executable module loading exists, initial releases load only assemblies shipped in the verified SquiFlow artifact. Adding/replacing assemblies is a deployment and normally a process restart.

If runtime tenant feature publication is introduced, feature enablement by itself changes availability data, not the process service graph:

```text
features present in this executable's composed capability set
∩ platform capability ceiling
∩ tenant-published activation
∩ dependency closure
∩ release-channel / compatibility / rollout constraints
= immutable effective feature snapshot for the operation
```

Feature availability, release channel, experiment assignment, permission and domain validity remain separate systems. See `docs/architecture/FEATURE_RELEASE_AND_EXPERIMENTS.md`.

A separate trusted implementation-variant selection in the Tenant Application Profile may select a qualified profile runtime. That is a different publication responsibility from an ordinary feature flag and follows the activation/drain lifecycle in `TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md`.

Do not build a child `IServiceProvider` merely to carry tenant values, branding, settings, secrets, limits, feature flags, forms, fields, rules or workflow definitions. Those belong to the immutable Tenant Application Profile and scoped `TenantContext`. If a published profile genuinely selects a different trusted shipped implementation graph, profile-aware composition may be introduced only after the focused POC in `TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md`. Such runtimes are created outside request execution, cached by a stable profile revision/fingerprint or tenant identity as proven by the POC, and never rebuilt on every request.

A feature publication, when introduced, is validated, versioned, audited and atomic from the tenant perspective. A request, command, sync batch or claimed job uses one effective revision rather than observing half of a publication.

Disabling an introduced feature:
- prevents new entry into its endpoints/commands/UI contributions;
- resolves dependent features according to explicit dependency rules;
- never deletes authoritative data automatically;
- never silently abandons/reinterprets pending work;
- follows declared drain/finish/cancel/quarantine/migrate policy;
- cannot disable an always-required safety/core feature;
- marks related grants dormant/visible rather than deleting them;
- requires authorized confirmation before dormant grants reactivate where that risk exists;
- increments relevant configuration/authorization evidence as defined.

A loaded assembly is not hot-unloaded. Untrusted micro-plugins, dynamic replacement and arbitrary tenant scripting remain deferred until signing, compatibility, isolation/sandboxing, upgrade, rollback, resource and support responsibilities are proven.

## 6. Dependency injection and executable composition

Each executable, once it exists, owns one ordinary .NET composition root. It explicitly references and composes only the capability modules/adapters it needs.

Illustrative shapes only:

```text
SquiFlow.Workstation
→ SquiFlow.Customers
→ SquiFlow.Customers.Workstation

compact authoritative API host
→ SquiFlow.Customers

SquiFlow.Guard
→ no business capability module
```

These examples are not current project claims.

A future executable does not require a kernel enum change. When its first real responsibility earns the process/project, its composition root selects the applicable modules/adapters.

Rules:
- constructor injection by default;
- no ambient service locator in Capability Core/domain/application code;
- no per-tenant mutable singleton;
- no retaining one tenant's service/context across requests/jobs;
- provider SDK types behind narrow adapters;
- replacement interfaces only for real provider/strategy boundaries;
- decorators/pipelines may own cross-cutting validation/authorization/transaction/idempotency/audit only with explicit ordering/failure semantics;
- no reflection-discovered future host registry merely to avoid explicit composition.

Ordinary tenant behavior comes from an immutable Tenant Application Profile and scoped context. Capability selection, forms, fields, rules, workflows, settings, limits and integrations do not require rebuilt DI containers. Many tenants may share one compiled profile runtime when their trusted implementation graph is identical.

A tenant-specific or profile-specific runtime is activated only when a supported profile needs a genuinely different trusted implementation type/graph inside the same executable and a small fixed strategy set would make ownership or lifecycle unsafe. The current executable still uses standard DI; Autofac is selected but not introduced. Its implementation plan requires membership-derived `TenantContext` before profile acquisition, immutable revision keys, deduplication of equivalent graphs where safe, bounded retained runtimes, in-flight draining before disposal, and reconstruction from durable profile authority on another node. A child lifetime scope remains only a composition/lifetime boundary. It does not provide CPU, memory, GC, thread-pool, database, provider, or process-fault isolation.

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

Hosts differ in fact providers and effects when those hosts exist:

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

## 8. Execution modes and authority

The accepted semantic vocabulary for operations that need execution/authority classification is:

```text
DeviceLocal
LocalProvisional
ServerAuthoritative
```

- `DeviceLocal`: device-only behavior with no server business effect.
- `LocalProvisional`: Workstation may prepare/store a provisional local projection and later submit the semantic operation for authoritative admission.
- `ServerAuthoritative`: current server authority required; cannot become authoritative offline.

Do not introduce a global enum merely because these terms are accepted architecture vocabulary. Introduce the narrow concrete representation when a real operation needs the distinction.

These values are semantic execution/authority categories, not process names.

The Workstation/server contract is `Local Preparation + Authoritative Admission/Commit + Reconciliation`. One semantic operation has one authoritative transition. See `docs/decisions/DUAL_PROCESSING_AND_IN_PROCESS_COORDINATION.md` and `docs/sync/SYNC_AND_AUTHORITY.md`.

Web normally enters authoritative application use cases directly once that surface exists. A future SyncApi receives semantic Workstation operations and performs current admission/reconciliation. Both reach the same Capability Core semantics rather than parallel business implementations.

## 9. Persistence, repositories, and unit of work

The **unit-of-work concept is accepted; a generic unit-of-work framework abstraction is not**.

For one authoritative command, once authoritative persistence exists:

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

PostgreSQL remains selected central transactional state and SQLite/WAL the selected Workstation local store under `docs/data/PERSISTENCE_SELECTION.md` when those scopes are introduced. Dedicated NoSQL and libSQL remain deferred; extensibility does not itself justify another database.

No transaction coordinator pretends PostgreSQL, SQLite, OpenFGA, ZITADEL, object storage and external providers form one ACID transaction. Cross-system effects use durable operation state, idempotency, verification and reconciliation when those systems are introduced.

## 10. Concurrency and dependency revisions

Expected-version optimistic concurrency is the ordinary edit direction where mutable authoritative state exists.

Each mutable aggregate/configuration publication defines, when introduced:
- concurrency token/version;
- command expected version;
- conflict result/user recovery;
- whether stronger isolation/constraint/lock is needed;
- how bounded whole-transaction retry remains idempotent.

ModuleFeatureRevision, SettingRevision, TenantAuthorizationRevision, workflow/rule/form revisions and data-row versions are distinct evidence when those concepts exist. Do not merge them into one magic global revision.

For Workstation provisional operations, retain only material dependency revisions needed for authoritative admission. The server may use unchanged revisions for a safe fast path and selectively re-evaluate affected decisions when material facts changed; mandatory authority/concurrency checks still run.

## 11. Settings system

When a capability needs configurable settings, it declares typed setting definitions with:
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

When the relevant responsibilities exist, keep these meanings separate:

```text
feature availability = is capability exposed by the composed application and enabled by current policy?
release channel      = what maturity audience may receive it?
experiment           = which safe approved variant is assigned?
setting              = how is it configured?
permission           = may this actor attempt it on this resource/scope?
domain rule          = is it valid for current business state?
limit/admission      = may it consume bounded resources now?
```

All applicable checks pass independently. Feature targeting does not grant permission. UI hiding is never authorization.

Accepted release-channel vocabulary is `Internal`, `Preview`, `Beta`, `Stable`, `Deprecated`; `Removed` means the feature definition/compatibility path is no longer exposed rather than a runnable channel. Do not create the machinery until a real release/feature responsibility needs it.

Workstation may consume a versioned effective feature snapshot for local/offline UX according to `SnapshotAllowed`, `StableOnly`, or `ServerRequired` policy when the feature/local-snapshot scope exists. Security/admin operations remain server-authoritative regardless of cached feature state.

A/B testing is limited to safe product/presentation alternatives and stable subject assignment; security, tenant isolation, financial/accounting correctness, inventory integrity and durability/concurrency invariants are not experiments.

Detailed owner: `docs/architecture/FEATURE_RELEASE_AND_EXPERIMENTS.md`.

## 13. Permission integration

When a capability introduces permission-protected operations, it publishes stable permission definitions to the SquiFlow catalog. Definitions may include group/parent metadata, resource/scope semantics, feature dependency, delegation risk and freshness class where those concepts are actually needed.

Permission definitions do not enumerate executable/process names. The presence of a Workstation/Web/API adapter does not change the permission's business meaning, and UI availability never grants authority.

The accepted future authoritative path is:

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

OpenFGA remains the selected relationship/authorization engine. SquiFlow owns permission vocabulary/catalog, configuration revisions, delegation ceilings, domain checks and durable/reconcilable admin workflows when implemented.

An online authorized Workstation may later expose selected tenant administration as a host adapter, but it never grants authority locally/offline and never receives OpenFGA administrative credentials. Platform administration remains Admin Web/Admin API only once those surfaces are implemented. See `docs/admin/ADMIN_SURFACES.md`.

## 14. HTTP, Sync API, gRPC, GraphQL, and generated clients

REST/task-oriented HTTP remains ordinary Web/external application API direction. Automatic REST controller exposure is off by default.

A capability contributes an endpoint only through an explicit reviewed contract stating tenant context, permission, idempotency, concurrency, validation, versioning, rate/limit, audit and failure behavior as applicable.

Generated clients are allowed from reviewed OpenAPI/Protobuf contracts when generation reduces drift. Generation never decides what is safe to expose.

Interactive Web/API traffic and Workstation sync traffic are accepted as separate future ingress/workload hosts when the split is implemented:

```text
WebApi  → interactive low-latency Web/tenant operations
SyncApi → workstation batching/idempotency/cursor/revision/backpressure workload
```

Both use the same authoritative application/Capability Core semantics and PostgreSQL once those responsibilities exist; they are not two business backends.

`services/core-api/` now contains the compact host earned by public bootstrap/liveness and authenticated account resolution. A later WebApi/SyncApi split is earned by real workload pressure rather than prebuilt now.

gRPC remains a preferred candidate for the Workstation sync boundary when a real POC proves value; sync semantics remain transport-independent. GraphQL remains deferred until a concrete query-composition use case pays for its cost/authorization/complexity.

Inside one host, capabilities call in-process application contracts.

Detailed owner: `docs/architecture/WEB_AND_SYNC_INGRESS.md`.

## 15. Background work

Capabilities may declare job/handler kinds and compatibility metadata when a real durable workload exists. The SquiFlow Worker owns durable execution when the first real workload earns that executable.

A job envelope, when introduced, identifies enough stable semantics for the workload, including tenant/scope, capability/handler kind/version, semantic operation/idempotency identity, payload/schema version, material configuration/feature revision, attempts/timing and trace/audit correlation as applicable.

Disabling a capability cannot make accepted durable work disappear. Accepted work explicitly finishes, pauses, migrates, cancels with outcome, or moves to quarantine/reconciliation according to policy.

Do not run multiple overlapping queue frameworks merely because they exist. The selected mechanism preserves SquiFlow outbox, claim/lease, retry, poison-work, fairness, reconciliation, observability and recovery requirements for the workload actually introduced.

## 16. Seeding and recipes

When seeding/configuration publication is needed, keep three distinct paths:

| Path | Use |
|---|---|
| Idempotent C# migration/seed contributor | invariant system records, schema-linked reference data, permission/setting definitions requiring compile-time ownership |
| Validated JSON recipe | reviewable declarative environment/tenant starter configuration, feature activation, sample/demo setup |
| Authenticated application/Admin command | interactive production changes with authorization, validation, versioning, audit and reconciliation |

A JSON recipe is data. It cannot execute arbitrary code, select arbitrary CLR types, contain secrets, bypass permissions, or mutate outside allow-listed handlers. Recipe schema/version and dry-run/diff are required before production use.

Seeds are repeatable and must not overwrite intentional tenant changes merely because an application restarts.

## 17. Audit and history

Keep distinct evidence once those responsibilities exist:
- security/administrative audit;
- business history;
- technical telemetry;
- content revision history only for a future content system.

Do not enable blanket request/body/entity-property capture. Redact secrets/sensitive fields, bound payload/cardinality/retention and measure overhead. High-risk capability/feature/setting/permission changes require durable audit even when technical traces are sampled.

## 18. Executable composition

The following table describes accepted runtime responsibilities. It is architecture narrative, **not an application-kernel enum and not a claim that these executables currently exist**.

| Executable/surface | Composition responsibility |
|---|---|
| Workstation | when implemented, explicitly reference local-capable capability cores plus Workstation adapters; consume published feature/permission/settings/rule snapshots that actually exist; execute only approved DeviceLocal/LocalProvisional operations |
| Guard | when implemented, supervision/update/diagnostic trigger responsibilities only; reference no business Capability Core, tenant authorization or central DB |
| CoreApi | current compact composition root for public bootstrap/liveness, configured bearer authentication, account resolution and active-membership listing; future business authority still requires OpenFGA/resource/domain checks and tenant-owned persistence |
| Tenant Web | future tenant business UI + broader Owner/Settings adapters; presentation only, server authority remains backend |
| WebApi | future earned interactive authoritative ingress; compose capability application adapters without duplicating business meaning |
| SyncApi | future earned Workstation sync/admission ingress; compose synchronization/application adapters without duplicating business meaning |
| Worker | future earned durable background executable; compose handlers for the workloads it actually owns |
| Admin API/Web | future separate platform-control executable/surface with platform permission scope |
| Customer Web | least-privilege customer-facing adapter subset if that boundary is accepted |
| Optional Orchard CMS | separate content authority only; no orders/payments/stock/roles as Orchard content |

One Capability Core can have different adapters/contributions without forcing every executable to reference every adapter/UI package.

## 19. Activation-time constraints and evidence direction

The following are architecture properties to prove **only when their underlying responsibility is introduced**. This list is not a Phase-0 implementation checklist and is not permission to manufacture features/settings/permissions/hosts so that evidence can exist.

- when a module dependency graph exists: prove deterministic ordering plus missing/cyclic/duplicate/incompatible dependency failure;
- when executable composition exists: prove explicit composition and that executable topology is not encoded in host-neutral capability metadata;
- whenever Foundation or a physically separated Capability Core exists: mechanically protect it from forbidden host/provider dependencies;
- when DI composition exists: preserve the explicit current root and prevent service-locator leakage; introduce a profile runtime only after its representative POC proves graph selection, cache bounds, revision publication, concurrency, drain/disposal and cross-tenant negative behavior;
- when tenant feature publication exists: prove dependency closure, platform ceiling, revision stability and disablement semantics for the declared scope;
- when release channels/experiments exist: prove their actual accepted assignment/filtering semantics and keep them separate from authorization;
- when typed settings exist: prove validation, precedence, ceilings, revision/history behavior required by that setting scope;
- when capability-owned permissions exist: evaluate them through the current SquiFlow authorization boundary once that boundary exists;
- when endpoint/direct-app/background/UI routes exist for a feature: mechanically prove disabled/unavailable scope cannot be entered through applicable routes;
- when durable work exists: prove disabling/configuration change cannot silently lose accepted work;
- when Workstation exists: protect it from ABP/Orchard/OpenFGA administration/central persistence leakage according to its actual boundary;
- when Guard exists: mechanically prevent business capability/persistence/key-authority ownership;
- at all times: do not create empty future WebApi/SyncApi/Worker/process projects merely to complete a diagram;
- at all times: do not create a process/executable-name enum in host-neutral capability metadata merely to describe current/future topology;
- before adopting reflection scanning/dynamic loading/UI auto-generation: measure and justify the current workload/operational trade-off.

Evidence class, exact tests, cadence and requalification triggers are derived from the real responsibility at activation time under `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`.

## 20. Alternatives and revisit triggers

Rejected as initial application-kernel directions:
- full ABP application foundation;
- full Orchard application foundation;
- ABP + Orchard together in the same business host;
- maximum custom scripting/plugin loading;
- separate per-tenant service containers as the default tenancy model or as a claim of resource/fault isolation;
- feature flags implemented as permission checks;
- ZITADEL application roles as business authorization source;
- a central enum/registry of executable process names used to filter capability/feature/permission meaning.

Revisit framework/package adoption only if measured implementation/support burden is materially lower than dependency, upgrade, security and replacement cost, and an executable proof shows it preserves current SquiFlow authority/offline/tenancy/persistence/composition boundaries.

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
- Application base framework admission research: `docs/review/APPLICATION_BASE_FRAMEWORK_ADMISSION_RESEARCH.md`
