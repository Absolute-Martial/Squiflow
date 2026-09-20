# Application Base Framework Admission Research

**Reviewed:** 2026-09-17

**Status:** current source-study and architecture-decision evidence; the first backend slice is now implemented

**Current implementation truth:** `README.IMPLEMENTATION.md`

**Focused owner:** `docs/architecture/APPLICATION_KERNEL_AND_MODULES.md`

**Catalog input:** `# Application Baseline, Reference Projec.md`
**Local evidence:** `reference-sources/` contains source-only snapshots at the revisions listed below; it contains no upstream Git histories and is not product code

## 1. Decision

The best application base for SquiFlow is:

```text
.NET 10
  + Microsoft.Extensions.Hosting / DependencyInjection / Configuration / Options / Logging
  + ASP.NET Core for server hosts
  + Avalonia + CommunityToolkit.Mvvm for Workstation
  + one explicit composition root per earned executable
  + capability-owned application and domain behavior
  + small SquiFlow-owned composition contracts only when real consumers need them
```

No reviewed full application framework becomes the SquiFlow runtime foundation.

This is a controlled hybrid. The runtime spine uses standard Microsoft primitives. Focused packages may own bounded mechanisms after source admission. Large frameworks and applications supply tested designs, failure cases and selected implementation ideas without becoming product authority.

If a single existing full framework had to be chosen on breadth alone, ABP is the strongest candidate. It is not selected because its module base classes, conventional registration, application-service conventions, repositories, ambient unit of work, auto API surface and cross-cutting infrastructure create a framework-owned application model. Those assumptions conflict with current SquiFlow decisions about explicit transactions, minimal public surface, capability ownership, cross-host reuse and earned physical boundaries.

FullStackHero is the selected source-owned backend starting base. Its complete pinned backend source is retained locally, but SquiFlow does not run the template generator or import the complete predefined module/infrastructure graph. Each subsystem is admitted, rewritten and tested against SquiFlow's active responsibility. The part-by-part record is `FULLSTACKHERO_BACKEND_ADOPTION_LEDGER.md`.

The focused comparison that admits the current feature/profile compiler is `TENANT_APPLICATION_PROFILE_INDUSTRY_COMPARISON.md`. It records the inspected Orchard Core, ABP, Finbuckle, Autofac, FullStackHero, Oqtane and alternative-runtime boundaries rather than treating this broad base decision as implementation evidence.

At the time of this source study, runtime responsibilities were `NOT_INTRODUCED`. The current implementation truth is maintained in `README.IMPLEMENTATION.md`; this research does not independently authorize an empty kernel project, solution, host, package graph or test tree.

## 2. License rule for research and adoption

License is not a filter for internal research. Proprietary, reciprocal and commercial sources may be inspected when they can answer a concrete architecture or product question.

Research admission and product admission are separate decisions:

| Activity | License treatment |
|---|---|
| Internal source reading and behavioral comparison | allow research; record source and revision |
| Reusing ideas, failure cases or independently written tests | record provenance and avoid copying protected expression |
| Copying or modifying source into SquiFlow | verify that the license permits the intended modification and distribution; preserve required notices |
| Adding a package/runtime dependency | verify the exact released package license and transitive obligations |
| Distributing SquiFlow with third-party code | satisfy the applicable license and notice obligations before release |

The architectural rejection of ABP, Orchard, Oqtane, FullStackHero, ExtCore, SimplCommerce or Serenity as the universal base does not depend on license. It follows from runtime ownership, host coverage, authority, lifecycle and replacement cost.

## 3. What the base must provide

The base is evaluated against current SquiFlow requirements rather than a generic framework feature count.

It must support:

1. a modular monolith with capability-owned business meaning;
2. ordinary in-process composition across Web, API, Worker and Admin hosts;
3. shared host-neutral capability behavior usable by server and Workstation where semantics are genuinely common;
4. server-owned current authority with provisional/local Workstation state;
5. one ordinary .NET service provider per executable rather than a service-provider tree per tenant;
6. explicit, reviewable registrations and minimal public/API surface;
7. explicit authoritative transactions and capability-owned persistence;
8. tenant context as validated scoped data, with feature/settings/permission snapshots introduced only when needed;
9. trusted shipped modules initially, without arbitrary runtime package loading;
10. selective use of focused packages without forcing the rest of a framework model;
11. clear upgrade and replacement paths;
12. evidence that follows the real responsibility rather than a prebuilt project diagram.

Breadth is useful only when the framework's ownership model matches these constraints. A framework that supplies more features can still be a worse base if removing or bypassing its assumptions becomes permanent work.

## 4. Comparative result

| Candidate | Useful strength | Main mismatch as the SquiFlow base | Admission |
|---|---|---|---|
| .NET Generic Host + ASP.NET Core | stable host lifetime, DI, configuration, options, logging, hosted services and HTTP foundation with low policy intrusion | does not supply SquiFlow module/tenant/business semantics | **selected runtime spine**; add only earned SquiFlow contracts |
| ABP Framework | broadest .NET modular application infrastructure; mature modules, features, settings, permissions, audit, tenancy and UoW | framework base classes and conventions spread into application code; broad core dependency surface; ambient UoW/repository/auto-controller defaults conflict with accepted boundaries | **source/test/contract donor**; no ABP application foundation |
| Orchard Core | mature feature graph, tenant feature profiles, recipes, admin composition and enable/disable behavior | ASP.NET/CMS shell model, tenant shell/service-provider lifecycle and content assumptions do not cover Workstation or SquiFlow authority | **feature/provisioning donor**; optional separate CMS may be evaluated later |
| FullStackHero .NET Starter Kit | modern modular-monolith examples, tenant tests, architecture tests, provisioning and operational examples | template imports a predetermined module/infrastructure shape; loader uses reflection, static state and broad contribution hooks | **selected source-owned backend base**; selective adaptation, no generator/runtime dependency |
| Oqtane | mature Blazor module/page administration, host/site admin distinction, module metadata and package lifecycle | page/CMS composition, runtime extension installation and database-per-tenant model conflict with the selected host and tenant model | **admin UX/metadata donor**; no Oqtane runtime |
| ExtCore | small extension discovery and ordered startup actions | reflection-created extensions, service-provider access during registration and web-focused lifecycle provide little benefit over explicit composition | **simplicity counterexample**; no dependency |
| SimplCommerce | compact module manifest and initializer pattern in a real modular application | initializer owns services and ASP.NET middleware without dependency/version validation; commerce application shape is too specific | **manifest/lifecycle donor**; no template/runtime adoption |
| Serenity | productive metadata-driven forms, grids, permissions and reporting patterns | row/property metadata and generated web UI would become a second application/presentation model and does not serve Avalonia directly | **future forms/grids donor**; no application runtime |
| Smartstore, Virto, nopCommerce, Odoo, Vendure, Frappe and XAF | deep business, admin, extensibility and customization examples | each is a large product/platform with its own domain, storage, extension and UI assumptions | inspect for the specific responsibility only |

## 5. Source findings

### 5.1 Standard .NET supplies the correct low-level base

The [.NET Generic Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host) already owns process lifetime, startup, graceful shutdown, DI, configuration and logging. [`IHostApplicationBuilder`](https://learn.microsoft.com/dotnet/core/extensions/generic-host#host-builder-settings) is the current straightforward composition model.

This is enough for the first real host. SquiFlow does not need a module framework merely to call capability-specific `IServiceCollection` extension methods from one composition root. A descriptor graph becomes useful only when several capabilities contribute stable metadata or ordered lifecycle behavior that explicit calls can no longer express safely.

### 5.2 ABP is the strongest full framework and the wrong ownership boundary

Inspected revision: [`955a7876537ebeaedbcb81e5407facb0840616bb`](https://github.com/abpframework/abp/tree/955a7876537ebeaedbcb81e5407facb0840616bb).

Useful evidence:

- [`ModuleLoader`](https://github.com/abpframework/abp/blob/955a7876537ebeaedbcb81e5407facb0840616bb/framework/src/Volo.Abp.Core/Volo/Abp/Modularity/ModuleLoader.cs) builds and sorts a dependency graph, reports missing dependencies and creates module instances;
- [`AbpModule`](https://github.com/abpframework/abp/blob/955a7876537ebeaedbcb81e5407facb0840616bb/framework/src/Volo.Abp.Core/Volo/Abp/Modularity/AbpModule.cs) exposes pre/configure/post service phases and pre/init/post/shutdown application phases;
- [`Volo.Abp.Core.csproj`](https://github.com/abpframework/abp/blob/955a7876537ebeaedbcb81e5407facb0840616bb/framework/src/Volo.Abp.Core/Volo.Abp.Core.csproj) shows that even the core package is broader than a dependency-sorter and carries configuration, localization, options, logging, dynamic LINQ, annotations and other dependencies;
- feature, setting and permission definitions clearly separate different product meanings;
- UoW, audit, tenancy and authorization implementations provide failure cases and contract ideas.

The module graph is worth adapting when SquiFlow earns one. The runtime is not. Depending on `Volo.Abp.Core` solely for module ordering would import framework lifecycle and convention vocabulary for a small algorithm SquiFlow can own and test. Adding the broader ABP stack would also reintroduce generic repositories, ambient UoW/interceptors and automatic application-service exposure that current decisions explicitly reject.

### 5.3 Orchard solves a different tenant model

Inspected revision: [`b304fcd78a70b792c6f63916c0ce6e6957bb1aa0`](https://github.com/OrchardCMS/OrchardCore/tree/b304fcd78a70b792c6f63916c0ce6e6957bb1aa0).

Orchard's feature information, dependency validation, feature profiles and recipe execution are mature sources for a future SquiFlow feature publication or tenant provisioning mechanism. Its tenant model is built around broad CMS shells, tenant configuration and tenant-specific application composition. SquiFlow instead uses one reviewed shipped capability set, authoritative tenant context and an immutable Tenant Application Profile. Most variation remains effective versioned data; a narrower cached profile runtime is POC-gated only for trusted implementation variants. SquiFlow does not adopt Orchard's shell lifecycle or CMS application authority.

Adopting Orchard as the application base would create a second tenant lifecycle and a server-only composition vocabulary that cannot be the shared base for Avalonia Workstation. The relevant algorithms and tests remain valuable donors.

### 5.4 FullStackHero is the source-owned backend base

Inspected revision: [`3f2959e683e9f83f13e55e1678c9119f63c7e8e5`](https://github.com/fullstackhero/dotnet-starter-kit/tree/3f2959e683e9f83f13e55e1678c9119f63c7e8e5).

The architecture tests, tenant-isolation tests and provisioning state are directly useful. Its [`ModuleLoader`](https://github.com/fullstackhero/dotnet-starter-kit/blob/3f2959e683e9f83f13e55e1678c9119f63c7e8e5/src/BuildingBlocks/Web/Modules/ModuleLoader.cs) is a useful counterexample: it keeps global static module state, scans attributes, constructs modules through reflection and allows each module to contribute services, middleware and endpoints.

SquiFlow starts from the complete pinned source for review continuity, while product projects contain only the parts an active responsibility earns. The template generator, FSH names and metadata, reflective loader, Mediator source generator, built-in JWT/Identity authority, per-tenant database model, Hangfire path and bundled infrastructure are excluded. Reuse its tests and bounded mechanisms when the corresponding SquiFlow responsibility appears; `FULLSTACKHERO_BACKEND_ADOPTION_LEDGER.md` records every current decision.

### 5.5 Oqtane is an admin and module UX reference

Inspected revision: [`b5e76441a4139966beb9327708ce39a59764787d`](https://github.com/oqtane/oqtane.framework/tree/b5e76441a4139966beb9327708ce39a59764787d).

[`ModuleDefinition`](https://github.com/oqtane/oqtane.framework/blob/b5e76441a4139966beb9327708ce39a59764787d/Oqtane.Shared/Models/ModuleDefinition.cs) combines presentation metadata, dependencies, permissions, routes, package identity, runtime/database compatibility and enablement. That breadth is valuable for studying an administrative catalog but too broad for a host-neutral SquiFlow capability descriptor.

[`Tenant`](https://github.com/oqtane/oqtane.framework/blob/b5e76441a4139966beb9327708ce39a59764787d/Oqtane.Shared/Models/Tenant.cs) directly carries tenant database connection and database type. SquiFlow has selected shared PostgreSQL authority and does not use database-per-tenant as the baseline.

Use the host-admin/site-admin flows and administrative presentation ideas. Keep runtime package installation, page modules and its tenant persistence model outside the base.

### 5.6 ExtCore and SimplCommerce keep the custom layer honest

Inspected revisions:

- ExtCore [`3d10fcb358e3828b42e138fbbc942ef14fd2fe2a`](https://github.com/ExtCore/ExtCore/tree/3d10fcb358e3828b42e138fbbc942ef14fd2fe2a);
- SimplCommerce [`3472ba02a6f2d9b6bdca7f7fb84957176aa799dc`](https://github.com/simplcommerce/SimplCommerce/tree/3472ba02a6f2d9b6bdca7f7fb84957176aa799dc).

ExtCore demonstrates discovery, ordered actions and ASP.NET assembly contribution with very little vocabulary. Its [`IConfigureServicesAction`](https://github.com/ExtCore/ExtCore/blob/3d10fcb358e3828b42e138fbbc942ef14fd2fe2a/src/ExtCore.Infrastructure/Actions/IConfigureServicesAction.cs) also passes an `IServiceProvider` into service registration, which encourages early resolution/service-location patterns SquiFlow should avoid.

SimplCommerce's [`IModuleInitializer`](https://github.com/simplcommerce/SimplCommerce/blob/3472ba02a6f2d9b6bdca7f7fb84957176aa799dc/src/SimplCommerce.Infrastructure/Modules/IModuleInitializer.cs) is compact: a module contributes services and ASP.NET configuration. It does not supply the stable identity, dependency closure, duplicate/version checks, host-adapter separation or failure semantics that SquiFlow would need once a real module graph exists.

Their main value is a size check. A future SquiFlow composition layer should stay close to this scale until real requirements prove the need for more.

### 5.7 Serenity belongs at the presentation metadata boundary

Inspected revision: [`2d854c6550436d957945898867194ab8260f946f`](https://github.com/serenity-is/Serenity/tree/2d854c6550436d957945898867194ab8260f946f).

[`PropertyItem`](https://github.com/serenity-is/Serenity/blob/2d854c6550436d957945898867194ab8260f946f/src/core/ComponentModel/PropertyGrid/PropertyItem.cs) and [`DefaultPropertyItemProvider`](https://github.com/serenity-is/Serenity/blob/2d854c6550436d957945898867194ab8260f946f/src/services/Entity/PropertyGrid/DefaultPropertyItemProvider.cs) show a mature metadata pipeline for labels, editors, visibility, validation, layout and presentation permissions.

That is a future forms/grids input, not a base application model. SquiFlow should keep compiled business invariants, authoritative authorization and domain state outside presentation metadata. Any future shared form schema also needs separate Web and Avalonia renderers, version/migration semantics and server enforcement.

## 6. Selected hybrid and ownership

```text
SquiFlow-owned
  capability meaning and use cases
  authoritative admission and transactions
  tenant, feature, permission and setting semantics
  explicit executable composition
  Workstation provisional/local meaning
  stable contracts and compatibility

Standard runtime
  .NET Generic Host
  Microsoft.Extensions DI/configuration/options/logging
  ASP.NET Core for server HTTP and Blazor hosts
  Avalonia for Workstation

Focused package candidates when earned
  Finbuckle.MultiTenant       tenant resolution/context and EF safeguards
  CommunityToolkit.Mvvm       Workstation presentation state and commands
  Stateless                  small deterministic lifecycle
  Proto.Actor + Quartz.NET   already selected Worker execution/scheduling split

Source and test donors
  ABP                        module graph; feature/setting/permission/audit cases
  Orchard Core               feature dependency and provisioning cases
  FullStackHero              architecture/tenant/provisioning tests
  Oqtane                     platform-admin and tenant-admin UX
  ExtCore/SimplCommerce      minimal lifecycle and size checks
  Serenity                   forms/grids presentation metadata
```

A donor does not become a dependency automatically. Every active slice still records the exact package/source admission, retained SquiFlow authority, gaps, exit path and SquiFlow-owned evidence.

## 7. Implementation activation route

### Current state

```text
application runtime: NOT_INTRODUCED
application kernel:  NOT_INTRODUCED
module graph:        NOT_INTRODUCED
tenant host:         NOT_INTRODUCED
BLOCKED:             none
```

### First useful vertical slice

The first implementation should contain only the projects and packages required by one useful end-to-end responsibility. Its executable composition root should use ordinary .NET registration directly. One capability does not need a module framework.

The slice must define its production intent, authority boundary, data ownership, failure/recovery/security behavior, falsifiable evidence and recurring regression guard. Build/test/CI files return with that executable slice because it needs a repository verification contract.

### When a small composition contract is earned

Introduce a SquiFlow-owned descriptor only after at least one real consumer needs stable composition metadata such as dependency ordering, feature definitions or adapter contributions. Start with the smallest contract that satisfies the real need.

When a dependency graph exists, adapt the ABP/Orchard failure cases and prove:

- deterministic ordering;
- missing dependency failure;
- cycle failure;
- duplicate stable ID failure;
- incompatible version failure where versions are actually required;
- explicit executable selection;
- no provider, UI, transport or process-name leakage into host-neutral capability code.

### When a tenant-aware server host is earned

Qualify the exact released Finbuckle package for resolver/context/EF mechanics. SquiFlow must still prove authenticated membership, authoritative tenant context, PostgreSQL RLS, pooled-connection reset, bypass-path isolation and audit behavior.

### When tenant features/settings/permissions are earned

Introduce separate typed definitions and versioned snapshots for the responsibilities actually needed. Use ABP contract ideas and Orchard dependency/provisioning cases. Do not import their stores, tenant containers or application authority wholesale.

## 8. Revisit triggers

Reconsider a larger framework only when a representative SquiFlow vertical slice demonstrates all of the following:

1. the framework removes materially more implementation and support burden than it adds;
2. the exact required package graph is bounded and upgradeable;
3. server, Worker, Admin and Workstation boundaries remain coherent;
4. SquiFlow tenant and security authority remain explicit;
5. explicit transactions, offline/provisional behavior and persistence ownership remain intact;
6. the replacement/exit path is credible;
7. an executable comparison proves the claim with representative behavior and failure tests.

Feature count, template generation speed and internal research license access are not sufficient revisit evidence.

## 9. Permanent research evidence

The tracked manifest and materializer are:

- `reference-sources/SOURCES.md`;
- `reference-sources/materialize.sh`.

The ignored `reference-sources/snapshots/` directory retains allow-listed source and tests for ABP, Oqtane, ExtCore, SimplCommerce, Serenity and the previously admitted candidates/donors. Each snapshot has an immutable revision marker and contains no `.git` directory.

The snapshots remain research evidence only. They are excluded from SquiFlow implementation inventory and must never be referenced by product projects.
