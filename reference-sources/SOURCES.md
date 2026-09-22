# Curated Reference Source Manifest

**Established:** 2026-09-17

**Catalog input:** `# Application Baseline, Reference Projec.md`

**Admission owners:**

- `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md`
- `docs/review/APPLICATION_BASE_FRAMEWORK_ADMISSION_RESEARCH.md`
- `docs/review/CASBIN_NET_AUTHORIZATION_ADMISSION_REVIEW.md`
- `docs/review/WORKSTATION_FRAMEWORK_ADMISSION_RESEARCH.md`
- `docs/server/WORKER_RUNTIME_AND_SCHEDULING.md`

| Source | Revision | License | Local class | Retained scope | Current decision |
|---|---|---|---|---|---|
| Proto.Actor .NET | `6a5706283022e865f0e490e85ec7a7c5f4f891e0` | Apache-2.0 | selected-runtime | local actor runtime, TestKit and focused tests | selected for future Worker execution/supervision; not durable authority |
| Quartz.NET | `d523c898bc0222084bf48749a835063e75e70127` | Apache-2.0 | selected-runtime | core scheduler, hosting/DI integration, JSON serialization, unit tests and PostgreSQL schema | selected future durable scheduler; SquiFlow/PostgreSQL owns jobs and occurrences |
| CommunityToolkit.Mvvm | `b135626dd54d33b8f05f2ff31591592c004aa848` | MIT | direct-candidates | MVVM runtime, generators and focused tests | preferred Workstation presentation dependency when a real slice is activated |
| Finbuckle.MultiTenant | `ad67b15ecb6158f041abbb0c39ae4d718f3fda42` | Apache-2.0 | direct-candidates | resolver, ASP.NET Core and EF Core implementation/tests | focused candidate when tenant-aware host/EF boundaries exist |
| OpenFGA .NET SDK | `ec8ee04761b41e2400693b911a17463877e500c3` | Apache-2.0 | direct-candidates | high-level client, generated API models, retry/auth configuration and tests | focused provider adapter candidate for pinned-model authorization checks; never host-neutral authority |
| Casbin.NET | `30b142f0f5c4598852e8258d638bded3e24caf2c` | Apache-2.0 | poc-gated | enforcer/model/policy store, domain RBAC, resource roles, filtered loading, watcher contracts, benchmarks and tests | replacement candidate for OpenFGA only after the authorization admission POC; never a parallel authority |
| Casbin EF Core adapter | `1cc2c9ae985e48a93c38d1b884095502d15d52f8` | Apache-2.0 | poc-gated | adapter source, PostgreSQL-capable persistence, filtered policy, transaction design and tests | persistence candidate inside the same replacement POC; generic schema and replica freshness are not accepted by dependency alone |
| Stateless | `588f1a1a08683b452eb7c05562d9f055693cba5d` | Apache-2.0 | direct-candidates | state-machine source and tests | candidate for a small deterministic entity lifecycle |
| Dock.Avalonia | `cc08602d02fde1b85067cec064da29f34785e505` | MIT | poc-gated | Dock source and tests | admit only if a real workflow needs persistent docking/floating |
| IdentityModel OIDC Client | `6eaad5d969799f3a7eb388238fecaa655c66bd19` | Apache-2.0 | poc-gated | native OIDC client source/tests and system-browser sample | candidate after ZITADEL/system-browser/security POC |
| Autofac + Microsoft DI integration | `Autofac 9.3.4`; `Autofac.Extensions.DependencyInjection 11.0.2` | MIT | selected-runtime | CoreApi root provider and ordinary lifetime-scope integration | current CoreApi dependency; capability projects remain container-neutral |
| Autofac multitenant DI | `Autofac.Multitenant 2fdd4c0fc6a913324f5b985184d73f484db501e4`; `Autofac.AspNetCore.Multitenant 42851fdc88d2a988f266e70d108064210c0559d2` | MIT | poc-gated | tenant lifetime-scope cache, override/reconfiguration/disposal tests and ASP.NET Core request-scope integration | source/test donor for the SquiFlow runtime registry; packages not referenced and no resource/fault-isolation claim |
| FullStackHero .NET Starter Kit | `3f2959e683e9f83f13e55e1678c9119f63c7e8e5` | MIT | base-reference plus focused donor slice | complete source-only backend tree plus focused tenancy/module/provisioning tests | backend source base for selective SquiFlow-owned adaptation; no template generator or runtime dependency |
| Orchard Core | `b304fcd78a70b792c6f63916c0ce6e6957bb1aa0` | BSD-3-Clause | donors | feature and recipe contracts/services/tests | bounded feature/provisioning donor; no shell/CMS runtime |
| Prism | `358118cd640d9a22ff8cf21c8ad197fa038b7990` | Community or Commercial | donors | core/Avalonia regions, dialogs, demo and license | research donor; runtime dependency remains license/composition POC-gated |
| Uno.Extensions | `945312137dd56f42745a58cb5c65d9e81922d659` | Apache-2.0 | donors | hosting/navigation/auth/storage/localization slices | pattern/test donor; no Uno/WinUI runtime in Avalonia |
| CSLA .NET | `408c05eef72c0ffe651fac6565641c71b21d7457` | MIT | donors | business-object core and relevant documentation | state/lifecycle donor; no BusinessBase/DataPortal runtime |
| Elsa | `aa021de39ee1323c212190a5f561b45d858206ec` | MIT | donors | workflow runtime commit/outbox/recovery source and tests | long-running workflow evidence only until a real orchestration need exists |
| Temporal .NET SDK | `4a183307d90d6291fc213941a4f8d2506bd85800` | MIT | poc-gated | workflow/activity/worker/schedule source and focused tests | alternative only if replay-based durable orchestration earns a separate Temporal Service and supersedes overlapping Worker mechanics |
| ABP Framework | `955a7876537ebeaedbcb81e5407facb0840616bb` | LGPL-3.0 | donors | module graph/lifecycle plus feature, setting, authorization, audit, tenancy and UoW source/tests | broadest .NET framework reference; no ABP runtime foundation |
| Oqtane Framework | `b5e76441a4139966beb9327708ce39a59764787d` | MIT | donors | module metadata, tenant resolution and module-administration source | admin-composition donor; no page/CMS/package runtime |
| ExtCore | `3d10fcb358e3828b42e138fbbc942ef14fd2fe2a` | Apache-2.0 | donors | minimal extension discovery, ordered startup actions and ASP.NET integration | simplicity counterexample; no runtime adoption |
| SimplCommerce | `3472ba02a6f2d9b6bdca7f7fb84957176aa799dc` | Apache-2.0 | donors | compact module manifest/configuration/initializer source | simplicity and manifest donor; no commerce runtime |
| Serenity | `2d854c6550436d957945898867194ab8260f946f` | MIT | donors | property metadata/provider, renderer and focused tests | future business forms/grids donor; no application runtime or domain model |

## Intentionally not materialized

| Source family | Reason |
|---|---|
| Velopack | update POC is not active; retain the pinned official link/revision in the Workstation research and materialize only when the update gate starts |
| Eclipse RCP, NetBeans Platform | behavior/documentation donors in another runtime ecosystem; selected source slices are not needed now |
| XAF | commercial and not Avalonia; documentation/behavior donor only |
| Tryton, Odoo | large GPL/LGPL/proprietary-mixed application platforms; interaction reference only, no source transplant |
| Smartstore, Virto, nopCommerce and the rest of the catalog | broad product donors remain link-only until a focused responsibility needs their source locally |
| MassTransit, Hangfire, TickerQ, RabbitMQ, Kafka and other workflow/saga engines | current Worker owner explicitly excludes them from the baseline |
