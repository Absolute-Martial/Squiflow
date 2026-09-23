# Autofac Tenant Application Profile Runtime — Implementation Plan

**Status:** Increment A is implemented and qualified; Increment B's internal mechanics are implemented and isolated from production requests. Durable profile authority and tenant-specific implementation resolution remain `NOT_INTRODUCED`; `BLOCKED = none`.

**Decision:** Autofac is the CoreApi root provider and is selected for the first Tenant Application Profile implementation-runtime path. DryIoc is not part of the baseline. Standard Microsoft DI registrations remain source-compatible through `IServiceCollection`, while SquiFlow owns profile authority, runtime keys, cache bounds, activation, draining and resource governance.

**Focused architecture owner:** `docs/architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md`.

## 1. Production intent and first declared scope

The intended outcome is that a validated tenant operation can select a reviewed shipped implementation graph from an immutable Tenant Application Profile without rebuilding a container per request, leaking one tenant's state into another tenant, or treating a DI scope as fault/resource isolation.

The first declared scope is deliberately narrower than the eventual platform:

- CoreApi uses Autofac as its host container without changing existing ApplicationProfiles, Branding, IdentityAccess or Tenancy behavior.
- An authenticated account and current active membership establish `TenantContext` before profile-specific implementation resolution.
- A process-local registry creates at most one usable Autofac runtime per tenant and immutable implementation fingerprint/revision at a time.
- An operation receives an ordinary child lifetime scope containing its immutable `TenantContext` and effective profile revision.
- Equivalent implementation graphs may reuse shipped code or immutable definitions, but not a retained Autofac scope across tenants.
- Profile runtimes are reconstructable acceleration state. Durable authority never lives only in the container.
- The first production implementation override is activated only with a real capability whose supported implementations differ materially. Branding/settings/connection strings/custom fields/rules alone do not qualify.

The first slice does not claim tenant self-service profile administration, arbitrary plugins, hot assembly loading, process isolation, dynamic tenant code, workflow authoring, flexible record storage or noisy-neighbor elimination.

## 2. Why request-wide ambient tenant selection is rejected

The current CoreApi derives the application account from a validated external identity and checks current active tenant membership asynchronously through `ResolveTenantContext`. An incoming route/header/domain value is only a tenant candidate.

`Autofac.AspNetCore.Multitenant` can select request services through a synchronous tenant-identification strategy before ordinary endpoint execution. SquiFlow must not use an untrusted candidate to select secret-bearing or privileged tenant services before current account/membership authority is established.

The accepted request flow is therefore explicit:

```text
root ASP.NET request scope
→ JWT validation
→ active application account resolution
→ requested tenant candidate validation
→ current membership check
→ immutable TenantContext
→ active immutable Tenant Application Profile lookup
→ acquire ProfileRuntimeLease by tenant and immutable implementation key
→ begin Autofac operation scope
   + TenantContext
   + effective profile revision
→ resolve one typed capability entry point
→ execute and dispose operation scope
→ release runtime lease
```

The same executor accepts an already-authorized `TenantContext` in future Worker, synchronization and recovery paths. It does not read `HttpContext` inside capability code.

## 3. Autofac integration shape

### 3.1 Packages

The implemented host pins this compatible stable set through `Directory.Packages.props`:

- `Autofac` `9.3.4`;
- `Autofac.Extensions.DependencyInjection` `11.0.2`.

Both packages declare MIT licensing. The resolved CoreApi package graph was inspected with `dotnet list package --include-transitive`, and `dotnet list package --vulnerable --include-transitive` reported no known vulnerable package from the configured NuGet source on 2026-09-19. Package/advisory evidence must be refreshed when versions or sources change.

`Autofac.Multitenant` and `Autofac.AspNetCore.Multitenant` remain pinned source references rather than runtime dependencies. SquiFlow needs explicit profile acquisition after asynchronous account/membership authority, a bounded cache, immutable-key activation and lease-aware draining. The upstream `MultitenantContainer` owns its application container, keeps an unbounded tenant-scope dictionary and immediately disposes replaced scopes, so adding the package would not remove the SquiFlow registry and would add an unused ownership layer.

### 3.2 Host composition

CoreApi now uses `AutofacServiceProviderFactory` while preserving existing `builder.Services` registrations. Autofac-specific registrations remain in the CoreApi composition boundary. ApplicationProfiles, Branding, IdentityAccess, Tenancy and Orders projects do not reference Autofac.

Do not convert every service registration to Autofac syntax. Existing framework/provider extension methods continue through `IServiceCollection`; use `ContainerBuilder` only for profile runtime infrastructure and qualified implementation overrides.

### 3.3 Runtime registry

The initial internal host-owned types are conceptually:

```text
ProfileRuntimeKey
  = TenantId + ImplementationFingerprint + ImplementationRevision

ProfileRuntimeRegistry
  EnsureConfigured(profile)
  Acquire(profile, tenantContext) -> ProfileRuntimeLease
  Retire(key)

ProfileRuntimeLease
  OperationScope
  EffectiveProfile
  Dispose/DisposeAsync

TenantProfileExecutor
  Execute<TEntryPoint, TResult>(tenantContext, profile, operation)
```

Tenant identity is part of every retained-runtime key, including when the implementation graph is identical. A future published profile/configuration revision joins the key whenever it changes retained registrations or compiled tenant state. The immutable definition must belong to the same validated tenant as the operation. Current user/account, authorization decisions, request objects, tokens, database connections/transactions, mutable `DbContext` instances and unrestricted credentials stay out of the retained runtime. `TenantContext` remains operation-scoped; authoritative business state remains in its owning adapters.

The registry may wrap `MultitenantContainer` explicit-key operations and the upstream configure/remove behavior, but SquiFlow owns the outer state machine and lease accounting. Code outside the composition boundary does not receive `IContainer`, `ILifetimeScope`, `IComponentContext` or `IServiceProvider` for arbitrary resolution.

The implemented registry uses ordinary Autofac child scopes directly. That keeps application-root ownership with ASP.NET Core/Autofac integration and avoids a second root-owning wrapper. Its types are internal and the registry is not injected into any endpoint or capability path.

Runtime registration callbacks are trusted composition code and must only declare allow-listed registrations. They do not perform database/network calls, load tenant data or secrets, create external resources, or depend on ambient `HttpContext`. Resource activation belongs to the resulting scopes, where Autofac can track disposal. Builds run away from request execution, are concurrency-bounded and are canceled before starting during shutdown; an already-running synchronous registration callback cannot be aborted safely, so the host drain budget and process supervisor remain the final shutdown bound.

### 3.4 Orchard Core and ABP reference boundary

No Orchard Core or ABP runtime/package is added. Their pinned sources inform the future profile compiler rather than the container cache itself:

- Orchard Core `IFeatureInfo` and `ShellFeaturesManager` provide cases for explicit feature identity, dependency closure, validation before enablement and replacement of a published feature set. SquiFlow does not adopt Orchard shells, service-provider-per-tenant ownership, CMS discovery or force-enable behavior.
- ABP `ModuleLoader` and its tests provide cases for missing-dependency failure and deterministic dependency ordering. SquiFlow does not adopt ABP module instances as service locators, automatic plugin discovery, generic repositories/unit-of-work or framework-owned tenancy/authorization.
- The current registry uses neither donor's types and copies no donor implementation. Increment C may adapt their graph-validation test cases around SquiFlow-owned immutable profile definitions and allow-listed shipped variants.

## 4. Immutable revision and lifecycle model

Never reconfigure an in-use key in place. A material implementation change produces a new `ProfileRuntimeKey`.

```text
Absent
→ Building
→ Ready
→ Retiring
→ Disposed

Build failure
→ Failed with bounded retry/backoff owned by activation
```

Rules:

- concurrent first use deduplicates construction;
- a losing or failed build disposes every created disposable exactly once;
- only `Ready` runtimes accept new leases;
- publication switches new operations atomically to the new immutable key;
- the superseded runtime becomes `Retiring`;
- existing leases finish on their pinned runtime;
- disposal occurs only after lease count reaches zero or a declared forced-shutdown policy applies;
- shutdown stops new leases, drains within the host budget, then disposes remaining runtimes;
- another process can rebuild from the authoritative profile definition without sticky routing.

Do not call `ReconfigureTenant` for live publication if it can dispose objects used by in-flight operations. Prefer new-key activation plus old-key draining.

## 5. Cache and resource bounds

Autofac's tenant-scope dictionary is not the resource policy. The SquiFlow registry must enforce:

- a configured maximum number of retained `Ready` plus `Retiring` profile runtimes;
- single-flight construction per key;
- an idle-retirement duration for zero-lease runtimes;
- rejection/backpressure when no safe capacity exists;
- bounded build concurrency so a cold-start burst cannot compile many graphs simultaneously;
- metrics for runtime count, build duration/failure, lease count, retirement age and disposal failure;
- no forced `GC.Collect` and no correctness dependency on a warm cache.

Exact initial limits are deployment configuration validated against the measured CoreApi memory budget. They are not tenant-editable business settings.

The current safe defaults are 64 retained runtimes, four concurrent builds, 30-minute idle retention, one-minute maintenance and a 30-second shutdown drain. They are initial operational bounds, not representative-capacity claims. The registry emits a dedicated brand-neutral `Application.CoreApi.ProfileRuntime` meter for acquisitions, cache hits/misses, builds, failures, capacity rejection, retained runtimes, in-progress builds, active leases, build duration, idle age at retirement, retirements and disposal failures. Structured lifecycle logs contain implementation fingerprint/revision and exceptions; they do not contain tenant profile payloads, connection strings, tokens or keys.

### 5.1 Durable storage and restart behavior

Never serialize an Autofac container, lifetime scope or compiled registration graph to disk. Those objects contain process-bound delegates, live disposable instances, connections and potentially secret-bearing clients. Their representation is neither a stable data contract nor safe authority.

When Increment C introduces profile persistence, PostgreSQL stores the immutable profile definition, schema version, allow-listed selections, implementation fingerprint/revision and activation/audit evidence. Each process independently reconstructs the matching local runtime on first use or bounded warm-up. Concurrent first use is single-flight; warm acquisition creates only an operation scope. A restart or replica loss discards acceleration state and does not lose correctness, business state or profile authority.

## 6. Profile authority prerequisite

The container is not the Tenant Application Profile store. Before production profile resolution is enabled, introduce one authoritative profile query contract that returns a validated immutable snapshot containing at least:

```text
TenantId
ProfileId
ProfileRevision
DefinitionSchemaVersion
ImplementationFingerprint
ImplementationRevision
allow-listed ImplementationVariant selections
publication/activation evidence
```

The host-neutral contract belongs to a focused Application Profiles capability, not CoreApi and not Autofac. Its first persistence/publication scope must declare how profiles are created, validated, activated, rolled back and audited. Do not use an in-memory dictionary and present it as durable profile authority. Do not add profile tables before that exact lifecycle and its administration owner are accepted.

Until durable profile authority exists, runtime construction may be exercised only by isolated tests/benchmarks or a clearly labeled non-production POC.

## 7. First real implementation-variant slice

Autofac runtime infrastructure enters a claimed production path together with the first capability that has two supported, reviewed implementation types with the same SquiFlow-owned contract. The activating change must explain why typed settings, rules/workflows, data-driven forms or a small fixed strategy selection cannot safely own the difference.

Good triggers include a real provider/platform implementation with different dependency/lifetime graphs or a materially different reviewed business implementation. A second implementation invented only to demonstrate Autofac is not an acceptable trigger.

The selected capability owns the contract and business semantics. CoreApi owns profile runtime composition. Provider SDK types remain in adapters.

## 8. Implementation sequence

### Increment A — dependency qualification and root-provider migration — complete

1. Pin the compatible Autofac package set centrally.
2. Add host-only package references to CoreApi.
3. Switch CoreApi to the Autofac service-provider factory.
4. Preserve all current `IServiceCollection` registrations and endpoint behavior.
5. Prove scoped EF DbContexts/directories retain request lifetime and are disposed.
6. Prove startup failure, authentication, membership lookup, Problem Details and liveness remain unchanged.
7. Document the package graph, license, advisories and rollback to the built-in provider.

Declared result: Autofac root composition is `PRODUCTION_HONEST`; production tenant/profile resolution remains `NOT_INTRODUCED`.

### Increment B — isolated profile-runtime mechanics — complete for the internal evidence harness

1. Implement the internal runtime key, registry, lease and lifecycle state machine in the host composition boundary.
2. Adapt upstream Autofac concurrent configure/remove/disposal cases.
3. Use test-only variant types to prove container mechanics without exposing a fake product endpoint.
4. Add a benchmark/load harness outside the ordinary unit suite for cold build, warm acquisition, memory retention and retirement before activating a production variant.
5. Keep the registry unreachable from production requests until profile authority and a real variant exist.

Declared result: the internal mechanics prove single-flight construction, operation-context isolation, retained/build capacity, retry after failed build, idle retirement, in-flight draining, disposal and collectable metrics. A representative benchmark/load envelope is still an activation prerequisite rather than a current production claim. Production tenant-specific resolution remains `NOT_INTRODUCED`.

### Increment C1 — host-neutral feature-profile compiler — complete

1. Introduce the focused ApplicationProfiles capability without a dependency on CoreApi, Autofac, ASP.NET Core, EF Core or a provider SDK.
2. Validate stable feature identifiers, duplicates, missing dependencies, cycles, always-enabled features and dependency-only features.
3. Bound catalog, dependency and requested-selection inputs before materialization.
4. Produce deterministic dependency-first effective selections plus catalog/selection fingerprints.
5. Keep settings, permissions, release targeting, persistence and runtime activation outside this compiler.

Declared result: pure feature catalog/selection compilation is `PRODUCTION_HONEST`; a production catalog, durable profile authority and profile activation remain `NOT_INTRODUCED`.

### Increment C2 — authoritative profile snapshot and activation

1. Introduce the focused Application Profiles capability and its exact lifecycle.
2. Add the smallest durable provider/migration and administration path that can publish one immutable profile safely.
3. Resolve profiles only after authoritative `TenantContext` establishment.
4. Validate schema version, dependency closure, allow-listed variants and implementation fingerprint before activation.
5. Make rollback activate a prior compatible revision rather than mutate history.

Declared result when completed: profile publication/query is `PRODUCTION_HONEST`; implementation overrides may still be `NOT_INTRODUCED`.

### Increment D — first production implementation override

1. Add the first real capability contract and its reviewed variants.
2. Map allow-listed variant identifiers to registrations inside the CoreApi composition boundary.
3. Execute the capability through `TenantProfileExecutor` after membership/profile resolution.
4. Add cross-tenant, concurrent activation, drain, disposal and failure tests.
5. Add per-tenant/workload concurrency and provider budgets required by that capability.
6. Roll out behind an operator-controlled activation ceiling with immediate fallback to the default implementation revision.

Declared result: only the named capability's profile-aware implementation selection can become `PRODUCTION_HONEST`.

### Increment E — bounded retirement and operations

1. Enable idle retirement and configured capacity limits.
2. Add structured diagnostics without tenant secrets/profile payloads.
3. Prove graceful shutdown and build/activation failure recovery.
4. Measure representative active profile counts and establish requalification thresholds.
5. Write the operational runbook for disabling a bad profile revision and draining its runtime.

## 9. File ownership map

Expected touched paths, created only in their activating increment:

| Path | Responsibility |
|---|---|
| `Directory.Packages.props` | centrally pinned Autofac package versions |
| `services/core-api/Application.CoreApi/Application.CoreApi.csproj` | host-only Autofac package references |
| `services/core-api/Application.CoreApi/Program.cs` | service-provider factory and explicit composition wiring |
| `services/core-api/Application.CoreApi/Composition/` | internal registry, leases, explicit executor and Autofac modules |
| `modules/application-profiles/` | host-neutral profile identity/revision/publication contracts once earned |
| `services/db-migrator/` | ordered profile migrations only when durable profile persistence is introduced |
| `tests/integration/Application.CoreApi.Tests/` | real host, authority ordering, resolution, drain and cross-tenant evidence |
| a focused benchmark project only when Increment B starts | non-gating cold/warm/memory measurements |

Do not create Workstation, Worker, Sync or Admin profile-runtime projects in advance. Extract shared composition support only when a second real executable needs it.

## 10. Falsifiable evidence and permanent guards

| Claim | Evidence / regression guard |
|---|---|
| existing host behavior survives Autofac migration | full current CoreApi integration suite plus explicit scoped-lifetime/disposal test |
| authority precedes profile-specific resolution | real pipeline test showing unauthenticated, unbound, suspended and non-member requests create/acquire no profile runtime |
| tenant graphs do not cross | negative tests with two tenant contexts using the same implementation fingerprint/revision but separate retained scopes; mismatched definition/context acquisition fails |
| same-tenant reuse is bounded | same-tenant warm acquisition and single-flight build-count assertions; separate tenants consume separate retained capacity |
| no container build per request | concurrent/warm acquisition build-count invariant |
| first-use is race-safe | concurrent acquisition yields one ready runtime; losing resources are disposed |
| publication is revision-stable | in-flight operation remains on old key while new operations use the new key |
| retirement is safe | zero new leases after retirement; disposal only after final lease release |
| cache is bounded | retained-runtime and build-concurrency limit tests under churn |
| node-local state is disposable | fresh host reconstructs the active graph from profile authority |
| secrets do not leak | logs/errors/metrics contain IDs and stable failure codes, not profile definitions, tokens, connection strings or keys |
| DI is not claimed as resource isolation | load/failure evidence and documentation retain separate concurrency/process-isolation controls |

## 11. Requalification triggers

Re-run the focused compatibility, concurrency, lifecycle and load evidence when any of these change materially:

- Autofac or .NET major version;
- `Autofac.Multitenant`/ASP.NET integration version or provider-factory behavior;
- tenant/profile key semantics;
- profile definition schema or publication/rollback lifecycle;
- runtime capacity/idle-retirement policy;
- a new executable begins using profile runtimes;
- a variant introduces native code, unmanaged resources, long-lived connections, background threads or expensive singleton state;
- measured active-profile count, memory budget or churn exceeds the qualified envelope.

## 12. Rollback and exit path

Increment A remains reversible by restoring the default provider factory while registrations still originate from `IServiceCollection`.

Profile-aware operations retain a default reviewed implementation revision. A bad profile revision is disabled by atomically routing new work to the prior/default compatible key and draining the bad runtime.

If Autofac cannot meet lifecycle, compatibility or bounded-memory requirements, keep the SquiFlow-owned profile contracts and executor boundary and replace only the host composition adapter. DryIoc or another container is reconsidered only against the same evidence harness and a demonstrated Autofac gap.

## 13. Production envelope status

| Concern | Current state | Activation owner / next evidence |
|---|---|---|
| tenant identification and authorization | membership-derived `TenantContext` exists; no request acquires a profile runtime | first profile-aware capability must prove missing/unknown/non-member/suspended denial before acquisition |
| data isolation | account and membership queries are scoped; no tenant-owned business table/cache/blob/search surface exists | owning capability plus PostgreSQL isolation/RLS and cross-tenant provider tests |
| DI lifetime | Autofac root and ordinary scopes are active; internal profile runtimes are bounded and lease-drained | permanent CoreApi host/registry tests |
| profile configuration and secrets | bounded feature selection compilation exists; no production catalog, durable profile or secret is stored in a container definition | immutable profile publication plus secret-provider boundary |
| disk/durable storage | containers are never serialized; profile authority is `NOT_INTRODUCED` | immutable PostgreSQL profile schema/publication/rollback in Increment C |
| request pipeline | authentication and membership endpoints exist; profile execution is absent | authority-ordering test in first profile-aware endpoint |
| background work/messages | `NOT_INTRODUCED` | future Worker must receive and revalidate explicit tenant/profile context without `HttpContext` |
| monitoring | registry meter and structured lifecycle logs exist; exporter/dashboard/SLOs are `NOT_INTRODUCED` | deployment observability owner and representative load envelope |
| quotas/noisy-neighbor controls | retained/build bounds cover only runtime construction; tenant workload quotas are `NOT_INTRODUCED` | each real workload owns concurrency, queue, DB/provider and consumption limits |
| tenant/profile lifecycle | account/membership read state exists; provisioning/suspension/deletion/profile publication are `NOT_INTRODUCED` | authoritative administration capability and drain/revocation/audit evidence |
| multiple replicas | local runtime state is disposable and independently rebuildable by design; durable profile authority is absent | fresh-node reconstruction and revision-convergence tests in Increment C/D |
