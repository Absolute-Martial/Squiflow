# Tenant Application Profile Industry Comparison

**Reviewed:** 2026-09-20

**Decision:** SquiFlow owns a small, container-neutral profile compiler. It adopts bounded feature-graph semantics from Orchard Core and separation-of-concerns lessons from ABP, while keeping Finbuckle, Autofac, durable workflow engines and actor runtimes outside the feature-definition authority. No compared framework becomes the SquiFlow application model.

## 1. Question being answered

The product requirement is broader than tenant branding. A tenant must be able to select supported capabilities and personalize its application without receiving arbitrary code execution, weakening another tenant, or forcing SquiFlow to create and persist a dependency-injection container per tenant.

The first implementation responsibility is deliberately narrower than the complete Tenant Application Profile:

```text
shipped feature definitions
+ explicit dependency graph
+ always-required features
+ dependency-only features
+ bounded tenant selection
→ deterministic immutable effective selection
```

It does not yet publish or activate a tenant profile, store a profile revision, grant permission, choose a provider implementation, alter an Autofac graph, or isolate tenant CPU/memory. Those remain separate responsibilities.

## 2. Evidence base

The local source comparison uses immutable snapshots recorded in `reference-sources/SOURCES.md`:

| Source | Revision | License | Exact inspected surfaces |
|---|---|---|---|
| Orchard Core | `b304fcd78a70b792c6f63916c0ce6e6957bb1aa0` | BSD-3-Clause | `IFeatureInfo`, `ShellFeaturesManager`, recipe feature step and recipe executor tests |
| ABP Framework | `955a7876537ebeaedbcb81e5407facb0840616bb` | LGPL-3.0 | `FeatureDefinition`, `FeatureDefinitionManager`, `FeatureChecker`, `SettingDefinition`, `PermissionDefinition`, `ModuleLoader` and dependency-order test |
| Finbuckle.MultiTenant | `ad67b15ecb6158f041abbb0c39ae4d718f3fda42` | Apache-2.0 | tenant resolver, stores, per-tenant options and EF Core isolation surfaces/tests |
| Autofac multitenant | `2fdd4c0fc6a913324f5b985184d73f484db501e4` | MIT | `MultitenantContainer`, tenant identification, configure/remove/reconfigure and disposal tests |
| FullStackHero | `3f2959e683e9f83f13e55e1678c9119f63c7e8e5` | MIT | tenant provisioning state/steps, theme API and hostile cross-tenant tests |
| Oqtane | `b5e76441a4139966beb9327708ce39a59764787d` | MIT | module definitions, module administration and tenant manager |

Current primary documentation was also checked on 2026-09-20: [Orchard Core tenant feature profiles](https://docs.orchardcore.net/en/2.2/reference/modules/Tenants/), [Orchard recipes](https://docs.orchardcore.net/en/stable/reference/modules/Recipes/), [ABP features](https://abp.io/docs/latest/framework/infrastructure/features), [ABP settings](https://abp.io/docs/10.0/framework/infrastructure/settings), [ABP authorization](https://abp.io/docs/10.4/framework/fundamentals/authorization), [Finbuckle configuration](https://www.finbuckle.com/MultiTenant/Docs/v9.4.13/ConfigurationAndUsage), [Finbuckle per-tenant options](https://www.finbuckle.com/MultiTenant/Docs/v10.1.4/Options), [Autofac multitenancy](https://docs.autofac.org/en/stable/advanced/multitenant.html), and [DryIoc child-container models](https://github.com/dadhi/DryIoc/blob/master/docs/DryIoc.Docs/KindsOfChildContainer.md).

Orleans and Temporal were checked as category controls, not as DI/profile competitors: [Orleans grain activation lifecycle](https://dotnet.github.io/orleans/docs/grains/grain-lifecycle/) and [Temporal architecture vocabulary](https://github.com/temporalio/documentation/blob/main/docs/evaluate/understanding-temporal.mdx).

## 3. Comparison against the SquiFlow requirement

| System | What it solves well | Mismatch if used as the profile authority | SquiFlow decision |
|---|---|---|---|
| Orchard Core | feature identity, dependencies, always-enabled/dependency-only features, tenant feature profiles and recipes | feature-profile wildcards can broaden when new features ship; shell/service-provider and CMS tenant lifecycle would become a second application authority | adapt explicit graph semantics and failure cases; reject shells, wildcard activation and recipe-owned authority |
| ABP | separates features, settings and permissions; supports typed feature values and ordered module dependencies | arbitrary string-valued features blur feature availability with settings/limits; provider precedence, interception and framework module/application-service model would own too much | adopt semantic separation and missing/ordered dependency cases; keep typed settings and permissions as later separate SquiFlow responsibilities |
| Finbuckle | request/message tenant resolution, stores, per-tenant options and EF safeguards | resolves tenant context and options; it does not validate/publish immutable capability selections or supply audit/rollback authority | retain as a focused candidate for tenant mechanics only; do not use it as the profile compiler |
| Autofac.Multitenant | tenant-specific service overrides and lifetime scopes | container scope is process state, not durable profile state; upstream cache is tenant-keyed and does not provide SquiFlow bounds, authorization, audit or fault isolation | keep current SquiFlow bounded runtime registry; invoke only for a proven trusted implementation-graph variation |
| DryIoc | immutable registry snapshots and several child/facade/cache-sharing models | changing container technology does not define features, profile publication, authorization or noisy-neighbor policy | viable exit-path adapter if Autofac fails its evidence harness; no reason to replace the current qualified adapter now |
| FullStackHero | concrete tenant provisioning and cross-tenant API tests | theme and provisioning models are mutable template/application choices; upstream tenant header/root behavior conflicts with SquiFlow membership authority | reuse hostile isolation/provisioning cases; do not copy its tenant authority or mutable theme model |
| Oqtane | installable module metadata and module/site administration | runtime package/module/page model and server UI become product architecture; does not cover Workstation authority | admin-composition donor only |
| Orleans | on-demand actor activation, serialized message processing and idle deactivation | actor activation is execution topology, not feature/profile publication; it does not make process memory a durable profile contract | consider only for a future workload that earns actor semantics |
| Temporal | durable distributed workflow history and worker/task-queue execution | much larger operational boundary; does not replace feature/profile authority or DI composition | reconsider only for a proven long-running distributed workflow workload |

## 4. Adopted hybrid

The implemented compiler uses the following ideas:

- Orchard-style explicit feature IDs, dependencies, always-enabled features and features that can only enter through dependency/platform policy;
- ABP-style separation: a feature is availability, not a setting value and not a permission;
- ABP/Orchard dependency validation before any tenant selection can become effective;
- immutable normalized output and deterministic dependency-first ordering;
- a catalog fingerprint and selection fingerprint so later durable publication can bind a selection to the exact validated shipped vocabulary;
- explicit input/count/identifier bounds before materialization.

The compiler deliberately rejects several common conveniences:

- wildcard include/exclude rules, because a later deployment could silently activate a newly matching feature;
- arbitrary string-valued feature values, because typed settings and limits need their own definitions and validation;
- assembly scanning or automatic endpoint/controller discovery;
- container construction as the way to carry ordinary feature state;
- default-tenant fallback, header authority, or feature enablement as authorization;
- persistence or activation hidden inside compilation.

## 5. Implementation and state classification

`modules/application-profiles/SquiFlow.ApplicationProfiles` owns the current pure compiler:

```text
FeatureDefinition
→ FeatureCatalog.Create
→ validate duplicates, missing dependencies and cycles
→ FeatureCatalog.Compile(requested IDs)
→ CompiledFeatureSelection
```

The declared scope is `PRODUCTION_HONEST` for deterministic, bounded, host/provider/container-neutral feature compilation. The permanent regression guard is `tests/unit/SquiFlow.ApplicationProfiles.Tests` plus the repository verification command.

These responsibilities remain `NOT_INTRODUCED`:

- a production catalog containing real capability feature IDs;
- PostgreSQL profile drafts/revisions/activation/audit/rollback;
- profile administration API and OpenFGA permission;
- tenant-specific branding, settings, permissions, forms, fields, rules or workflows;
- release channel, rollout, experiment and Workstation snapshot evaluation;
- mapping a published implementation variant to the Autofac runtime registry;
- per-tenant workload quotas or process isolation.

## 6. Requalification triggers

Re-run this comparison and its evidence when:

- the first real capability publishes a stable feature ID;
- feature disablement must account for accepted durable work;
- the catalog or selection becomes a serialized/durable contract;
- typed settings, limits, release targeting or offline snapshots enter scope;
- a new framework/package is proposed to own feature/profile semantics;
- the Autofac adapter or its exit path changes;
- the feature/count/dependency bounds must change for measured product needs.
