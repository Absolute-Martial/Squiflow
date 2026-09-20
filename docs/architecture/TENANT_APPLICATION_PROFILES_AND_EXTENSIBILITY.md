# Tenant Application Profiles and Bounded Extensibility

**Status:** accepted strategic architecture direction. The host-neutral ApplicationProfiles capability now validates bounded feature catalogs and compiles deterministic dependency-closed feature selections. CoreApi uses Autofac as its root provider and contains an internal bounded profile-runtime registry qualified through isolated tests. A production feature catalog, durable profile authority, production profile acquisition, flexible tenant record storage and tenant-specific implementation composition remain `NOT_INTRODUCED`.

**Authority boundary:** this document owns how SquiFlow represents tenant-selectable capabilities, personalization depth, trusted implementation variation and the failure-containment expectations created by that variation. Capability business meaning, tenant isolation, workflow/rules, persistence and resource limits remain owned by their focused documents.

**Framework evidence:** `docs/review/TENANT_APPLICATION_PROFILE_INDUSTRY_COMPARISON.md` records the source-level comparison and exact adopt/reject boundary behind the current compiler.

## 1. Business intent

SquiFlow cannot predict or hard-code every way a tenant may operate. The product must let a tenant shape a coherent application without requiring a forked SquiFlow codebase or a separately maintained bespoke deployment for every ordinary variation.

A tenant application profile can select and version:

- shipped capabilities/features;
- branding, labels, navigation and safe presentation metadata;
- settings, permissions and role templates;
- bounded rules, workflows, forms and custom fields;
- supported integrations and provider strategies;
- trusted implementation variants when configuration cannot express a materially different behavior safely;
- the applicable data, processing and runtime-isolation profile.

Branding is one shallow part of this model. It is not the definition of tenant personalization.

The platform goal remains bounded extensibility. SquiFlow does not promise to accept arbitrary tenant C#, JavaScript, SQL, assemblies, database DDL or unrestricted expressions. Variation must remain validatable, versioned, explainable, supportable, recoverable and resource-bounded.

## 2. Variation ladder

Use the lowest layer that completely represents the supported difference:

```text
1. presentation variation
   brand, theme, labels, navigation arrangement

2. capability selection
   enable a shipped feature/capability and its dependency closure

3. behavioral configuration
   typed settings, permissions, rules, stages, transitions and forms

4. extensible tenant information
   versioned custom-field/form schemas and validated record extensions

5. trusted implementation variation
   select a shipped/reviewed strategy, adapter or capability implementation

6. runtime/data placement isolation
   assign dedicated process, worker pool, database or deployment profile
   when fault, resource, contract or compliance requirements earn it
```

Do not use a tenant DI container to represent a label, setting, connection value, rule or form field. Do not force a tenant's materially different trusted implementation through an unreadable collection of flags merely to avoid composition variation.

## 3. Tenant application profile

The exact persistence shape is earned with the first real configurable capability. Conceptually, a published immutable profile revision contains references such as:

```text
TenantApplicationProfile
- ProfileId
- TenantId
- Revision
- CapabilitySetRevision
- FeatureActivationRevision
- SettingsRevision
- PermissionRevision
- Rule/Workflow/Form schema revisions
- trusted ImplementationVariant selections
- IntegrationProfile selections
- DataPlacementProfile
- ProcessingIsolationProfile
- RuntimeIsolationProfile
- publication/audit metadata
```

The profile is control data, not a mutable bag read piecemeal during one operation. A request, command, sync batch, workflow continuation or claimed job is pinned to one effective compatible revision for every decision that must remain internally consistent.

Tenants may share an identical profile definition or compiled runtime. Tenant identity and tenant-owned state never become shared merely because the composition fingerprint is the same.

## 4. Composition model

The host retains one application root for platform-wide services. Profile-aware composition may add a cached immutable runtime beneath it when trusted implementation variation is introduced:

```text
executable root
    ↓
TenantProfileResolver
    ↓
immutable effective profile revision
    ↓
ProfileRuntimeRegistry
    ↓
shared compiled profile runtime OR tenant-specific runtime where required
    ↓
ordinary request/job scope + immutable TenantContext
```

The runtime key and ownership are explicit. Possible keys include a profile fingerprint when many tenants use the same trusted implementation graph, or a tenant/profile revision when tenant-specific lifetime/state is genuinely required. Tenant-specific mutable business state must not live only in the DI runtime.

Profile runtimes are built lazily or ahead of activation according to measured cost. They are never rebuilt on every request. Activation validates the complete dependency closure before routing work to the revision. Retirement uses bounded draining and disposal; in-flight work remains pinned or fails with a defined retry/recovery outcome rather than using a half-reconfigured graph.

## 5. Autofac implementation boundary

Autofac is selected for the first trusted implementation-variation path because it provides explicit lifetime scopes, Microsoft DI/ASP.NET Core integration, application defaults, tenant/profile-specific registration overrides and explicit removal/disposal behavior. The implementation sequence and evidence are owned by `docs/implementation/AUTOFAC_TENANT_PROFILE_RUNTIME_IMPLEMENTATION_PLAN.md`.

Autofac is not the tenant profile, feature system, flexible-data model, authority boundary or isolation policy. The implementation must preserve these choices:

- ordinary variation continues through data/rules/settings or fixed strategy registries;
- a SquiFlow registry owns immutable runtime keys, cache bounds, leases, activation, draining and disposal around Autofac scopes;
- tenants with the same implementation fingerprint may share a runtime while retaining separate operation-scoped `TenantContext` and data;
- a tenant-specific runtime key is used only when the implementation lifetime itself must be tenant-exclusive.

CoreApi uses Autofac at its host composition boundary while capability projects remain container-neutral. Profile-specific resolution will occur only after authoritative account/membership-derived `TenantContext`; an untrusted route/header/domain/token value may identify a candidate but must not select secret-bearing tenant services. The executor must also work for non-HTTP Worker/synchronization execution.

An Autofac container or lifetime scope is never serialized to disk. It contains process-bound delegates, live object instances, disposables and potentially secret-bearing clients, so a serialized graph would be unsafe and runtime-version dependent. The future durable authority stores an immutable profile definition, schema version, implementation fingerprint/revision and activation evidence. Each process reconstructs the matching local runtime once through single-flight creation, reuses it while retained, and can rebuild it after restart without sticky routing or lost business state.

## 6. Flexible tenant information

Personalization cannot require a new strongly typed column/table for every tenant field. It also cannot turn core business truth into opaque unvalidated blobs.

Use a hybrid model:

```text
stable core identity, lifecycle, money, stock, security and issued truth
→ explicit owned relational schema and constraints

tenant-specific supplementary information
→ published versioned field/form schema
→ validated typed extension values
→ bounded JSONB/document representation where it fits
→ deliberate promoted indexes/projections for supported query/report needs
```

Every extensible schema defines stable field identifiers, data types, required/optional behavior, size/count/depth limits, validation, sensitivity, permissions, search/report support, defaults, version compatibility, retirement and historical rendering. Display-label changes do not reinterpret historical values. A field used in a protected invariant must be promoted into capability-owned semantics rather than remaining an arbitrary extension.

Do not use unrestricted EAV, arbitrary tenant SQL/DDL, or a single unbounded JSON document as the whole business model. Custom information is supported without weakening tenant isolation, referential meaning, correction history, query bounds or migration/recovery.

## 7. Failure and noisy-neighbor containment

Tenant composition adds a fault surface. Containment is layered:

| Layer | Required containment |
|---|---|
| correctness/security | authoritative `TenantContext`, authorization, scoped persistence, RLS/negative tests where applicable |
| profile activation | validate dependency closure, versions, schemas and provider configuration before routing work |
| operation failure | catch expected tenant/profile/provider failures at the operation boundary; return stable failure/incident identity; do not corrupt another operation |
| integration failure | per-tenant/provider timeout, cancellation, circuit/admission policy and retry budget |
| resource pressure | bounded per-tenant/workload concurrency, queue/backlog limits, fair acquisition and consumption accounting |
| runtime fault | health/drain/restart and optional separate process/worker partition when the required blast radius cannot be met in one process |

A child DI/lifetime scope does not isolate CPU, managed heap, GC, thread pool, static state, native libraries or process-fatal failures. An unhandled ordinary request exception can be contained; out-of-memory, stack overflow, unsafe native failure or process corruption can still affect every tenant in that process.

When the accepted business/SLO consequence requires stronger isolation, use an earned runtime profile. This may be a separate OS process or worker pool on the same host; it does not require Kubernetes or one pod per tenant. Strict blast-radius isolation cannot be truthfully promised from Autofac scopes alone.

## 8. Publication and recovery lifecycle

Material profile changes follow a controlled lifecycle:

```text
Draft
→ Validate dependency/schema closure
→ Simulate/test where applicable
→ Publish immutable revision
→ Warm/qualify runtime if required
→ Atomically activate routing revision
→ Drain superseded runtime/work
→ Retire when no supported work/history depends on it
```

Rollback activates a compatible earlier or corrective revision; it does not mutate history. Active workflows, offline Workstations, durable jobs and issued records keep the definition/version information needed for interpretation and recovery.

## 9. Evidence required before runtime composition ships

The first implementation-variant slice must prove:

1. one tenant cannot resolve another tenant/profile's implementation or state;
2. invalid profile revisions cannot become active;
3. concurrent first-use creates one usable runtime and disposes losing builds safely;
4. cold/warm resolution, memory growth and GC behavior at representative profile/tenant counts;
5. safe revision activation, draining, removal and rollback under concurrent requests/jobs;
6. non-HTTP tenant/profile resolution for Worker, sync and recovery paths;
7. bounded resource and provider failure behavior under a noisy or faulty tenant;
8. another node/process can reconstruct the active runtime from durable profile authority;
9. process-fatal fault claims match the selected runtime isolation profile;
10. flexible extension data remains validated, tenant-scoped, query-bounded and historically renderable.

Until that proof exists, tenant-specific implementation composition is `NOT_INTRODUCED`; the current CoreApi continues to use standard DI and the currently implemented authoritative `TenantContext`.
