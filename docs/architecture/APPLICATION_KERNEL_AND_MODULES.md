# SquiFlow Application Kernel, Modules, Settings, and Features

**Version:** v0.0.18  
**Status:** Accepted architecture direction; Phase 0 must prove the smallest implementation.  
**Authority:** This document owns application composition, module/feature lifecycle, dependency injection, settings, application-service and transaction conventions, module-provided endpoints/UI/background work, data seeding, and framework-adoption boundaries. Permission semantics remain owned by docs/security/TENANT_PERMISSIONS.md; identity remains owned by docs/security/IDENTITY_AND_SESSIONS.md.

## 1. Decision

SquiFlow will build a small **SquiFlow-owned application kernel** on standard .NET and ASP.NET Core primitives.

ABP Framework and Orchard Core are reference designs. They are not combined as runtime foundations, and neither becomes an application, tenant, settings, permission, workflow, persistence, audit, or background-work authority.

The selected shape is:

~~~text
trusted SquiFlow modules
→ explicit dependency graph and host contributions
→ standard Microsoft.Extensions dependency injection/hosting
→ per-tenant module/feature/settings snapshot
→ SquiFlow permission + domain + persistence boundaries
~~~

The Workstation, Core API, Worker, Admin API, tenant Web, and future customer Web can compose different parts of the same reviewed modules without depending on Volo.Abp.* or OrchardCore.*.

An optional Orchard CMS may still be evaluated later as a **separate content host** for accepted editorial/public-content responsibilities. It does not become the business application kernel.

## 2. What is borrowed and what is not

| Concern | SquiFlow direction | Useful reference idea | Explicitly not adopted |
|---|---|---|---|
| Dependency injection | Microsoft.Extensions.DependencyInjection with one composition root per executable | ABP/Orchard module registration conventions | framework service locator, automatic property injection, per-tenant container forests |
| Modules | trusted C# module descriptor, dependency DAG, ordered startup and host contributions | ABP dependency/lifecycle graph; Orchard module/feature split | two module runtimes, arbitrary untrusted DLL loading, module-per-service |
| Tenant composition | versioned effective module/feature snapshot resolved from TenantContext | Orchard tenant feature profiles | separate application instance/service provider/database per tenant by default |
| DDD/application services | use-case-oriented application services and selective aggregates/value objects/domain services | ABP DDD guidance | mandatory layer/project/type for every CRUD feature |
| Transactions | explicit transaction per authoritative command where one store can own it | unit-of-work intent | generic IUnitOfWork baseline or cross-store ACID fiction |
| Data access | EF Core candidate for aggregate writes/migrations; Dapper candidate for measured read paths | ABP provider options | generic IRepository<T>, framework entities, unrestricted ad-hoc SQL |
| HTTP/API clients | explicit reviewed endpoints; clients generated from approved OpenAPI/Protobuf when valuable | ABP generation convenience | automatic controller exposure for every application method |
| Settings | typed definitions, ordered value sources, validation, sensitivity and UI metadata | ABP setting definitions/providers | security ceilings or invariants overridable by tenant values |
| Features | module-declared features, dependencies and tenant activation | Orchard enable/disable/dependency model | hot unloading CLR assemblies or using features as authorization |
| Permissions | module-owned stable definitions, SquiFlow evaluator, OpenFGA relationship decisions | ABP definition/catalog concept; Orchard module-aware admin composition | ABP/Orchard role store, ZITADEL claims as business permission truth |
| Background work | module-declared handlers plus SquiFlow durable Worker lifecycle | pluggable job-provider idea | adopting ABP and Orchard queues concurrently or losing SquiFlow outbox/reconciliation semantics |
| Seeding | idempotent C# system seeds plus validated declarative JSON recipes | ABP contributors; Orchard recipes | arbitrary JSON-selected CLR types/scripts or unaudited production mutation |
| Audit | explicit security/administrative/business evidence with bounded technical tracing | ABP technical interception and Orchard content history as references | blanket payload/property capture or content revisions as business audit truth |
| UI contributions | reviewed navigation/page/block descriptors per host | Orchard admin/navigation composition | generated UI as authorization enforcement or a universal CRUD product |

These are pattern choices, not copied framework implementations. Any later package or source reuse requires an explicit dependency, lifecycle, license, upgrade, security, and removal review.

## 3. Module descriptor and dependency graph

Each built-in module declares a stable descriptor containing only the capabilities it owns:

~~~text
ModuleId and ModuleVersion
direct module dependencies
features and feature dependencies
supported host kinds
permission definitions
setting definitions
migration/seed contributors
application services/use cases
endpoint and generated-contract contributions
UI/navigation contributions
background handler contributions
compatibility requirements
~~~

The kernel validates the dependency graph at startup and fails before serving traffic for missing dependencies, cycles, duplicate identifiers, incompatible versions, or invalid contributions.

A module dependency is a code/capability dependency, not permission and not tenant entitlement.

Module IDs, feature IDs, permission IDs, setting keys, job kinds, schema ownership, and public contract names are stable compatibility vocabulary. Renaming or removing one requires an explicit migration/compatibility path.

## 4. Trusted loading and runtime enable/disable

Initial releases load only modules shipped in the verified SquiFlow artifact. Adding or replacing assemblies is a deployment and normally a process restart.

Runtime tenant enable/disable changes **availability data**, not the process service graph:

~~~text
host-supported features
∩ platform safety/capability ceiling
∩ tenant-published activation
∩ dependency closure
∩ compatibility/rollout constraints
= immutable effective feature snapshot for the operation
~~~

Do not build a child IServiceProvider for every tenant. Tenant-aware services receive an immutable TenantContext plus effective ModuleFeatureRevision through scoped context and query IModuleAvailability or IFeatureChecker where needed.

A feature publication is validated, versioned, audited, and atomic from the tenant's perspective. A request, command, sync batch, or claimed job uses one effective revision rather than observing half of an enable/disable change.

Disabling a feature:
- prevents new entry into its endpoints/commands/UI contributions;
- also resolves dependent features according to explicit dependency rules;
- never deletes authoritative data automatically;
- does not cancel, abandon, or reinterpret pending work silently;
- follows the module's declared drain/finish/cancel/quarantine/migrate policy;
- cannot disable an always-required safety/core feature;
- marks related grants dormant and visible in administration rather than deleting them;
- requires an authorized permission-diff confirmation before re-enabling can reactivate dormant grants;
- increments the relevant configuration/authorization snapshot revision.

A loaded assembly is not hot-unloaded. True third-party micro-plugins, dynamic assembly replacement, scripting, and untrusted tenant extensions are deferred until signing, compatibility, isolation/sandboxing, upgrade, rollback, resource, and support responsibilities are proven.

## 5. Dependency injection

Each executable owns one ordinary .NET composition root. Modules contribute registrations only for the hosts they support.

Rules:
- constructor injection is the default;
- no ambient service locator in domain/application code;
- no per-tenant mutable singleton;
- no resolving one tenant's service and retaining it across requests/jobs;
- provider SDK types remain behind narrow SquiFlow adapters;
- replacement interfaces exist for a real provider/strategy boundary, not automatically per class;
- decorators/pipelines may own cross-cutting validation, authorization, transaction, idempotency, and audit behavior only when ordering and failure semantics are explicit.

Per-tenant behavior comes from scoped context and policy/data, not from rebuilding dependency injection for each tenant.

## 6. Domain and application-service pattern

Use DDD tactical patterns where they protect real business rules:

- an aggregate root owns an invariant requiring one consistency/transaction boundary;
- an entity has identity and lifecycle;
- a value object gives a constrained value explicit meaning;
- a domain service owns domain logic that belongs to no one aggregate;
- an application service coordinates one named use case, authorization, loading, transaction and consequences.

Simple reference data or read-only projections need not become elaborate aggregates.

Application services expose SquiFlow contracts. They do not inherit ABP application-service bases and do not use Orchard content items as business entities.

## 7. Persistence, repositories, and unit of work

The **unit-of-work concept is accepted; a generic unit-of-work framework abstraction is not**.

For one authoritative command:

~~~text
authorize current attempt
→ load tenant-scoped state
→ validate expected versions and domain rules
→ begin/use explicit store transaction
→ persist mutation + idempotency receipt + outbox where co-owned
→ commit
→ dispatch only after commit
~~~

An EF Core DbContext/transaction may implement that boundary inside an adapter. A direct ADO.NET/Dapper transaction may implement it for a focused path. Application code should not call SaveChanges at arbitrary hidden layers.

Use EF Core when change tracking, relational mapping, migrations, aggregate persistence, and concurrency support reduce risk. Use Dapper for measured query/read-model paths or focused SQL where explicit mapping is clearer. Do not split one authoritative write across EF and an unrelated connection/transaction. If both participate in one-store work, deliberately share the same connection/transaction and prove behavior.

Create a domain-named repository only when an aggregate/query boundary benefits from it. Generic IRepository<T> and IUnitOfWork are not baseline.

PostgreSQL remains the central transactional store and SQLite/WAL the Workstation store under docs/data/PERSISTENCE_SELECTION.md. Dedicated NoSQL and libSQL remain deferred; module extensibility does not itself justify another database.

No transaction coordinator pretends PostgreSQL, SQLite, OpenFGA, ZITADEL, object storage, and external providers form one ACID transaction. Cross-system effects use durable operation state, idempotency, verification, and reconciliation.

## 8. Concurrency

Expected-version optimistic concurrency is the ordinary edit contract.

Each mutable aggregate/configuration publication defines:
- its concurrency token/version;
- the command's expected version;
- conflict result and user recovery;
- whether a narrowly stronger isolation level, database constraint, or lock is needed;
- how a bounded whole-transaction retry remains idempotent.

ModuleFeatureRevision, SettingRevision, TenantAuthorizationRevision, workflow/rule/form revisions, and data-row versions are distinct evidence. Do not merge them into one magic global revision.

## 9. Settings system

Modules declare typed setting definitions with:
- stable key and owner;
- type/default and validation;
- allowed scopes;
- sensitivity/client visibility;
- restart/reload behavior;
- display/localization/help metadata;
- compatibility/migration rules.

Effective precedence is narrow and deterministic:

~~~text
code default
→ deployment/platform value or policy
→ tenant value where explicitly allowed
→ user preference only for settings marked personal/presentation-only
~~~

A lower scope cannot exceed a platform/provider/security ceiling or override a hard invariant. Secrets are references/protected values and are never included in general client configuration.

Metadata may generate a basic Admin/Settings editor for simple safe values. Complex, destructive, security-sensitive, or workflow-changing configuration uses a purpose-built UI and preview/diff/validation/publication flow. Generated UI is convenience, not correctness.

## 10. Features, settings, permissions, and domain validity are different

~~~text
feature/module availability = does this deployment/tenant expose the capability?
setting = how is an available capability configured?
permission = may this actor attempt this capability on this scope/resource?
domain rule = is this action valid for the current business state?
limit/admission = may it consume the required bounded resource now?
~~~

All applicable checks must pass. Enabling a feature grants no role. Granting a permission does not enable a module. Hiding a Web/Workstation block is not authorization.

Attributes such as RequiresFeature or RequiresPermission may be used as readable boundary declarations on endpoints/application methods/components. They are adapters to the same SquiFlow services, not alternate decision engines. Commands still protect themselves at the authoritative server/application boundary, and resource/domain checks still run.

## 11. Permission integration

Modules publish stable permission definitions to the SquiFlow permission catalog. A definition can include group/parent metadata, supported hosts/scopes, feature dependency, delegation risk, and required freshness class.

The effective decision path is:

~~~text
ZITADEL-authenticated (issuer, subject)
→ SquiFlow account + authoritative TenantContext
→ module/feature available at one published revision
→ permission definition exists and applies to this host/scope
→ OpenFGA relationship/role/resource check at required consistency
→ delegation and platform/tenant boundary checks
→ SquiFlow domain/workflow/concurrency/limit validation
~~~

ZITADEL can supply authentication strength and recency for step-up. It is not the store of current orders.refund, inventory.adjust, roles.manage, tenant resource relationships, or platform super-admin authority.

OpenFGA remains the relationship/authorization engine. SquiFlow owns permission vocabulary, catalog metadata, configuration revisions, delegation ceilings, domain checks, and the durable/reconcilable administrative change workflow.

The complete permission contract is in docs/security/TENANT_PERMISSIONS.md.

## 12. HTTP, gRPC, GraphQL, and generated clients

REST/task-oriented HTTP remains the normal Web/external application API. Automatic REST controller exposure is **off by default**.

A module may contribute an endpoint only through an explicit reviewed contract that states tenant context, permission, idempotency, concurrency, validation, versioning, rate/limit, audit, and failure behavior as applicable.

Generated clients are allowed from reviewed OpenAPI or Protobuf contracts when generation reduces drift. Generated output is reproducible, versioned or reproducibly produced in CI, and protected by compatibility tests; generation never decides what is safe to expose.

gRPC remains a candidate for the Workstation sync or another real synchronous process boundary. GraphQL remains deferred until a concrete query-composition use case pays for field/resource authorization, query cost/depth, N+1, caching, and schema-evolution complexity.

Inside one host, modules call in-process application contracts.

## 13. Background work

Modules declare job/handler kinds and compatibility metadata. The SquiFlow Worker owns the durable execution mechanism when the first real workload exists.

The job envelope identifies at least the tenant/scope, module and handler kind/version, semantic operation/idempotency identity, payload/schema version, configuration/feature revision where material, attempts, timing, and trace/audit correlation.

Disabling a module cannot make durable work disappear. The module declares whether already-accepted work finishes, pauses, migrates, cancels with an explicit outcome, or moves to quarantine/reconciliation.

Do not run ABP Background Jobs beside Orchard background tasks. The selected mechanism must preserve SquiFlow outbox, claim/lease, retry, poison-work, fairness, reconciliation, observability, and recovery requirements.

## 14. Seeding and recipes

Use three distinct paths:

| Path | Use |
|---|---|
| Idempotent C# migration/seed contributor | invariant system records, schema-linked reference data, permission/setting definitions requiring compile-time ownership |
| Validated JSON recipe | reviewable declarative environment/tenant starter configuration, feature activation, sample/demo setup |
| Authenticated application/Admin command | interactive production changes with authorization, validation, versioning, audit and reconciliation |

A JSON recipe is data. It cannot execute arbitrary code, select arbitrary CLR types, contain secrets, bypass permissions, or mutate outside allow-listed handlers. Recipe schema/version and dry-run/diff are required before production use.

Seeds are repeatable and must not overwrite intentional tenant changes merely because an application restarts.

## 15. Audit and history

Keep distinct evidence:

- security/administrative audit: actor, tenant/scope, action, target, before/after diff where safe, revision, outcome and correlation;
- business history: issued values, workflow transitions, corrections/reversals and domain explanations;
- technical telemetry: bounded traces/logs/metrics for diagnosis;
- content revision history: only for a future content system and never a substitute for business audit.

Do not enable blanket request/body/entity-property capture. Redact secrets and sensitive fields, bound payload/cardinality/retention, and measure overhead. High-risk module/feature/setting/permission changes require durable audit even when ordinary technical traces are sampled.

## 16. Host composition

| Host | Initial kernel use |
|---|---|
| Workstation | compose local-capable application/UI/store adapters; consume published feature/permission/settings snapshots; no ABP/Orchard or server provider SDKs |
| Guard | only supervision/update/diagnostic contributions; no business modules, tenant authorization or central DB |
| Core API | tenant HTTP composition, identity adapter, TenantContext, module availability, permission/OpenFGA adapter, domain application services and central persistence |
| Tenant Web | tenant business UI plus Owner Settings contributions; UI checks improve UX but Core API enforces authority |
| Worker | only created for the first durable workload; composes background handlers without UI/controllers |
| Admin API/Web | separate platform-control host/surface with platform permission scope and reviewed module administration |
| Customer Web | least-privilege subset of customer-facing application/UI contributions if its boundary is accepted |
| Optional Orchard CMS | separate deployable content authority only; no orders/payments/stock/roles as Orchard content |

One module can provide different host contributions, but it does not force every host to reference every adapter/UI package.

## 17. Phase-0 proof obligations

Phase 0 implements only the kernel slice needed by the first launchable hosts and one sample business module.

Prove:
- module graph ordering, missing/cyclic dependency failure, and host filtering;
- standard DI composition without per-tenant containers;
- one tenant feature enable/disable publication with dependency closure and revision stability;
- one typed setting with platform ceiling and tenant override;
- one module-owned permission definition evaluated through the SquiFlow service/OpenFGA adapter boundary;
- disabled feature cannot be reached through endpoint, direct application-service call, background enqueue, or UI route;
- disabling never deletes data or loses accepted durable work;
- generated endpoint/client support is absent unless explicitly enabled by a reviewed contract;
- Workstation does not reference ABP, Orchard, OpenFGA administration, or central persistence packages;
- framework/package types do not leak into domain/application contracts;
- startup and memory cost are measured before adding reflection scanning, dynamic loading, or UI auto-generation.

## 18. Alternatives and revisit triggers

Rejected as the initial application kernel:
- full ABP application foundation;
- full Orchard application foundation;
- ABP and Orchard together in the same business host;
- maximum custom scripting/plugin loading;
- separate per-tenant service containers;
- feature flags implemented as permission checks;
- ZITADEL application roles as the business authorization source.

Revisit framework/package adoption only if a measured implementation/support burden is materially lower than the dependency, upgrade, opinion, security, and replacement cost, and an executable proof shows it preserves SquiFlow's current authority, offline, tenancy, persistence, and host boundaries.

## Source basis

- ABP modularity and dependency lifecycle: https://abp.io/docs/latest/framework/architecture/modularity/basics
- ABP plug-in modules: https://abp.io/docs/latest/framework/architecture/modularity/plugin-modules
- ABP authorization definitions: https://abp.io/docs/latest/framework/fundamentals/authorization
- ABP settings/value providers: https://abp.io/docs/latest/framework/infrastructure/settings
- Orchard Core features: https://docs.orchardcore.net/en/latest/reference/modules/Features/
- Orchard Core tenant feature profiles: https://docs.orchardcore.net/en/latest/reference/modules/Tenants/
- Orchard Core roles/recipe configuration: https://docs.orchardcore.net/en/latest/reference/modules/Roles/
- Microsoft .NET dependency injection: https://learn.microsoft.com/dotnet/core/extensions/dependency-injection
- ASP.NET Core authorization: https://learn.microsoft.com/aspnet/core/security/authorization/introduction
- OpenFGA custom roles: https://openfga.dev/docs/modeling/custom-roles
- OpenFGA consistency: https://openfga.dev/docs/interacting/consistency
- ZITADEL OIDC: https://zitadel.com/docs/guides/integrate/login/oidc
- ZITADEL self-hosting: https://zitadel.com/docs/self-hosting/deploy/overview
