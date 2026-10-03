# Application Baseline, Reference Projects, and Technology Qualification Catalog

> Status: living architecture/reference catalog.
>
> Governing principle: **Own the application baseline. Adopt proven mechanisms selectively. A framework, proxy, workflow engine, or infrastructure product must earn its place through a real requirement.**
>
> This document intentionally does **not** make another framework the product architecture.

---

## 1. Core Decision: Own Baseline + Selective Adaptation

The application does **not** adopt ABP, Orchard Core, Oqtane, FullStackHero, Prism, or another application framework as the permanent runtime that all product code must live inside.

Instead:

```text
Reference projects / libraries
        │
        ├── study mechanisms
        ├── use small dependencies where they fit cleanly
        ├── adapt source where its license permits the intended product use
        └── reimplement when our requirements differ
        │
        ▼
OUR APPLICATION BASELINE
        │
        ├── Foundation
        ├── Business/Platform Capabilities
        ├── Workstation Runtime
        ├── Server Runtime
        ├── WebApi / SyncApi / AdminApi
        ├── Worker
        └── Infrastructure Adapters
```

License is not a filter for internal research. Research admission does not itself permit source copying, dependency adoption, modification or distribution; evaluate those obligations when external work is proposed to enter the product.

### Four ways external work may enter the product

```text
1. OUR CODE
   Product-specific behavior and architecture.

2. NORMAL LIBRARY DEPENDENCY
   A focused library that solves its job cleanly.

3. ADAPTED OSS IMPLEMENTATION
   Selected implementation ideas/code brought under our architecture,
   after license review and removal of framework-specific assumptions.

4. REFERENCE ONLY
   Study architecture/behavior, then implement our own version.
```

Examples:

```text
Npgsql / OpenTelemetry
    -> normal dependencies

Finbuckle
    -> possible focused dependency or reference

FullStackHero / Orchard / Oqtane / ABP
    -> mechanism/reference donors

Odoo Enterprise source
    -> reference only; proprietary source must not be transplanted
```

---

# 2. Overall Application Baseline

```text
                           APPLICATION PLATFORM
                                  │
                     ┌────────────┴────────────┐
                     │                         │
                 Foundation                Capabilities
                     │                         │
                     └────────────┬────────────┘
                                  │
                       Shared Business Meaning
                                  │
          ┌───────────────────────┼────────────────────────┐
          │                       │                        │
          ▼                       ▼                        ▼
     Workstation              Server Hosts             Workers
       Avalonia         ┌─────────┼─────────┐
          │             ▼         ▼         ▼
          │          WebApi    SyncApi    AdminApi
          │             │         │         │
          └─────────────┴─────────┴─────────┘
                            │
                     Capability Application
                            │
                  ┌─────────┴──────────┐
                  ▼                    ▼
               SQLite              PostgreSQL
          local/provisional        authoritative
```

## Hard rules

1. **Business meaning belongs to capabilities.**
2. **Hosts own transport/runtime concerns, not duplicate business implementations.**
3. **Workstation local/provisional execution exists only where explicitly allowed.**
4. **Server owns authoritative admission/commit.**
5. **Admin/control-plane authority is separate from normal tenant business processing.**
6. **Conceptual modules do not automatically require separate `.csproj` projects.**
7. **Source generation, dynamic plugin loading, mediator machinery, framework base classes, etc. are not architectural goals.**

---

# 3. Shared Capability/Common Processing Point

The common point for WebApi, SyncApi, AdminApi, Worker, and Workstation is **not** a generic `SharedService`, controller layer, gateway, or common repository.

It is the relevant **Capability Application**.

```text
                           Orders Capability
                                  │
                    Authoritative Application
                                  │
          ┌───────────────────────┼──────────────────────┐
          │                       │                      │
      CreateOrder          AdmitOrderIntent       ReconcileOrder
          ▲                       ▲                      ▲
          │                       │                      │
       WebApi                   SyncApi                Worker
```

Different hosts may enter through different use cases while retaining one business meaning.

---

# 4. Workstation Application Baseline

The Workstation is a **purpose-built rich-client platform**, not merely an Avalonia UI project.

```text
Workstation
│
├── Host
│   ├── startup/shutdown
│   ├── DI/composition
│   ├── configuration
│   └── session
│
├── Shell
│   ├── navigation
│   ├── workspaces
│   ├── dialogs
│   ├── notifications
│   ├── commands/actions
│   └── capability contributions
│
├── Capability Runtime
│   ├── feature snapshot
│   ├── permission snapshot
│   ├── settings snapshot
│   ├── branding snapshot
│   └── capability availability
│
├── Shared Capability Code
│   ├── deterministic calculation
│   ├── deterministic validation
│   ├── local-safe state transitions
│   └── local/provisional use cases
│
├── Local Runtime
│   ├── SQLite
│   ├── local projections
│   ├── outbox
│   ├── cache
│   ├── local file staging
│   └── local workflow projection
│
├── Sync Client
│
├── Device Integration
│   ├── printers
│   ├── scanners
│   ├── filesystem
│   └── OS integration
│
├── Diagnostics
│
└── Guard / Update / Recovery boundary
```

## Workstation execution modes

```text
DeviceLocal
    -> printer, scanner, local files, SQLite maintenance, Guard

LocalProvisional
    -> offline-capable customer/order/quotation changes where allowed

ServerAuthoritative
    -> permissions, staff security, credit overrides,
       device revocation, central security/configuration
```

---

# 5. Workstation Reference Family

These are **reference sources**, not automatically dependencies.

## Prism / Prism.Avalonia ->

```text
{
  shell,
  modular UI contributions,
  navigation,
  region/workspace concepts,
  commands,
  dialog service,
  view lifecycle,
  explicit composition ideas
}
```

Do not inherit:

```text
{
  Prism naming everywhere,
  framework-wide EventAggregator business messaging,
  dynamic module downloading unless actually required
}
```

## Eclipse RCP ->

```text
{
  rich-client platform architecture,
  workbench/shell,
  extension points,
  command/action model,
  service lifecycle,
  background jobs,
  preferences,
  update concepts
}
```

Do not inherit OSGi complexity or a general-purpose plugin ecosystem.

## Apache NetBeans Platform ->

```text
{
  modular desktop runtime,
  window/workspace system,
  action system,
  service lookup concepts,
  application lifecycle,
  update-center concepts,
  application branding/configuration
}
```

## DevExpress XAF ->

```text
{
  business-workstation architecture,
  navigation metadata,
  security-aware UI,
  form/list metadata,
  validation presentation,
  reporting/scheduling ideas,
  configurable layouts,
  action contributions
}
```

Do not allow metadata/ORM/UI generation to become the domain model.

## Uno.Extensions ->

```text
{
  Microsoft.Extensions hosting inside a client,
  configuration,
  logging,
  authentication services,
  localization,
  navigation,
  secure/local storage concepts
}
```

Use ideas without switching away from Avalonia merely to gain them.

## CSLA .NET ->

```text
{
  rich-client editing state,
  dirty/new/busy/valid states,
  broken-rule/validation tracking,
  authorization-aware actions,
  client/server business-state lessons
}
```

Do not make domain entities derive from framework business-object base classes.

## Tryton Desktop ->

```text
{
  permission-derived navigation,
  large business-module desktop client,
  server/client responsibility split,
  enterprise desktop behavior
}
```

## Odoo POS / Odoo client behavior ->

```text
{
  offline-capable subset,
  online-only operations,
  session lifecycle,
  local business operation UX,
  later synchronization
}
```

Our SQLite/local-first model may go beyond Odoo's client caching model.

---

# 6. Backend / Cloud Application Baseline

```text
Backend Platform
│
├── Authoritative Capability Runtime
├── PostgreSQL
├── authorization
├── tenant context
├── idempotency
├── transactions
├── outbox
├── durable jobs
├── object storage
├── external integrations
├── observability
│
└── Hosts
    ├── WebApi
    ├── SyncApi
    ├── AdminApi
    └── Worker
```

The hosts share capabilities; they do **not** contain separate copies of business logic.

---

# 7. WebApi Baseline

Purpose: human-interactive online traffic.

```text
HTTP
 ↓
authentication
 ↓
tenant context
 ↓
transport validation
 ↓
request policy
 ↓
capability use case
 ↓
authoritative transaction/query
 ↓
response
```

Optimized for:

```text
{
  forms,
  queries,
  commands,
  search,
  dashboards,
  low-latency interactive requests
}
```

---

# 8. SyncApi Baseline

Purpose: Workstation synchronization/admission workload.

```text
Workstation
   ↓
batch / HTTP2 / gRPC-like suitable transport
   ↓
SyncApi
   ├── device authentication
   ├── tenant/device authorization
   ├── batching
   ├── compression
   ├── idempotency
   ├── revision comparison
   ├── conflict/admission
   ├── cursors/checkpoints
   ├── backpressure
   └── acknowledgements
           ↓
    authoritative capability
```

Workstation path:

```text
local/provisional execution
        ↓
OperationEnvelope
        ↓
SyncApi
        ↓
authoritative admission
```

Not:

```text
duplicate business implementation on Workstation
        +
duplicate business implementation on Server
```

---

# 9. AdminApi Baseline

Purpose: separate control plane.

```text
Admin Frontend
      ↓
AdminApi
      ├── platform tenant management
      ├── encryption policy
      ├── key lifecycle requests
      ├── feature/release management
      ├── recovery
      ├── platform configuration
      ├── support controls
      └── infrastructure/deployment operations
```

Hard boundary:

```text
Data Plane
    WebApi
    SyncApi
    Worker

Control Plane
    AdminApi
```

`AdminApi` is not simply a superuser route inside WebApi.

---

# 10. Worker Baseline

```text
Worker
│
├── outbox dispatch/consumption
├── notifications
├── scheduled work
├── document generation
├── reconciliation
├── integrations
├── maintenance
└── durable workflow continuation
        ↓
capability application
```

Worker owns runtime execution, not separate business meaning.

---

# 11. Backend / Business Platform Reference Family

## FullStackHero ->

```text
{
  tenant lifecycle,
  module-boundary architecture tests,
  tenant-aware cache/jobs,
  operator-vs-tenant separation,
  provisioning patterns
}
```

Do not inherit:

```text
{
  source-generated mediator as architecture,
  its exact module catalog,
  its naming,
  its exact vertical-slice/project structure
}
```

## Finbuckle.MultiTenant ->

```text
{
  tenant resolution,
  tenant context,
  per-tenant options,
  shared/separate/hybrid database patterns
}
```

Keep the product's actual Tenant model and lifecycle under our ownership.

## Orchard Core ->

```text
{
  feature dependency graph,
  tenant feature profiles,
  enable/disable semantics,
  provisioning/recipe concepts,
  mature modular composition
}
```

Adapt `Recipe`-like ideas into our own `ProvisioningPlan`.

Do not inherit CMS/content-shell architecture.

## ABP ->

```text
{
  permission vs feature vs setting separation,
  auditing patterns,
  unit-of-work ideas,
  data-filter concepts,
  background job abstractions,
  distributed locking/event patterns
}
```

Do not inherit:

```text
{
  AbpModule vocabulary,
  framework base classes everywhere,
  project explosion,
  framework-owned domain/application model
}
```

## Oqtane ->

```text
{
  host-admin vs tenant/site-admin separation,
  module contribution metadata,
  runtime administrative composition,
  scheduled-job management,
  branding/site administration ideas
}
```

Do not inherit CMS/page composition or runtime package installation.

## Smartstore ->

```text
{
  rule tree/builder,
  hierarchical permissions,
  import/export pipelines,
  scheduling,
  admin UX,
  modular migrations,
  caching/pubsub concepts
}
```

## Virto Commerce ->

```text
{
  capability-owned permissions/settings,
  provider strategies,
  touchpoint separation,
  extensible administration,
  worker/server separation
}
```

## nopCommerce ->

```text
{
  mature settings/configuration management,
  permissions,
  upgrade/migration lifecycle,
  multi-store lessons,
  admin separation
}
```

## Serenity ->

```text
{
  metadata-driven business forms/grids,
  permission-driven UI,
  reporting/data-screen productivity
}
```

Do not inherit a generator-heavy development model.

## Elsa Workflows ->

```text
{
  durable long-running workflow semantics,
  persistence,
  bookmarks/waits,
  resume,
  scheduling,
  workflow versions,
  correlation,
  fault/retry behavior
}
```

Use only where durable orchestration is genuinely needed.

## Frappe / ERPNext ->

```text
{
  custom fields,
  metadata-driven forms,
  reports,
  workflow customization,
  permissions,
  business customization without recompilation
}
```

Do not make "everything metadata."

## Odoo / Odoo Enterprise reference ->

```text
{
  business capability decomposition,
  module dependencies,
  multi-company behavior,
  permissions/record rules,
  sales/quotation/order/invoice transitions,
  inventory/purchasing flows,
  configurable views/fields,
  scheduled actions,
  activity/history concepts,
  module migration/upgrade behavior
}
```

Enterprise source is **reference only** unless licensing explicitly permits another use.

## Vendure ->

```text
{
  stable strategy interfaces,
  replaceable policies,
  custom fields,
  worker/server split,
  typed events,
  channel/touchpoint concepts
}
```

## ExtCore / SimplCommerce ->

```text
{
  minimal module lifecycle,
  host/module boundary,
  startup ordering,
  simplicity sanity-check
}
```

Use them to prevent our own capability runtime from becoming over-engineered.

---

# 12. Workflow Baseline

Do **not** make workflow the execution engine for everything.

```text
ordinary business operation
    -> normal capability code

entity lifecycle
    -> small deterministic state machine

long-running business process
    -> durable workflow/orchestration
```

## Reference mapping

```text
Stateless
    -> small entity lifecycle/state-machine ideas

Workflow Core
    -> lightweight durable workflow reference

Elsa
    -> full durable workflow mechanisms

MassTransit saga
    -> distributed message-driven process coordination
```

Workstation keeps a local workflow **projection** and can execute only explicitly allowed local/provisional transitions.

The server owns the authoritative multi-user workflow instance.

---

# 13. Tenant Branding / White-Label / Communications Baseline

Do not model everything as one `WhiteLabel = true` flag.

```text
Tenant Experience
│
├── Brand Identity
│   ├── display name
│   ├── logo
│   ├── colors
│   ├── favicon
│   └── document/email identity
│
├── Web Presence
│   ├── platform subdomain
│   ├── custom domain
│   ├── branded login
│   └── branded customer portal
│
├── Communications
│   ├── platform email
│   ├── tenant-owned email/domain
│   ├── tenant-owned provider
│   ├── WhatsApp/SMS/etc.
│   └── platform-managed endpoints/numbers
│
└── Deep White Label
    ├── reduced platform branding
    ├── custom Workstation identity
    └── optional custom installer/update identity
```

Use **entitlements**, not plan-name checks:

```text
Branding.CustomDomain
Branding.CustomLogin
Branding.RemovePlatformBrand
Communications.Email.CustomDomain
Communications.Email.BringOwnProvider
Communications.WhatsApp
Communications.ManagedNumber
```

Branding is data/configuration, not separate application forks.

---

# 14. Tenancy Baseline

Business tenancy stays inside the product:

```text
Tenant
├── organizations
├── branches
├── staff
├── roles/permissions
├── devices/workstations
├── features
├── rules
├── sync scope
├── storage/encryption policy
└── lifecycle
```

Infrastructure tenancy is a separate deployment concern.

Reference projects:

```text
Finbuckle
    -> tenant plumbing

FullStackHero
    -> tenant lifecycle

Orchard/Oqtane
    -> tenant feature/admin composition

Capsule
    -> Kubernetes namespace/resource tenancy

KubePlus
    -> per-tenant application-instance lifecycle

vCluster
    -> stronger tenant control-plane isolation

Crossplane
    -> provider-neutral infrastructure provisioning
```

None of Capsule/KubePlus/vCluster/Crossplane belongs in the initial product unless deployment requirements earn them.

---

# 15. Ingress / Reverse Proxy Baseline

## Current architectural decision

Do not require a dedicated API Gateway.

```text
External Edge
    ↓
optional Ingress / Reverse Proxy
    ↓
WebApi / SyncApi / AdminApi
```

### Small deployment baseline

If Cloudflare Tunnel can route directly:

```text
Cloudflare
    ↓
cloudflared
    ├── app hostname   -> WebApi
    ├── sync hostname  -> SyncApi
    └── admin hostname -> AdminApi
```

then **no second proxy is required**.

## Preferred future local reverse-proxy candidate

If local load balancing / health checking / failover becomes necessary:

```text
HAProxy
```

currently has the strongest fit for the likely backend topology because of:

```text
{
  very efficient native runtime,
  strong load balancing,
  active health checks,
  HTTP/2 and gRPC support,
  connection control,
  ACLs,
  stick tables,
  rate/connection policing,
  backend failover,
  runtime backend control,
  strong metrics/stats
}
```

### Important status

HAProxy is **not mandatory in the smallest deployment**.

It is the preferred candidate **when the local reverse-proxy/load-balancing requirement is earned**.

---

# 16. Reverse Proxy Alternatives

## HAProxy ->

```text
{
  strongest fit for traffic management,
  load balancing,
  active health checks,
  gRPC/HTTP2,
  connection limits,
  stick tables,
  low resource usage
}
```

Use when multiple backend instances/local failover become real requirements.

## NGINX OSS ->

```text
{
  reverse proxy,
  HTTP/gRPC,
  static file serving,
  caching,
  TCP/UDP stream proxy,
  mature ecosystem,
  very efficient runtime
}
```

Particularly attractive if local static serving/caching becomes important.

Caveat: richer active-health/runtime features differ between OSS and commercial NGINX offerings.

## Caddy ->

```text
{
  simple configuration,
  automatic HTTPS,
  reverse proxy,
  active/passive health checks,
  multiple upstreams,
  convenient standalone deployment
}
```

Best fit when operational/TLS simplicity is a primary need, especially without Cloudflare.

## Traefik ->

```text
{
  dynamic Docker/Kubernetes discovery,
  provider-based configuration,
  HTTP/TCP/UDP routing,
  middleware composition,
  health checks,
  container-native routing
}
```

Best fit when dynamic container/Kubernetes discovery earns it.

## YARP ->

```text
{
  programmable .NET reverse proxy,
  ASP.NET middleware integration,
  custom transforms,
  application-aware destination selection
}
```

Use only when programmable routing is genuinely required.

## Envoy ->

```text
{
  advanced gRPC/L7 routing,
  sophisticated health/load management,
  circuit breaking,
  service-mesh/control-plane capabilities,
  dynamic configuration
}
```

Very high bar; useful only for a substantially more distributed network topology.

---

# 17. Reverse Proxy Resource Qualification

Resource use should be measured on the actual target deployment.

Initial qualification targets:

```text
Idle RAM:
    target <= 50 MB where practical

Normal RAM:
    target <= 100 MB where practical

Idle CPU:
    effectively near zero

Requirements:
    no unbounded memory growth
    HTTP/2/gRPC verified
    no accidental full buffering of sync/upload payloads
    configurable connection/resource limits
    observable CPU/RAM/connections
```

Test actual workloads:

```text
WEB
    small interactive REST/JSON requests

SYNC
    long-lived HTTP2/gRPC connections
    compressed batches
    many devices
    backpressure

UPLOAD
    large streamed files/documents/images
```

Do not select a proxy from hello-world benchmarks alone.

---

# 18. Decoupled Frontends

Three first-class presentation applications:

```text
                       Presentation
             ┌────────────┼─────────────┐
             ▼            ▼             ▼
        Workstation    Tenant Web   Platform Admin
         Avalonia       Frontend      Frontend
             │            │             │
          SyncApi       WebApi        AdminApi
```

May share:

```text
{
  design tokens,
  icons,
  terminology,
  transport contracts where appropriate,
  feature identifiers,
  permission identifiers
}
```

Must not share presentation state/ViewModels.

---

# 19. Capability / Feature / Permission / Setting / Rule Distinction

Borrow the conceptual separation seen in mature platforms, but own the model:

```text
Capability
    Can this functionality exist on this host/tenant?

FeatureState
    Is the capability enabled?

Permission
    May this actor perform an action?

Setting
    How does enabled functionality behave?

Rule
    What business decision applies?

Policy
    What constraints govern execution?
```

Do not collapse everything into one configuration dictionary.

---

# 20. Capability Composition

Avoid runtime arbitrary plugin code initially.

Everything may ship compiled:

```text
Application
├── Parties
├── Commerce
├── Supply
├── Operations
└── Platform
```

Tenant configuration determines activation:

```text
Tenant A
    Commerce     ON
    Inventory    ON
    Credit       OFF
    Programs     OFF
```

A capability can expose metadata such as:

```text
CapabilityDefinition
{
    Id,
    Category,
    Dependencies,
    RequiredPermissions,
    SupportedHosts,
    OfflineSupport,
    ExecutionMode,
    ConfigurationSchema,
    LifecyclePolicy
}
```

This borrows useful ideas from Orchard/Oqtane/XAF/RCP-style systems without adopting a runtime plugin ecosystem.

---

# 21. Provisioning Plans

Borrow the *idea* of Orchard Recipes / platform provisioning without inheriting the framework.

```text
ProvisioningPlan
```

Examples:

```text
SmallRetail
    enable Commerce
    enable Inventory
    disable Programs
    create Owner/Staff defaults
    initialize settings
```

```text
PrintBusiness
    Customers
    Organizations
    Programs
    Quotations
    Orders
    Suppliers
    Documents
```

Plans are starting configurations, not permanent business-type restrictions.

---

# 22. Business Module Grouping

Prefer grouped business responsibility over dozens of tiny sibling projects.

```text
Modules/
├── Parties/
│   ├── Customers
│   ├── Organizations
│   ├── Contacts
│   └── Programs
│
├── Commerce/
│   ├── Quotations
│   ├── Orders
│   ├── Payments
│   └── Credit
│
├── Supply/
│   ├── Products
│   ├── Inventory
│   ├── Suppliers
│   └── Purchasing
│
├── Operations/
│   ├── Documents
│   ├── Workflow
│   ├── Rules
│   └── Approvals
│
└── Platform/
    ├── Tenancy
    ├── Identity
    ├── Access
    ├── Features
    ├── Settings
    ├── Devices
    ├── Audit
    ├── Branding
    └── Communications
```

Folders do not imply one `.csproj` per leaf.

Split assemblies only when a real dependency/runtime/ownership boundary needs compiler enforcement.

---

# 23. Suggested Physical Repository Baseline

```text
src/
│
├── Foundation/
│   └── Foundation.csproj
│
├── Modules/
│   ├── Parties/
│   ├── Commerce/
│   ├── Supply/
│   ├── Operations/
│   └── Platform/
│
├── Hosts/
│   ├── Workstation/
│   ├── WebApi/
│   ├── SyncApi/
│   ├── AdminApi/
│   └── Worker/
│
├── Frontends/
│   ├── Web/
│   └── Admin/
│
├── Infrastructure/
│   ├── PostgreSql/
│   ├── SQLite/
│   ├── Identity/
│   ├── Authorization/
│   ├── ObjectStorage/
│   ├── Messaging/
│   ├── Scheduling/
│   └── Observability/
│
└── Processes/
    ├── Guard/
    ├── Documents/
    └── Diagnostics/
```

Do **not** automatically create:

```text
Orders.Domain
Orders.Application
Orders.Contracts
Orders.Infrastructure
Orders.Api
Orders.Sync
Orders.Workstation

x every capability
```

---

# 24. Current Qualification / Selection Status

## Baseline / committed direction

```text
{
  C# / modern .NET,
  ASP.NET Core for server hosts,
  Avalonia for Workstation,
  PostgreSQL authoritative server data,
  SQLite local Workstation data,
  modular-monolith capability architecture,
  WebApi + SyncApi + AdminApi + Worker host separation,
  server-authoritative admission,
  local/provisional Workstation execution where permitted,
  separate Platform Admin control plane,
  OpenTelemetry-style observability boundary,
  provider-neutral object storage boundary,
  own application baseline
}
```

## Preferred candidate once requirement is earned

```text
HAProxy
    -> local reverse proxy/load balancer when needed
```

## Candidate / qualify before adoption

```text
Finbuckle
    -> tenant plumbing

Elsa / Stateless / Workflow Core / MassTransit sagas
    -> workflow/state-machine mechanisms depending on actual requirement

PowerSync or similar
    -> only if client-sync qualification proves it fits the .NET/offline/security model

Ticker/scheduler/message technologies
    -> only when corresponding workload requires them
```

## Reference only / mechanism donors

```text
{
  FullStackHero,
  Orchard Core,
  ABP,
  Oqtane,
  Smartstore,
  Virto Commerce,
  nopCommerce,
  Serenity,
  Frappe/ERPNext,
  Odoo/Odoo Enterprise,
  Vendure,
  ExtCore,
  SimplCommerce,
  Prism,
  Eclipse RCP,
  NetBeans Platform,
  DevExpress XAF,
  Uno.Extensions,
  CSLA,
  Tryton Desktop
}
```

## Explicitly not required now

```text
{
  generic API gateway,
  framework-owned runtime,
  arbitrary runtime plugin marketplace,
  Kubernetes tenancy platform,
  KubePlus/Capsule/vCluster/Crossplane integration,
  service mesh,
  Envoy-class distributed networking,
  source-generation-driven architecture,
  one project per conceptual layer per capability
}
```

---

# 25. Governing Selection Rule

For every external technology or framework:

```text
Requirement appears
      ↓
Can ordinary .NET / current architecture satisfy it simply?
      │
      ├── YES -> do not add technology
      │
      └── NO
           ↓
Evaluate focused libraries / proven implementations
           ↓
Benchmark or prototype if behavior/resource usage matters
           ↓
Adopt only the smallest mechanism that solves the requirement
```

And for reference projects:

```text
Reference
   ↓
mechanism
   ↓
our requirement
   ↓
our terminology/model
   ↓
adapt / reimplement / use focused dependency
   ↓
our tests
```

Never:

```text
Reference framework
   ↓
copy vocabulary
   ↓
force the product into that framework
```

---

# 26. Short Reference Map

```text
WORKSTATION
{
  Prism              -> shell/navigation/regions
  Eclipse RCP        -> rich-client platform architecture
  NetBeans Platform  -> modular desktop lifecycle/actions
  XAF                -> business UI metadata/security-aware UI
  Uno.Extensions     -> hosted client services
  CSLA               -> editing/business-state lessons
  Tryton Desktop     -> enterprise desktop behavior
  Odoo POS           -> offline/online capability split
}

BACKEND / BUSINESS PLATFORM
{
  FullStackHero      -> tenant lifecycle + module boundary tests
  Finbuckle          -> tenant context/resolution
  Orchard Core       -> feature dependencies/provisioning plans
  ABP                -> permissions/features/settings/audit concepts
  Oqtane             -> host-admin vs tenant-admin
  Smartstore         -> rules/permissions/imports/scheduler
  Virto              -> provider strategies/touchpoints
  nopCommerce        -> mature config/admin/migration patterns
  Serenity           -> metadata-driven business UI
  Elsa               -> durable workflow mechanics
  Frappe/ERPNext     -> business customization metadata
  Odoo               -> business capability decomposition
  Vendure            -> strategy/extension boundaries
  ExtCore            -> minimal modularity
}

INFRASTRUCTURE
{
  cloudflared        -> smallest initial edge route if sufficient
  HAProxy            -> preferred earned local load balancer
  NGINX              -> proxy + static/cache/TCP/UDP
  Caddy              -> simple secure standalone proxy/TLS
  Traefik            -> dynamic container/Kubernetes discovery
  YARP               -> programmable .NET routing
  Envoy              -> advanced distributed L7/networking
}

TENANT INFRASTRUCTURE - FUTURE ONLY
{
  Capsule            -> Kubernetes namespace/resource tenancy
  KubePlus           -> application-per-tenant provisioning
  vCluster           -> stronger virtual-cluster isolation
  Crossplane         -> provider-neutral infrastructure provisioning
}
```

---

## Final Architectural Position

The product is **not** a modified FullStackHero, ABP, Orchard, Oqtane, Prism, Odoo, or any other framework.

It is its own application platform.

Those projects are used to avoid rediscovering solved engineering problems.

The baseline remains ours; every external dependency, proxy, workflow runtime, scheduler, sync engine, or infrastructure component must be justified by a concrete requirement, operational benefit, or measured result.


---

# Companion: Detailed Capability Comparison

# Detailed Capability Comparison and Requirement Matrix

> Companion to `application_baseline_reference_catalog.md`
>
> Purpose: compare the actual functionality of the reference frameworks/projects and infrastructure candidates, then separately state what our product needs.
>
> Governing rule: **own the application baseline; adopt or adapt functionality, not another project's identity.**

## Legend

Capability strength:
- `●●●` defining/deep capability
- `●●` strong built-in capability
- `●` present but narrower/secondary
- `◐` adjacent/partial/reference value
- `—` not a meaningful capability of the project

Requirement status:
- `NOW` required by current baseline
- `NEAR` likely needed as real slices grow
- `EARN` add only after a concrete requirement proves it
- `REF` study/reference only
- `NO` deliberately avoid as a baseline

Adoption mode:
- `DEPEND` focused runtime dependency
- `ADAPT` adapt an implementation/mechanism into our architecture
- `REIMPLEMENT` study then implement our own version
- `REFERENCE` architecture/product research only

---

# 1. What the product actually needs

## 1.1 Workstation requirements

| Capability | Status | Product requirement |
|---|---|---|
| Application lifecycle | NOW | deterministic startup, initialization, recovery, shutdown |
| DI/composition | NOW | explicit capability registration; no magic required |
| Shell | NOW | main window, navigation, workspaces, dialogs, notifications |
| Navigation | NOW | capability-contributed and permission/feature filtered |
| Workspace/window state | NEAR | restore useful UI state; never authoritative business state |
| Commands/actions | NOW | consistent capability actions exposed to shell/UI |
| Capability registry | NOW | compiled capabilities and host availability |
| Feature/entitlement snapshot | NOW/NEAR | cached tenant availability |
| Permission snapshot | NOW | UX/offline hint only; server remains authority |
| Branding snapshot | NEAR | logo/name/theme/custom-login identity cached offline |
| Settings/preferences | NOW | device, user, tenant scopes kept distinct |
| Localization | NEAR | UI/document localization without hard-coded strings |
| Validation/edit state | NOW | dirty/valid/pending/conflict/saving state |
| SQLite | NOW | durable local projections/state/outbox |
| Offline subset | NOW | explicit, deterministic subset |
| Provisional execution | NOW | local result + durable pending OperationEnvelope |
| Sync client | NOW | retries, cursors, admission results, conflicts |
| Device integration | NEAR | printers/scanners/files/OS adapters |
| Local background work | NOW | bounded; durability in SQLite/outbox |
| Update/recovery integration | NEAR | safe update lifecycle supervised by Guard where appropriate |
| Diagnostics | NOW | structured logs, OTel, support bundle/health information |
| Metadata-driven repetitive UI | EARN | only where it reduces repetitive forms/grids safely |
| Arbitrary runtime plugins | NO | compiled capabilities first |
| Full workflow engine on client | NO | projection + explicitly permitted local transitions only |

## 1.2 Server / cloud requirements

| Capability | Status | Product requirement |
|---|---|---|
| Authoritative capability application | NOW | one business meaning used by every host |
| WebApi | NOW | interactive web/API workload |
| SyncApi | NOW | workstation sync/admission workload |
| AdminApi | NOW/NEAR | protected control plane, separate from tenant data plane |
| Worker | NOW/NEAR | outbox, documents, integrations, reconciliation, async work |
| PostgreSQL | NOW | authoritative state |
| Tenant context | NOW | resolved before business execution |
| Permissions | NOW | capability/business-action based |
| Feature/entitlement model | NOW/NEAR | distinct from permission and setting |
| Settings | NOW | typed and scoped |
| Audit | NOW/NEAR | durable where security/business needs it |
| Idempotency | NOW | especially sync/admission and retries |
| Optimistic concurrency/revisions | NOW | authoritative conflict detection |
| Transactional outbox | NOW | durable async boundary |
| Background processing | NOW/NEAR | bounded, durable, workload-specific |
| Scheduling | EARN/NEAR | only for real scheduled operations |
| Durable workflow | EARN | only long-running waits/handoffs/timeouts |
| Rules engine | EARN | only truly tenant-configurable business rules |
| Import/export | NEAR | resumable, validated, observable |
| Object storage | NEAR | provider-neutral adapter |
| Communications routing | NEAR | email/SMS/WhatsApp-like channels behind intent |
| Branding/custom domains | NEAR | tenant experience capability |
| Dynamic infra tenancy | EARN | only if deployment topology requires it |
| Microservices | EARN | extract after real scaling/ownership pressure |
| Generic API gateway | NO initially | ingress only until topology earns a gateway |
| Service mesh | NO initially | not justified by current topology |

---

# 2. Workstation reference frameworks — functionality matrix

| Functionality | Prism | Eclipse RCP | NetBeans Platform | XAF | Uno.Extensions | CSLA | Tryton Desktop | Odoo POS/client | Our need |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Application shell | ●● | ●●● | ●●● | ●●● | ● | ◐ | ●● | ●● | NOW |
| Window/workspace system | ●● | ●●● | ●●● | ●● | ● | — | ●● | ● | NEAR |
| Navigation | ●●● | ●●● | ●● | ●●● | ●●● | — | ●● | ●● | NOW |
| Modular UI contribution | ●●● | ●●● | ●●● | ●●● | ● | — | ●● | ●● | NOW |
| Module lifecycle/dependencies | ●● | ●●● | ●●● | ●● | ● | — | ●● | ●● | NOW, smaller |
| DI/service composition | ●● | ●● | ●● | ●● | ●●● | ●● | ◐ | ◐ | NOW |
| Command/action model | ●● | ●●● | ●●● | ●●● | ● | ◐ | ●● | ●● | NOW |
| UI event/messaging | ●●● | ●● | ●● | ●● | ● | ● | ◐ | ◐ | EARN/local only |
| MVVM/presentation patterns | ●●● | ● | ● | ●● | ●● | ●● | ◐ | ◐ | NOW |
| Metadata-driven forms/grids | ◐ | ◐ | ◐ | ●●● | ◐ | ◐ | ●● | ●●● | EARN |
| Security-aware UI | ◐ | ◐ | ◐ | ●●● | ● | ●● | ●● | ●●● | NOW |
| Validation/edit-state | ● | ◐ | ◐ | ●●● | ●● | ●●● | ●● | ●● | NOW |
| Dirty/new/busy semantics | ◐ | ◐ | ◐ | ●● | ● | ●●● | ●● | ●● | NOW |
| Authentication helpers | ◐ | ◐ | ◐ | ●●● | ●●● | ● | ●● | ●● | NOW, server-owned authority |
| Local settings/preferences | ● | ●●● | ●●● | ●● | ●● | ● | ●● | ●● | NOW |
| Secure local secrets | — | ● | ● | ● | ●● | — | ◐ | ◐ | NOW via OS facilities |
| Local DB abstraction | — | — | — | ●● | — | ◐ | — | ● | NOW, ours |
| Offline-first architecture | — | — | — | — | — | — | — | ●● | NOW |
| Durable sync/outbox | — | — | — | — | — | — | — | ● | NOW, ours |
| Background jobs | — | ●● | ● | ●● | ● | — | ◐ | ● | NOW, bounded |
| Update lifecycle | — | ●●● | ●●● | ◐ | — | — | ◐ | ●● | NEAR |
| Localization | ◐ | ●● | ●● | ●●● | ●●● | ◐ | ●● | ●●● | NEAR |
| Branding/theming | ● | ●● | ●● | ●●● | ●● | — | ● | ●●● | NEAR |
| Reporting/printing | — | ◐ | ◐ | ●●● | — | — | ●● | ●●● | NEAR |
| Scheduler/calendar UI | — | ◐ | ◐ | ●●● | — | — | ●● | ●● | EARN |
| Device/peripheral integration | — | ● | ● | ◐ | ● | — | ◐ | ●● POS | NEAR, ours |
| Diagnostics | ◐ | ●● | ●● | ●● | ●● | ◐ | ◐ | ●● | NOW |
| Dynamic runtime plugins | ● | ●●● | ●●● | ●● | — | — | ●● | ●●● | NO |
| Cross-platform desktop | ●● | ●● | ●● | ◐ | ●●● | ●● libs | ●● | browser/POS-dependent | NOW via Avalonia |

## 2.1 Prism / Prism.Avalonia

**Deep functionality:** MVVM composition, DI integration, commands, EventAggregator, region-based navigation, navigation parameters/journal, view lifetime, loosely coupled composite UI.

**Adapt/reference:**

```text
{
  shell region/workspace idea,
  navigation lifecycle,
  command composition,
  dialog/navigation abstractions
}
```

**Do not assume it solves:** SQLite, offline sync, operation envelopes, tenant lifecycle, server admission, durable outbox, Guard/update recovery.

**Adoption:** `REFERENCE` first; `DEPEND` only if a shell/navigation spike proves that Prism removes substantial code without controlling our business architecture.

## 2.2 Eclipse RCP

**Deep functionality:** workbench, windows/views/editors, command framework, services, plugin lifecycle, background Jobs, preferences, updates/provisioning, extension points.

**Adapt/reference:** workbench-vs-capability separation, action contributions, UI-thread/background-job separation, preference scopes, lifecycle hooks, update/recovery thinking.

**Avoid:** OSGi complexity, general-purpose plugin ecosystem, IDE-like extension machinery.

## 2.3 Apache NetBeans Platform

**Deep functionality:** main window/window system, persistent/lazy TopComponents, actions, module lifecycle, Lookup/service discovery, update-center concepts.

**Adapt/reference:** lazy workspace creation, persisted non-business window state, action contribution, simple service lookup concepts, update lifecycle.

## 2.4 DevExpress XAF

**Deep functionality:** Application Model metadata, generated list/detail views, navigation, Controllers/Actions, authentication, roles/permissions, validation, conditional appearance, audit trail, reporting, scheduler, attachments, localization, multi-tenancy.

**Very useful lesson:** UI visibility is not authorization. XAF explicitly distinguishes navigation visibility from actual data permissions.

**Adapt/reference:** presentation metadata, permission-aware UI, action metadata, navigation contributions, validation presentation, reporting/admin UX.

**Avoid:** framework ORM/model controlling domain, generated UI defining business architecture, XAF base types/actions becoming our core model.

## 2.5 Uno.Extensions

**Deep functionality:** Microsoft.Extensions-style hosting, DI, configuration, logging, HTTP, auth, navigation, localization, serialization, validation, storage, hosted/startup services.

**Adapt:** generic-host style composition in the desktop process. Use the pattern without switching from Avalonia.

## 2.6 CSLA .NET

**Deep functionality:** business/authorization rules, BrokenRulesCollection, validation state, property management, n-level undo, serialization/DataPortal, UI support packages.

**Best reference area:** rich-client editor state:

```text
{
  IsDirty,
  IsValid,
  IsSaving,
  IsPendingSync,
  HasConflict,
  BrokenRules,
  AllowedActions
}
```

**Avoid:** domain inheritance from CSLA base classes and DataPortal as our sync architecture.

## 2.7 Tryton Desktop / Odoo POS

Use primarily as **product-behavior references**.

Tryton: permission-derived navigation, module-oriented business desktop, server/client responsibility.

Odoo POS/client: explicit offline-capable subset, session lifecycle, local business operation UX, later synchronization, online-only actions.

---

# 3. Backend reference platforms — tenancy/security/configuration

| Functionality | ABP | Orchard | Oqtane | FullStackHero | Finbuckle | Smartstore | Virto | nopCommerce | Serenity | Frappe | Odoo | Vendure | Our need |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Tenant resolution | ●● | ●●● | ●● | ●● | ●●● | ◐ | ● | ◐ store-host | ◐ | ●● | ●●● | ●● channels | NOW |
| Tenant lifecycle/admin | ●●● | ●●● | ●●● | ●●● | ◐ | ◐ | ●● | ●● stores | ◐ | ●● | ●●● | ● | NEAR |
| Shared-DB tenancy | ●●● | ●●● | ●● | ●● | ●●● | ◐ | ●● | ●● | ◐ | ●●● | ●●● | ●● | NOW |
| DB-per-tenant patterns | ●●● | ●● | ●● | ●● | ●●● | ◐ | ●● | — | — | ● | ●● | ● | EARN |
| Per-tenant settings | ●●● | ●●● | ●●● | ●● | ●●● options | ●● | ●●● | ●●● per store | ● | ●●● | ●●● | ●● | NOW |
| Features | ●●● | ●●● | ●● | ●● | — | ●● | ●● | ● | ◐ | ●● | ●●● | ●● | NOW/NEAR |
| Entitlements/editions | ●● | ●● profiles | ● | ●● | — | ● | ●● | ◐ | — | ●● | ●● | ●● | NEAR |
| Roles/permissions | ●●● | ●●● | ●● | ●● | — | ●●● | ●●● | ●● | ●●● | ●●● | ●●● | ●● | NOW |
| Record/data permissions | ●● filters | ●● content | ● | ● | — | ●● | ●● | ACL/role limits | ●● | ●●● | ●●● record rules | ● | NEAR/domain-specific |
| Audit | ●●● | ●● | ●● | ●● | — | ●● | ●● | ●● logs | ● | ●● | ●●● | ● | NOW/NEAR |
| Settings/admin UI | ●●● | ●●● | ●●● | ●● | — | ●●● | ●●● | ●●● | ●● | ●●● | ●●● | ●● | NEAR |
| Localization | ●●● | ●●● | ●● | ●● | — | ●● | ●● | ●●● | ●●● | ●●● | ●●● | ●● | NEAR |
| Branding/site identity | ● | ●●● | ●●● | ● | — | ●● | ●● | ●●● | ●● | ●●● | ●●● | ●● | NEAR |
| Custom domain/host mapping | ● | ●●● | ●●● | ● | ● host strategy | ● | ●● | ●●● | ◐ | ●● | ●●● | ●● | NEAR |

# 4. Backend reference platforms — runtime/modules/jobs

| Functionality | ABP | Orchard | Oqtane | FSH | Finbuckle | Smartstore | Virto | nopCommerce | Serenity | Frappe | Odoo | Vendure | Our need |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Modular application | ●●● | ●●● | ●●● | ●●● | — | ●●● | ●●● | ●● plugins | ●● | ●●● | ●●● | ●●● | NOW |
| Feature dependencies | ●● | ●●● | ●● | ●● | — | ●● | ●● | ● | ◐ | ●● | ●●● | ●● | NOW, simpler |
| Explicit startup/composition | ●●● | ●●● | ●●● | ●●● | — | ●● | ●●● | ●● | ●● | ●● | ●●● | ●● | NOW |
| Runtime package/plugin install | ● | ●●● | ●●● | ◐ | — | ●●● | ●●● | ●●● | ● | ●●● | ●●● | ●● | NO initially |
| Module migrations | ●●● | ●●● | ●● | ●● | — | ●●● | ●●● | ●●● | ●● | ●● | ●●● | ●● | NOW/NEAR |
| Background jobs | ●●● | ●● | ●● | ●● | — | ●●● | ●● | ●● | ● | ●● | ●●● | ●● workers | NEAR |
| Background workers | ●●● | ●● | ●● | ●● | — | ●● | ●● | ● | ● | ●● | ●●● | ●●● | NOW/NEAR |
| Scheduler/admin UI | ●● | ●●● | ●●● | ● | — | ●●● | ●● | ●●● | ◐ | ●● | ●●● | ◐ | EARN |
| Event bus | ●●● | ●● | ●● | ●● | — | ●● | ●● | ●● | ● | ●● | ●● | ●●● | NOW internal; distributed EARN |
| Distributed event bus | ●●● | ◐ | ◐ | ● | — | ● | ●● | ● | — | ● | ● | ●● | EARN |
| Unit-of-work/transaction abstraction | ●●● | ●● | ●● | ●● | — | ●● | ●● | ●● | ●● | ●● | ●●● | ●● | NOW, simple |
| Distributed locking | ●●● | ◐ | ◐ | ● | — | ● | ●● | ◐ | — | ● | ●● | ◐ | EARN |
| Cache abstractions | ●●● | ●●● | ●● | ●● | ◐ | ●●● | ●●● | ●●● | ●● | ●● | ●●● | ●● | NEAR |
| Blob/object storage | ●●● | ●● | ●● | ●● | — | ●● | ●●● | ●● | ● | ●● | ●● | ●● | NEAR |
| Email | ●●● | ●●● | ●● | ●● | — | ●●● | ●● | ●●● | ● | ●●● | ●●● | ●● | NEAR |
| Other communication channels | ●● | ● | ● | ● | — | ● | ● | plugins | — | ●● | ●●● | plugins | NEAR via our router |
| Import/export | ● | ●● | ● | ● | — | ●●● | ●● | ●● | ●● | ●●● | ●●● | ●● | NEAR |
| Search | ● | ●●● | ● | ● | — | ●●● | ●●● | ●● | ●● | ●● | ●●● | ●● | EARN |
| Reporting | ● | ● | ● | ● | — | ●●● | ●● | ●● | ●●● | ●●● | ●●● | ● | NEAR |
| Rules/conditions | ● | ●● | ● | ● | — | ●●● | ●● | ●● | ● criteria | ●●● | ●●● | ●● strategy | EARN |
| Durable workflow | external/adjacent | ●● | ◐ | ◐ | — | ◐ | ◐ | — | — | ●● | ●● | ● | EARN |

# 5. Backend reference platforms — customization/admin

| Functionality | ABP | Orchard | Oqtane | FSH | Smartstore | Virto | nopCommerce | Serenity | Frappe | Odoo | Vendure | Our use |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Admin application | ●●● | ●●● | ●●● | ●● | ●●● | ●●● | ●●● | ●●● | ●●● | ●●● | ●● | REF/ours |
| Platform vs tenant admin | ●●● | ●●● | ●●● | ●● | ●● | ●● | ●● | ● | ●● | ●●● | ● | NOW/NEAR |
| Metadata-driven forms | ● | ●● | ●● | ● | ●● | ●● | ● | ●●● | ●●● | ●●● | ●● | EARN |
| Custom fields without compile | ● | ●●● | ●● | ◐ | ●● | ●● | ● | ●● | ●●● | ●●● | ●●● | EARN |
| User-customizable layouts | ● | ●● | ●●● | ◐ | ●●● | ●● | ● | ●● | ●●● | ●●● | ● | EARN |
| Visual rules/workflow editor | ◐ | ●● | ◐ | — | ●● | ◐ | discount UI | — | ●●● | ●●● | — | EARN |
| Strategy/provider replacement | ●● | ●● | ●● | ●● | ●● | ●●● | ●● | ●● | ●● hooks | ●● hooks | ●●● | NEAR only where needed |
| Stable extension contracts | ●●● | ●●● | ●●● | ●● | ●● | ●●● | ●● | ●● | ●● | ●● | ●●● | NOW, narrow |
| Marketplace/plugin ecosystem | ●● | ●● | ●●● | — | ●●● | ●●● | ●●● | ● | ●●● | ●●● | ●● | NO initially |


---

# 6. Backend project-by-project detail

## 6.1 ABP

ABP is the broadest .NET infrastructure reference in this set.

```text
Architecture
{
  modularity,
  multi-tenancy,
  DDD/layering conventions,
  microservice support
}

Infrastructure
{
  audit logging,
  background jobs,
  background workers,
  BLOB storage,
  data filtering,
  data seeding,
  distributed locking,
  email/SMS,
  entity cache,
  event bus,
  features,
  settings,
  unit of work,
  current user,
  timing,
  text templating
}

Security
{
  permission definitions,
  host-vs-tenant permission side,
  identity/account modules
}
```

**Best transferable lesson:**

```text
Capability != Feature != Permission != Setting != Rule
```

**Our use:** `REFERENCE / REIMPLEMENT` permission, feature, settings, audit, data-filter, UoW/event/distributed-lock patterns. Do not make ABP the application runtime.

## 6.2 Orchard Core

Orchard is particularly strong for per-tenant composition.

```text
{
  tenants,
  tenant admin,
  modules/features,
  feature dependency graph,
  Feature Profiles,
  site settings,
  custom settings,
  roles/permissions,
  recipes,
  deployment plans,
  background tasks,
  workflows,
  themes/assets,
  authentication integrations,
  media/storage integrations
}
```

Adaptation map:

```text
Orchard Feature         -> CapabilityDefinition / FeatureState
Feature Profile         -> entitlement/allowed-capability policy
Recipe                  -> ProvisioningPlan
Site Settings           -> typed tenant settings by capability
```

Do not inherit CMS/content architecture.

## 6.3 Oqtane

Study:

```text
{
  host/site administration split,
  modular Blazor UI,
  module metadata/lifecycle,
  site configuration,
  admin dashboard,
  scheduled jobs,
  themes,
  module/page contribution,
  package lifecycle
}
```

Most useful to us: **Platform Admin vs Tenant Admin**, admin contribution metadata, and scheduled-job administration UX.

## 6.4 FullStackHero

Use as source-level .NET implementation reference for:

```text
{
  modular-monolith boundaries,
  tenant provisioning,
  tenant-aware cache/jobs,
  architecture tests,
  operator-vs-tenant separation,
  infrastructure registration,
  vertical-slice organization
}
```

Take tests/mechanisms, not source-generator/mediator conventions or its project naming.

## 6.5 Finbuckle.MultiTenant

Finbuckle is deliberately narrow.

```text
Tenant resolution strategies
{
  host,
  base path,
  route,
  header,
  claim,
  session,
  static,
  delegate/custom
}

Tenant stores
{
  configuration,
  memory,
  EF Core,
  distributed cache,
  remote HTTP,
  custom
}

Per-tenant behavior
{
  Options<T>,
  authentication options,
  tenant context,
  EF Core/Identity isolation patterns
}
```

It does **not** provide our product's tenant lifecycle, entitlement model, commercial provisioning, capability registry, or platform-admin authority.

**Adoption possibility:** one of the few candidates that may deserve `DEPEND` status if it fits all relevant hosts cleanly without forcing our Tenant model.

## 6.6 Smartstore

Study:

```text
{
  rule builder/tree,
  hierarchical permissions,
  scheduler/admin,
  import/export profiles,
  mapping,
  modular migrations,
  caching/pub-sub,
  search,
  administration UX,
  extension/module bootstrap
}
```

Particularly useful implementation questions:

```text
How are rules represented/evaluated?
How does permission inheritance work?
How do large imports resume/fail/retry?
How do modules contribute migrations/admin functions?
```

License must be checked before copying code.

## 6.7 Virto Commerce

Study:

```text
{
  module-owned permissions/settings,
  provider/strategy abstractions,
  extensible admin,
  API/touchpoint separation,
  background processing,
  core/data/web boundaries,
  extension contracts
}
```

Key lesson:

```text
permission belongs to business capability
    Commerce.Orders.Create
not to transport/controller
```

## 6.8 nopCommerce

Study:

```text
{
  plugin lifecycle,
  permissions/ACL,
  multi-store host mapping,
  per-store settings overrides,
  scheduled tasks,
  email queues,
  themes,
  administration/configuration,
  migrations/upgrades,
  logs/system information
}
```

Especially useful for custom-domain/branding mechanics:

```text
HTTP host
   -> site/store context
   -> per-site settings/theme/company identity
```

Our tenant model remains broader than a nopCommerce Store.

## 6.9 Serenity

Serenity is strongest as a reference for business UI metadata.

Its property metadata can describe:

```text
{
  field name/title,
  editor/formatter,
  width/layout,
  visibility,
  permissions,
  presentation metadata
}
```

It also has forms/grids/filtering/search, permission keys, localization and reporting.

Good boundary:

```text
business capability
      ↓
presentation metadata
      ↓
Web / Workstation renderer
```

Bad boundary:

```text
metadata defines core business invariants
```

## 6.10 Frappe / ERPNext

Strong customization reference:

```text
{
  metadata-defined records,
  custom fields,
  forms,
  reports,
  permissions,
  workflows,
  roles,
  print formats,
  notifications,
  hooks/scripts,
  business modules
}
```

Our lesson:

```text
custom fields       -> metadata
layout              -> metadata
reports             -> definitions
restricted workflow -> versioned definition
notification policy -> data/configuration
security invariants -> compiled/enforced code
```

## 6.11 Odoo / Odoo Enterprise reference

Study business decomposition and product behavior:

```text
{
  CRM,
  sales/quotations/orders,
  invoicing,
  inventory,
  purchasing/suppliers,
  POS,
  manufacturing,
  projects,
  HR,
  company context,
  groups/access rights,
  record rules,
  settings,
  scheduled actions,
  views/fields,
  reports,
  activity/history,
  module upgrades
}
```

For Workstation/POS also study the split between offline-capable and server-dependent actions.

Enterprise source remains **REFERENCE ONLY** unless its license expressly allows a different use.

## 6.12 Vendure

Strongest transferable idea: explicit, stable extension strategies.

```text
{
  strategies,
  custom fields,
  plugin contracts,
  typed events,
  API server,
  worker process,
  background jobs,
  channels,
  storefront/admin touchpoints
}
```

Potential product abstractions only where alternatives are real:

```text
IDocumentStorage
IPricingPolicy
IOrderNumberPolicy
IInventoryAllocationPolicy
ICommunicationChannel
```

Do not make every service replaceable speculatively.

## 6.13 ExtCore / SimplCommerce

Their value is simplicity:

```text
{
  discover/register module,
  contribute services,
  contribute startup behavior,
  expose functionality,
  startup ordering
}
```

Use as a sanity check whenever our capability runtime starts growing too many lifecycle interfaces.

---

# 7. Workflow / state-machine comparison

| Capability | Stateless | Workflow Core | Elsa | MassTransit saga | Our need |
|---|---:|---:|---:|---:|---|
| In-process transitions | ●●● | ●● | ●● | ●● | NEAR |
| Hierarchical states | ●●● | ◐ | workflow composition | ●● | EARN |
| Guards | ●●● | ● | ●● | ●● | NEAR |
| Entry/exit actions | ●●● | ●● | ●● activities | ●● | NEAR |
| External state storage | ●●● | ●●● | ●●● | ●●● saga repository | NEAR |
| Durable persistence | — | ●●● | ●●● | ●●● | EARN |
| Wait for external event | — | ●●● | ●●● bookmarks | ●●● messages | EARN |
| Delay/timer/cron | — | ●● | ●●● | ●● | EARN |
| Definition/versioning | code version | ●● | ●●● | code deployment | EARN |
| Human approvals/waits | model only | ●● | ●●● | ●● | EARN |
| Visual designer | — | — | ●●● | — | NO initially |
| Distributed execution | — | ●● | ●●● | ●●● | EARN |
| Correlation | app-owned | ●● | ●●● | ●●● | EARN |
| Retry/fault handling | app-owned | ●● | ●●● | ●●● | EARN |
| Lightweight lifecycle fit | ●●● | ●● | ● | ● | important |
| Long-running orchestration fit | — | ●●● | ●●● | ●●● | only where earned |

## 7.1 Selection rule

```text
entity lifecycle/status
    -> ordinary code or Stateless-style state machine

multi-hour/day process with waits/timeouts/approvals/resume
    -> qualify Workflow Core or Elsa

message-driven cross-service coordination
    -> MassTransit saga becomes relevant
```

## 7.2 Stateless

Native functionality:

```text
{
  generic states/triggers,
  hierarchical states,
  entry/exit actions,
  sync/async guards,
  parameterized triggers,
  re-entry,
  externally stored state,
  permitted-trigger introspection,
  DOT/Mermaid graph output
}
```

Not a durable workflow runtime, scheduler or distributed coordinator.

## 7.3 Workflow Core

Native functionality:

```text
{
  long-running workflows,
  persistence between steps,
  defer/wait,
  external events,
  scheduling,
  pluggable persistence,
  multi-node concurrency providers,
  fluent/code/definition approaches
}
```

Potential middle ground between a state machine and a full workflow platform.

## 7.4 Elsa

Full workflow runtime:

```text
Definitions
{
  versions,
  draft/published,
  programmatic/visual definitions
}

Instances
{
  running,
  suspended,
  finished,
  faulted,
  canceled
}

Runtime
{
  bookmarks,
  triggers,
  stimuli,
  correlation,
  scheduling,
  persistence,
  inbox,
  execution logs,
  dispatcher,
  distributed execution
}

Infrastructure
{
  EF Core/PostgreSQL,
  MongoDB/Dapper,
  clustering,
  distributed locking,
  multitenancy,
  observability
}
```

Use only when durable orchestration genuinely exists.

## 7.5 MassTransit saga state machines

Strong when the product already needs message-driven distributed coordination:

```text
{
  message contracts,
  correlation,
  saga repositories,
  state machines,
  retries/redelivery,
  faults,
  outbox/inbox patterns,
  routing slips,
  brokers/transports,
  middleware
}
```

Do not add a broker/MassTransit merely because an Order has a `Status`.
 also that the some of the decision are already present like for thee background workwer and processing is decided and theyr have suitabble choosee look for them also
