# Application Baseline Implementation Source Review

**Reviewed:** 2026-09-17

**Status:** active source-study and implementation-routing evidence; first backend slice implemented

**Input catalog:** `/home/lets-smile/Downloads/application_baseline_reference_catalog.md`

**Repository catalog copy:** `# Application Baseline, Reference Projec.md` — living user-supplied input; current focused owners and accepted decisions still take precedence

**Curated local source workspace:** `reference-sources/` — tracked manifest/materializer plus ignored source-only snapshots; no upstream Git histories and no product project references

**Application-base decision:** `docs/review/APPLICATION_BASE_FRAMEWORK_ADMISSION_RESEARCH.md` — detailed comparison of standard .NET, ABP, Orchard, FullStackHero, Oqtane, ExtCore, SimplCommerce and Serenity

**Current implementation truth:** `README.IMPLEMENTATION.md`

## 1. Outcome

SquiFlow will not begin each infrastructure responsibility from a blank file. Before custom implementation, an active slice must inspect the most relevant proven implementation and choose one entry mode:

1. use a focused package;
2. adapt bounded source under SquiFlow ownership when its license permits the intended use and distribution;
3. reuse tests, failure cases, and algorithms while writing the SquiFlow-specific boundary;
4. keep the source as reference after recording why direct reuse does not fit.

This does not make a reference framework the product architecture. SquiFlow still owns business meaning, authority, security boundaries, compatibility, and the production-honest claim.

License does not exclude a source from internal research. Research admission, source copying, package adoption and product distribution are separate decisions. Record obligations before copied source or a dependency enters product code.

The post-purge repository now contains the first earned backend slice. FullStackHero is retained as the complete pinned source-owned backend base, while each admitted mechanism is rewritten under SquiFlow ownership. The live part-by-part decisions are recorded in `FULLSTACKHERO_BACKEND_ADOPTION_LEDGER.md`.

## 2. Required source-admission record

Before custom infrastructure is created, record:

```text
required SquiFlow responsibility
upstream repository + immutable revision
exact source types/tests inspected
license and attribution obligations
package / adapted source / test donor / reference-only decision
SquiFlow authority retained outside the borrowed mechanism
framework assumptions and dependencies removed or isolated
failure/security/compatibility gaps SquiFlow must close
replacement/upgrade exit path
SquiFlow-owned falsifiable tests
```

Custom code is appropriate for product-specific behavior or a demonstrated candidate gap. “We usually build our own” is not a sufficient gap analysis.

## 3. Reviewed implementations

| Source | Revision | License | Admission result | Concrete value |
|---|---|---|---|---|
| [Finbuckle.MultiTenant](https://github.com/Finbuckle/Finbuckle.MultiTenant/tree/ad67b15ecb6158f041abbb0c39ae4d718f3fda42) | `ad67b15ecb6158f041abbb0c39ae4d718f3fda42` | Apache-2.0 | focused package candidate when a tenant-aware ASP.NET Core or EF Core boundary exists | tenant resolution/context plumbing and EF tenant-integrity mechanics |
| [Autofac](https://github.com/autofac/Autofac) + [Microsoft DI integration](https://github.com/autofac/Autofac.Extensions.DependencyInjection) | `9.3.4`; `11.0.2` | MIT | current CoreApi root-provider dependency | standard `IServiceCollection` compatibility and explicit child/operation lifetime scopes |
| [Autofac.Multitenant](https://github.com/autofac/Autofac.Multitenant/tree/2fdd4c0fc6a913324f5b985184d73f484db501e4) + [ASP.NET Core integration](https://github.com/autofac/Autofac.AspNetCore.Multitenant/tree/42851fdc88d2a988f266e70d108064210c0559d2) | `2fdd4c0fc6a913324f5b985184d73f484db501e4`; `42851fdc88d2a988f266e70d108064210c0559d2` | MIT | source/test donor; packages not referenced | cached lifetime-scope concurrency/disposal cases and a comparison point for trusted implementation overrides |
| [FullStackHero .NET Starter Kit](https://github.com/fullstackhero/dotnet-starter-kit/tree/3f2959e683e9f83f13e55e1678c9119f63c7e8e5) | `3f2959e683e9f83f13e55e1678c9119f63c7e8e5` | MIT | complete source-owned backend base with selective adaptation; no generator/runtime dependency | repository/host/test shape plus reviewed tenant, persistence, migration and operational mechanisms |
| [OpenFGA .NET SDK](https://github.com/openfga/dotnet-sdk/tree/ec8ee04761b41e2400693b911a17463877e500c3) | `ec8ee04761b41e2400693b911a17463877e500c3` | Apache-2.0 | focused package candidate for the first real OpenFGA-backed operation; no host-neutral provider types | pinned store/model checks, consistency selection, provider error/cancellation behavior and official client maintenance |
| [Casbin.NET](https://github.com/apache/casbin-Casbin.NET/tree/30b142f0f5c4598852e8258d638bded3e24caf2c) + [EF Core adapter](https://github.com/apache/casbin-efcore-adapter/tree/1cc2c9ae985e48a93c38d1b884095502d15d52f8) | `30b142f0f5c4598852e8258d638bded3e24caf2c`; `1cc2c9ae985e48a93c38d1b884095502d15d52f8` | Apache-2.0 | POC-gated OpenFGA replacement candidate; never a parallel authorization authority | in-process policy evaluation, tenant-domain RBAC, filtered PostgreSQL policy loading, persistence and replica-freshness trade-offs |
| [Orchard Core](https://github.com/OrchardCMS/OrchardCore/tree/b304fcd78a70b792c6f63916c0ce6e6957bb1aa0) | `b304fcd78a70b792c6f63916c0ce6e6957bb1aa0` | BSD-3-Clause | feature/provisioning model and test donor; no Orchard shell/CMS runtime | feature dependencies, enable/disable mechanics, recipe-step dispatch and failures |
| [Prism](https://github.com/PrismLibrary/Prism/tree/358118cd640d9a22ff8cf21c8ad197fa038b7990) | `358118cd640d9a22ff8cf21c8ad197fa038b7990` | Community or Commercial license | Workstation behavior donor; dependency requires license and composition POC | Avalonia regions, navigation, dialogs, commands and view lifecycle |
| [Stateless](https://github.com/dotnet-state-machine/stateless/tree/588f1a1a08683b452eb7c05562d9f055693cba5d) | `588f1a1a08683b452eb7c05562d9f055693cba5d` | Apache-2.0 | focused package candidate for a small deterministic entity lifecycle | guarded/async transitions, external state storage and graph inspection |
| [Elsa Workflows](https://github.com/elsa-workflows/elsa-core/tree/aa021de39ee1323c212190a5f561b45d858206ec) | `aa021de39ee1323c212190a5f561b45d858206ec` | MIT | package candidate only after a durable long-running process exists | bookmarks, commit transaction, dispatch outbox and interrupted-instance recovery |
| [Temporal .NET SDK](https://github.com/temporalio/sdk-dotnet/tree/4a183307d90d6291fc213941a4f8d2506bd85800) | `4a183307d90d6291fc213941a4f8d2506bd85800` | MIT | POC-gated alternative for developer-authored durable orchestration; requires a superseding runtime decision | replay-based workflow execution, activities, signals/updates, child workflows, schedules and worker integration |
| [ABP Framework](https://github.com/abpframework/abp/tree/955a7876537ebeaedbcb81e5407facb0840616bb) | `955a7876537ebeaedbcb81e5407facb0840616bb` | LGPL-3.0 | framework/contract/test donor; no ABP runtime foundation | module dependency/lifecycle, feature, setting, permission, audit, tenancy and UoW cases |
| [Oqtane Framework](https://github.com/oqtane/oqtane.framework/tree/b5e76441a4139966beb9327708ce39a59764787d) | `b5e76441a4139966beb9327708ce39a59764787d` | MIT | admin UX/metadata donor; no page/CMS/package runtime | host-admin versus site-admin, module catalog and administrative composition |
| [ExtCore](https://github.com/ExtCore/ExtCore/tree/3d10fcb358e3828b42e138fbbc942ef14fd2fe2a) | `3d10fcb358e3828b42e138fbbc942ef14fd2fe2a` | Apache-2.0 | simplicity counterexample; no dependency | minimal extension discovery and ordered startup actions |
| [SimplCommerce](https://github.com/simplcommerce/SimplCommerce/tree/3472ba02a6f2d9b6bdca7f7fb84957176aa799dc) | `3472ba02a6f2d9b6bdca7f7fb84957176aa799dc` | Apache-2.0 | manifest/lifecycle donor; no commerce runtime | compact module metadata and service/middleware contribution |
| [Serenity](https://github.com/serenity-is/Serenity/tree/2d854c6550436d957945898867194ab8260f946f) | `2d854c6550436d957945898867194ab8260f946f` | MIT | future forms/grids donor; no application runtime | property metadata, provider pipeline and presentation permission behavior |

Package adoption must still pin an exact released version and recheck that release's source, license, target framework, advisories, and transitive graph.

The local snapshot selection is narrower than the input catalog. `reference-sources/SOURCES.md` retains only currently selected mechanisms, focused candidates, and bounded donors. Documentation-only references, rejected alternatives, and large unrelated platforms are not materialized merely because the catalog names them.

## 4. Tenancy route

### Finbuckle supplies plumbing

The inspected implementation supplies:

- ordered strategies and stores in [`TenantResolver<TTenantInfo>`](https://github.com/Finbuckle/Finbuckle.MultiTenant/blob/ad67b15ecb6158f041abbb0c39ae4d718f3fda42/src/Finbuckle.MultiTenant/TenantResolver.cs);
- ASP.NET Core strategies under [`Finbuckle.MultiTenant.AspNetCore/Strategies`](https://github.com/Finbuckle/Finbuckle.MultiTenant/tree/ad67b15ecb6158f041abbb0c39ae4d718f3fda42/src/Finbuckle.MultiTenant.AspNetCore/Strategies);
- request tenant context accessors;
- EF query filters, write integrity, tenant-aware unique indexes, and mismatch/not-set handling under [`Finbuckle.MultiTenant.EntityFrameworkCore`](https://github.com/Finbuckle/Finbuckle.MultiTenant/tree/ad67b15ecb6158f041abbb0c39ae4d718f3fda42/src/Finbuckle.MultiTenant.EntityFrameworkCore);
- tests for strategy ordering, resolution events, mismatches, missing tenants, query filters, and indexes.

When a tenant-aware host is earned, prefer released Finbuckle packages over rebuilding resolver/context/EF mechanics. SquiFlow retains its Tenant lifecycle, authenticated membership resolution, immutable authoritative `TenantContext`, OpenFGA/domain authorization, placement/provisioning policy, PostgreSQL RLS, and audit meaning.

A hostname, header, route, query value, Workstation value, or token claim may select a candidate tenant. It is not authoritative scope without current membership/authority validation. Finbuckle resolves identifiers; SquiFlow authorizes the context.

Finbuckle EF filters remain defense in depth. They do not replace PostgreSQL RLS, application scoping, authorization, pooled-connection context reset, or bypass-path tests.

### Autofac multitenant DI does not replace tenant context or isolation

The pinned source confirms a narrower capability than several secondary comparisons imply:

- `MultitenantContainer` owns a root application container and a `ConcurrentDictionary<object, ILifetimeScope>` of tenant scopes;
- first access creates a tenant lifetime scope and later access returns that same scope;
- `ConfigureTenant` adds tenant-specific registrations, while `InstancePerTenant` shares one component instance inside a tenant scope;
- ASP.NET Core integration replaces the service-provider factory and request-services middleware so the ordinary request scope is created from the identified tenant scope;
- `RemoveTenant`, `ClearTenants` and `ReconfigureTenant` dispose tenant scopes; reconfiguration swaps and rebuilds rather than mutating an existing scope.

It does not supply an LRU/idle eviction policy, distributed tenant-scope cache, cross-node revision protocol, resource quotas, fair scheduling, database isolation, secret rotation, actor supervision, or durable workflow execution. Tenant lifetime scopes remain in the same process, managed heap and shared execution resources. They are composition/lifetime boundaries, not security, memory, CPU, thread-pool or noisy-neighbor isolation boundaries.

The current CoreApi runtime uses Autofac as its root provider while preserving its existing `IServiceCollection` registrations. It also contains an internal SquiFlow-owned bounded registry around ordinary Autofac lifetime scopes. The registry is exercised only by isolated tests until profile authority and a real implementation variant exist. The accepted target adds an immutable Tenant Application Profile containing feature, setting, permission, rule, workflow, form, extensible-information, integration, placement and implementation-variant revisions. Most profile variation remains validated data over shared implementations. Where a published profile genuinely changes a trusted implementation graph, a disposable compiled profile runtime may be cached locally without becoming authority: losing it must cause a bounded rebuild from the durable profile, not incorrect behavior or lost business state.

| Concern | Current SquiFlow model | Autofac multitenant model |
|---|---|---|
| Executable composition | one Autofac-backed root plus an internal bounded registry of immutable profile runtimes; production profile acquisition remains absent | root graph plus one cached lifetime scope per encountered/configured tenant |
| Request work | create ordinary request scope; derive `TenantContext`; load/select bounded versioned facts | identify tenant; find/create tenant scope; create ordinary request scope below it |
| Tenant variation | profile data first; trusted implementation variants only when lower variation layers cannot express the requirement safely | component registration/implementation overrides and per-tenant component lifetime |
| Configuration change | publish a new immutable revision and let bounded caches expire/replace | remove/reconfigure and dispose/rebuild the tenant scope |
| Memory growth | active requests plus explicitly bounded fact/client caches | active requests plus tenant scope/container metadata and any per-tenant instances for every retained tenant scope |
| Multi-node behavior | every node can serve every tenant by reading authority; local caches warm independently | every node independently creates/retains tenant scopes and needs a separate revision/eviction protocol |
| Resource isolation | none from DI; enforce resource-specific admission/fairness/limits | none from the child scope; the same process/heap/runtime resources remain shared |

Both approaches hold disposable process-local acceleration state. That alone does not make the service depend on stateful routing: a node may disappear and another node can rebuild from durable authority. The architecture becomes operationally stateful when correctness or continuity depends on the local tenant scope, a sticky route, or mutable per-tenant singleton state that is not durably represented elsewhere.

Therefore Autofac is now the **qualified CoreApi root provider**, and the internal runtime registry is evidence for the later Tenant Application Profile path. SquiFlow does not currently reference `Autofac.Multitenant`: its pinned source supplied concurrency and disposal cases, while the package's root-owning, unbounded tenant dictionary and immediate reconfiguration disposal do not satisfy SquiFlow's explicit post-membership acquisition, cache bound and in-flight drain requirements. Finbuckle remains the focused candidate for host/EF tenant plumbing when that boundary is earned, while SquiFlow retains profile/context authority, persistence isolation and resource governance.

Activate production profile-specific resolution when the first representative Tenant Application Profile needs a different trusted implementation type or shipped module graph in the same executable and the requirement cannot be expressed safely through capability selection, typed configuration, versioned forms/fields, rules/workflows, integrations or a small fixed strategy set. Different values alone—connection strings, keys, branding, limits or rule definitions—still do not justify a separate runtime.

Any future POC must prove:

1. authoritative tenant identification before tenant-specific resolution, including non-HTTP Worker paths;
2. cross-tenant negative resolution and secret-lifetime tests;
3. bounded memory and cold/warm resolution measurements at representative active-tenant counts;
4. safe concurrent request/job behavior during tenant removal, reconfiguration and secret/config revision rollout;
5. deterministic multi-node revision convergence without treating local scopes as authority;
6. startup, disposal, drain and rollback behavior;
7. separate admission, concurrency, queue fairness, database/provider and consumption controls for noisy-neighbor protection.

Compare Lamar or DryIoc only if the Autofac implementation evidence exposes a concrete lifecycle, performance, compatibility or memory gap. Faster or more dynamic child-scope mechanics do not solve profile authority, resource governance, persistence isolation or process-fault containment.

### FullStackHero supplies tests and counterexamples

Useful material includes:

- [`ApplyTenantIsolationByDefault`](https://github.com/fullstackhero/dotnet-starter-kit/blob/3f2959e683e9f83f13e55e1678c9119f63c7e8e5/src/BuildingBlocks/Persistence/TenantIsolationExtensions.cs);
- [`TenantIsolationTests`](https://github.com/fullstackhero/dotnet-starter-kit/blob/3f2959e683e9f83f13e55e1678c9119f63c7e8e5/src/Tests/Architecture.Tests/TenantIsolationTests.cs);
- [`ModuleArchitectureTests`](https://github.com/fullstackhero/dotnet-starter-kit/blob/3f2959e683e9f83f13e55e1678c9119f63c7e8e5/src/Tests/Architecture.Tests/ModuleArchitectureTests.cs) and [`BuildingBlocksIndependenceTests`](https://github.com/fullstackhero/dotnet-starter-kit/blob/3f2959e683e9f83f13e55e1678c9119f63c7e8e5/src/Tests/Architecture.Tests/BuildingBlocksIndependenceTests.cs);
- explicit state and steps under [`Modules.Multitenancy/Provisioning`](https://github.com/fullstackhero/dotnet-starter-kit/tree/3f2959e683e9f83f13e55e1678c9119f63c7e8e5/src/Modules/Multitenancy/Modules.Multitenancy/Provisioning).

Adapt these to SquiFlow names and boundaries. Do not copy the `BaseDbContext.SaveChangesAsync` policy that overwrites missing tenant values without proving it cannot hide caller/stale-context errors. SquiFlow should fail closed on mismatches and assign a new entity's tenant through a controlled path.

Do not copy FullStackHero's request-selected root-operator tenant override or query/header selection as authority. They conflict with SquiFlow's private Admin control plane and authoritative membership/context rules.

### OpenFGA SDK supplies the provider boundary

The pinned official SDK source exposes explicit store and authorization-model IDs, per-check consistency, cancellation and the generated provider contracts. When the first real permission-protected operation is introduced, prefer the released `OpenFga.Sdk` package over a hand-written HTTP client. Keep the SDK inside an authorization provider adapter; host-neutral SquiFlow contracts use opaque account/tenant/resource IDs, stable permission IDs and SquiFlow-owned allow/deny/unavailable outcomes.

Do not introduce OpenFGA through a probe/demo endpoint. The first model must be earned by a real capability operation, pin its model ID on every check, use opaque non-PII tuple identifiers, fail closed on timeout/provider error, and run against a real isolated OpenFGA server. The likely first candidate is tenant branding/settings administration because white-label configuration is already a requested capability, but that implementation must introduce its persistence, concurrency, audit and authorization obligations together.

Casbin.NET is now retained as a source-backed replacement candidate, not an additional layer. `docs/review/CASBIN_NET_AUTHORIZATION_ADMISSION_REVIEW.md` owns the comparison and proof gates. The existing OpenFGA selection remains current until those gates produce a deliberate superseding decision.

## 5. Capability composition route

The complete application-base comparison and selected hybrid are recorded in `docs/review/APPLICATION_BASE_FRAMEWORK_ADMISSION_RESEARCH.md`. Standard .NET hosting/DI is the runtime spine. No reviewed full application framework becomes application authority.

FullStackHero's [`ModuleLoader`](https://github.com/fullstackhero/dotnet-starter-kit/blob/3f2959e683e9f83f13e55e1678c9119f63c7e8e5/src/BuildingBlocks/Web/Modules/ModuleLoader.cs) keeps global static state, discovers attributes, constructs modules with `Activator.CreateInstance`, and lets one module configure services, middleware, and endpoints. It lacks SquiFlow's possible future dependency DAG, duplicate-ID/version validation, and capability/host-adapter split.

Reuse the project/reference architecture-test approach. Keep composition explicit while there are few capabilities. Introduce a descriptor graph only when a real executable has enough dependencies/contributions to require it, then adapt proven ordering and failure tests rather than copying the loader wholesale.

## 6. Features and provisioning route

Orchard's [`IFeatureInfo`](https://github.com/OrchardCMS/OrchardCore/blob/b304fcd78a70b792c6f63916c0ce6e6957bb1aa0/src/OrchardCore/OrchardCore.Abstractions/Extensions/Features/IFeatureInfo.cs) demonstrates identity, ordering, dependencies, always-enabled features, and dependency-only activation. [`ShellFeaturesManager`](https://github.com/OrchardCMS/OrchardCore/blob/b304fcd78a70b792c6f63916c0ce6e6957bb1aa0/src/OrchardCore/OrchardCore/Shell/ShellFeaturesManager.cs) demonstrates enumeration, validation, and updates.

Adapt dependency/lifecycle cases, not Orchard's per-tenant shell/service-provider runtime. SquiFlow activation remains versioned data over one reviewed shipped capability set.

[`RecipeExecutor`](https://github.com/OrchardCMS/OrchardCore/blob/b304fcd78a70b792c6f63916c0ce6e6957bb1aa0/src/OrchardCore/OrchardCore.Recipes.Core/Services/RecipeExecutor.cs) and [`FeatureStep`](https://github.com/OrchardCMS/OrchardCore/blob/b304fcd78a70b792c6f63916c0ce6e6957bb1aa0/src/OrchardCore.Modules/OrchardCore.Features/Recipes/Executors/FeatureStep.cs) show named JSON steps, ordered execution, per-step outcomes, cancellation, nested plans, and feature activation. A future SquiFlow `ProvisioningPlan` can reuse those mechanics through bounded typed handlers.

Do not copy Orchard scripting, arbitrary handler discovery, shell reload, CMS steps, or force-enabled semantics. Validate an allow-listed versioned schema and the complete dependency closure before mutation, and define idempotent retry/resume plus durable/auditable outcomes when the responsibility exists.

## 7. Workstation route

The detailed Workstation comparison, exact source admissions, functionality routing, proof gates and phased implementation route are owned by `docs/review/WORKSTATION_FRAMEWORK_ADMISSION_RESEARCH.md`. Its selected hybrid is:

```text
direct when the first useful slice is activated:
  Avalonia + CommunityToolkit.Mvvm + minimum Microsoft.Extensions primitives

owned by SquiFlow:
  typed contribution/navigation/workspace/action contracts
  edit, local-authority and sync-state meaning

focused and POC-gated:
  Dock.Avalonia, native OIDC, Windows protected secrets,
  SQLite/encryption, updater, reporting/printing and device adapters

behavior/test donors only:
  Prism, Eclipse RCP, NetBeans, XAF, Uno.Extensions, CSLA,
  Tryton Desktop and Odoo POS/client
```

Prism's current Avalonia source targets .NET 10 and implements real region navigation, dialogs, commands, and startup. Inspected points include [`IRegionManager`](https://github.com/PrismLibrary/Prism/blob/358118cd640d9a22ff8cf21c8ad197fa038b7990/src/Prism.Core/Navigation/Regions/IRegionManager.cs), Avalonia [`DialogService`](https://github.com/PrismLibrary/Prism/blob/358118cd640d9a22ff8cf21c8ad197fa038b7990/src/Avalonia/Prism.Avalonia/Dialogs/DialogService.cs), and the [.NET 10 sample](https://github.com/PrismLibrary/Prism/blob/358118cd640d9a22ff8cf21c8ad197fa038b7990/e2e/Avalonia/PrismAvaloniaDemo/App.axaml.cs).

Prism source remains admitted for internal research regardless of license. It is not the default runtime dependency. Reconsider runtime adoption only if:

1. SquiFlow qualifies for and accepts the Community license or obtains a Commercial license;
2. DryIoc and service-locator-oriented navigation can be isolated without replacing ordinary Microsoft.Extensions composition or moving business authority into the shell.

Before writing common Workstation mechanics, qualify:

- [`CommunityToolkit.Mvvm`](https://github.com/CommunityToolkit/dotnet/tree/b135626dd54d33b8f05f2ff31591592c004aa848) for observable state, commands and presentation validation;
- official Avalonia navigation, binding, validation and headless-testing guidance;
- [`Dock.Avalonia`](https://github.com/wieslawsoltes/Dock) only if persisted docking/floating/layout restoration is a real requirement.

These libraries do not own navigation IDs, feature/permission availability, authority state, module contributions, or business ViewModel semantics.

## 8. Worker and background-processing route

Do not reopen the Worker runtime as an undifferentiated framework comparison. The focused owner `docs/server/WORKER_RUNTIME_AND_SCHEDULING.md` already selects:

- Proto.Actor for bounded in-process Worker execution, serialization and supervision;
- Quartz.NET 4.x for durable scheduling/trigger/calendar/misfire mechanics;
- PostgreSQL/SquiFlow state as the durable authority for jobs, occurrences, attempts, results and outbox state.

Curated source-only snapshots of Proto.Actor and Quartz.NET are retained under `reference-sources/snapshots/selected-runtime/`. Their snapshot revisions support inspection; they are not package-version selections and do not claim a Worker exists. Exact released package versions and transitive dependencies are qualified when the first real Worker workload activates the runtime.

Actor mailboxes are not durable queues, Quartz trigger state is not business-job authority, and supervisor restart is not semantic retry. Do not add Hangfire, TickerQ, MassTransit, RabbitMQ, Kafka, Proto.Cluster/Remote/Persistence, a generic Worker Channel queue, or a workflow engine beside this model without a new workload and superseding focused decision.

## 9. Workflow route

### Ordinary operation

Use capability application/domain code. No workflow package is needed.

### Small deterministic lifecycle

Use Stateless as the first package candidate. [`StateMachine<TState,TTrigger>`](https://github.com/dotnet-state-machine/stateless/blob/588f1a1a08683b452eb7c05562d9f055693cba5d/src/Stateless/StateMachine.cs) supports external state storage, queued firing, guards, async transitions, and graph inspection.

SquiFlow retains state/transition meaning, permissions/current facts, expected-version concurrency, persistence/transactions, audit/conflict results, and compatibility of persisted identifiers. Stateless is not a durable workflow runtime.

### Durable long-running process

Elsa becomes a package candidate only for durable waits, callbacks/timers, versioned definitions, and restart recovery. Current source includes:

- transaction-wrapped persistence in [`DefaultCommitStateHandler`](https://github.com/elsa-workflows/elsa-core/blob/aa021de39ee1323c212190a5f561b45d858206ec/src/modules/Elsa.Workflows.Runtime/Services/DefaultCommitStateHandler.cs);
- [`WorkflowDispatchOutbox`](https://github.com/elsa-workflows/elsa-core/blob/aa021de39ee1323c212190a5f561b45d858206ec/src/modules/Elsa.Workflows.Runtime/Services/WorkflowDispatchOutbox.cs);
- tenant-aware [`InterruptedRecoveryScanner`](https://github.com/elsa-workflows/elsa-core/blob/aa021de39ee1323c212190a5f561b45d858206ec/src/modules/Elsa.Workflows.Runtime/Services/InterruptedRecoveryScanner.cs);
- tests for transaction failure, notification ordering, tenant restoration, bounded recovery, and per-instance recovery failure.

An activation POC must prove the chosen provider's atomic boundary for instances, bookmarks, variables, logs, and dispatch records wherever SquiFlow relies on that claim. It must also prove tenant isolation, duplicate wake/idempotency, definition pinning, cancellation/compensation, upgrade compatibility, bounded recovery, and observability. Elsa does not own SquiFlow business state, permissions, rules, fact authority, or irreversible-effect semantics.

Temporal is the stronger candidate when developer-authored code must coordinate long-lived, mission-critical execution through durable timers, signals, child workflows and repeated failures. Its event-history replay and task queues would replace overlapping Quartz scheduling, SquiFlow DurableJob orchestration and Proto.Actor dispatch for each process class admitted to Temporal; it is not added beside them as another path for the same work.

Temporal does not directly provide SquiFlow's tenant-facing workflow/stage authoring model. Dynamic tenant definitions would still require a constrained SquiFlow interpreter or generated/deployed workflow code. Its POC must additionally prove deterministic replay/versioning, the SquiFlow-transaction-to-Temporal-start handoff, activity idempotency/ambiguous-effect recovery, payload protection, history limits/retention and the separate Temporal Service's production operations on the intended topology.

## 10. Remaining catalog routing

| Responsibility | Sources to inspect before custom implementation | Expected admission mode |
|---|---|---|
| permissions/features/settings/audit | ABP now reviewed; inspect Smartstore, Virto Commerce or nopCommerce only for a focused gap | contract/test/algorithm donor; no framework-owned application model |
| platform-admin versus tenant-admin | Oqtane and FullStackHero now reviewed | workflow and authorization test donor |
| business metadata/forms | Serenity now reviewed; Frappe/ERPNext and XAF remain behavior references | schema/UX donor; metadata does not become the domain model |
| commerce capability boundaries | Odoo, Vendure, Virto Commerce, nopCommerce | lifecycle/invariant donor; respect proprietary/reciprocal licensing |
| minimal module runtime | ExtCore and SimplCommerce now reviewed | simplicity and lifecycle test donor |
| enterprise desktop behavior | Eclipse RCP, NetBeans Platform, Tryton, Odoo POS | interaction/lifecycle/recovery reference only |
| ingress/load balancing | cloudflared, HAProxy, NGINX, Caddy, Traefik, YARP, Envoy | deployed product/package only after real topology earns it |

Odoo Enterprise and other proprietary sources are behavior/reference evidence only unless legal review grants another permitted use. Do not transplant their source.

## 11. Activation order

1. Start from a useful real capability/runtime journey rather than recreating the purged enum-only slice.
2. For a tenant-aware server slice, qualify Finbuckle and adapt tenant-isolation tests before custom resolver/filter infrastructure.
3. For a Workstation slice, qualify CommunityToolkit.Mvvm and official Avalonia patterns before deciding whether Prism or Dock is needed.
4. For a Worker slice, retain the accepted Proto.Actor + Quartz.NET + PostgreSQL responsibility split and qualify the exact released packages against the first workload rather than adding another Worker framework.
5. For an entity lifecycle, qualify Stateless before writing a state-machine engine.
6. For tenant feature/provisioning, adapt Orchard/FullStackHero mechanisms under bounded schemas and SquiFlow authority.
7. For a durable long-running process, run the Elsa persistence/recovery POC before writing or adopting an engine.
8. Add notices/attribution in the same change that first copies or redistributes licensed source.

No row authorizes a dependency, project, host, database, or runtime by itself. It removes blank-page design from the active-slice process while preserving the production-honest gate.
