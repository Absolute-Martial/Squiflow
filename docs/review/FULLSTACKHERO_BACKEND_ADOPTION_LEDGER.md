# FullStackHero Backend Adoption Ledger

**Reviewed:** 2026-09-19

**Upstream revision:** `3f2959e683e9f83f13e55e1678c9119f63c7e8e5`

**License:** MIT
**Local source:** `reference-sources/snapshots/base-reference/fullstackhero-backend/`

## Decision

FullStackHero is the source-owned starting base for SquiFlow backend implementation. This means its complete backend source remains locally available and each concern is reviewed before it enters product code. SquiFlow does not run the template generator, reference snapshot files, or an FSH runtime package.

Every adopted part is renamed, reduced to the active responsibility, and made subject to SquiFlow's authority, tenancy, persistence, security and evidence rules. No FSH product name, package metadata, namespace, default tenant, demo data or user-facing asset may enter a SquiFlow artifact.

## Current adoption decisions

| Area | FSH source inspected | SquiFlow decision | Current state |
|---|---|---|---|
| Repository build | `src/Directory.Build.props`, `src/Directory.Packages.props`, `global.json` | Adapt central build/package management with SquiFlow metadata and only packages required by an active slice. | `PRODUCTION_HONEST` for the current solution/build/test scope. |
| API composition root | `src/Host/FSH.Starter.Api/Program.cs`, `src/BuildingBlocks/Web/Extensions.cs` | Adapt the small-host shape. Registrations and endpoints remain explicit; do not import `AddHeroPlatform` or reflection discovery. | `PRODUCTION_HONEST` for public bootstrap/liveness, authenticated account and active-membership queries, tenant-workspace read and the narrow Order draft create/read slice. |
| White labeling | FSH tenant-theme migrations and host metadata | Use a SquiFlow-owned typed public brand contract. Permit bounded text, theme identifiers and safe URLs; reject arbitrary HTML, script, CSS and insecure absolute URLs. | `PRODUCTION_HONEST` for deployment-wide public application identity. Tenant-specific branding and asset upload remain `NOT_INTRODUCED`. |
| HTTP errors | `GlobalExceptionHandler.cs` and its tests | Adapt bounded RFC Problem Details mappings when the first fallible application operation exists. Do not expose exception/provider/authorization detail. | `PRODUCTION_HONEST` for generic authentication failure plus unbound/disabled account denial codes; broader application error vocabulary remains `NOT_INTRODUCED`. |
| OpenAPI | `Web/OpenApi/Extensions.cs` and `BearerSecuritySchemeTransformer.cs` | Adapt the ASP.NET Core document/transformer mechanism, not the upstream global bearer policy. Use configured public branding, describe the configured OpenID Connect discovery authority and apply the requirement only to endpoint metadata that requires authorization. | `PRODUCTION_HONEST` for the current CoreApi v1 surface. Real-host tests guard public identity, codename exclusion, discovery URL and protected-versus-public operation classification. |
| Module loading | `Web/Modules/ModuleLoader.cs` | Reject static mutable state, assembly scanning, `Activator.CreateInstance`, broad middleware hooks and automatic endpoint exposure. Compose current projects explicitly. | Rejected for the baseline. |
| Mediator/source generator | API project references and mediator registration | Do not adopt. Use direct use-case calls until an actual fan-out or pipeline requirement proves another mechanism. | Rejected for the baseline. |
| Identity | `Modules/Identity` JWT issuance, roles and permission handlers | Reject. ZITADEL authenticates; SquiFlow binds `(issuer, subject)`; OpenFGA plus resource/domain checks authorize. | FSH mechanism rejected. SquiFlow's configured ASP.NET bearer validation and active-account resolution are `PRODUCTION_HONEST`; real ZITADEL/login/session/provider evidence remains `NOT_INTRODUCED`. |
| Tenant selection | `Modules/Multitenancy`, tenant header/query strategies and root override | Reject as authority. Client tenant hints must be validated against current SquiFlow membership; pooled PostgreSQL is the baseline. | FSH mechanism rejected. SquiFlow active-membership query and immutable membership-derived `TenantContext` are `PRODUCTION_HONEST`; the workspace and Orders paths prove route candidates cannot bypass current membership. |
| PostgreSQL/EF | `BuildingBlocks/Persistence/*`, tenant-isolation tests | Reuse setup and hostile-test ideas. Keep concrete capability-owned data access, explicit transaction ownership, runtime/migration role separation and PostgreSQL RLS proof for tenant-owned tables. | `PRODUCTION_HONEST` for global IdentityAccess and Tenancy security/control schemas and for the narrow tenant-owned Orders draft/line/receipt schema with explicit predicates, forced RLS and transaction-local tenant context. Broader tenant-owned persistence remains absent. |
| Database migrator | `FSH.Starter.DbMigrator/*`, migrations project | Adapt only `apply`, `list-pending`, fail-fast configuration, bounded single-run coordination and clear exit codes. Exclude demo seed, tenant selection, catalog/per-tenant databases, identity placeholders, module bootstrap and job services. The API never migrates on startup. | `PRODUCTION_HONEST` for ordered IdentityAccess/Tenancy/Orders migration sets and real PostgreSQL evidence. |
| Background jobs | `BuildingBlocks/Jobs/*`, Hangfire packages and provisioning fallback | Reject. SquiFlow's selected direction is PostgreSQL durable job authority, Quartz scheduling and Proto.Actor bounded execution/supervision when a real workload exists. | FSH mechanism rejected; SquiFlow Worker `NOT_INTRODUCED`. |
| Caching/realtime/files/mail | `AddHeroPlatform` options and related building blocks | Do not import as a bundle. Admit each mechanism only for a named workload and owner. | `NOT_INTRODUCED`. |
| Tests | architecture, exception, tenant and integration tests | Reuse failure cases and real-host/test-container shapes while writing SquiFlow-owned assertions against SquiFlow boundaries. | Brand/identity/tenancy/Orders unit tests, real ASP.NET pipeline and OpenFGA tests, and real PostgreSQL migration/query/RLS/idempotency tests are active for their declared narrow scopes. |

## White-label boundary

The current `BrandProfile` is deployment-wide public bootstrap data. It contains display/legal names, bounded theme identity, safe asset links, safe legal/support links and a deterministic revision. It intentionally contains no executable content and no provider/internal configuration.

The repository may retain the internal development codename; active projects, assemblies and namespaces use the neutral `Application.*` identity. A white-label deployment changes user-visible identity through configuration without forking compiled identities, package IDs, database schema owners or security model. Tenant-specific overrides will require authoritative tenant context, persistence ownership, asset validation and fallback behavior before they are introduced.

## OpenAPI adaptation boundary

The inspected upstream OpenAPI extension and bearer transformer supplied the package/mechanics reference. SquiFlow retains direct ownership of the document identity and access classification. The upstream transformer applies bearer security to every operation, which would misdescribe public bootstrap and liveness routes, so that policy was not copied.

The current implementation uses `Microsoft.AspNetCore.OpenApi` 10.0.8 and pins its `Microsoft.OpenApi` 2.9.0 graph. `CoreApiOpenApi`, `ApiIdentityDocumentTransformer`, `ProtectedOperationSecurityTransformer` and `OrderCreateOperationTransformer` are the admitted repository-owned surface. `OpenApiContractTests` exercises the generated document through the real ASP.NET host, including the required idempotency header and replay/location response headers. Requalify when the OpenAPI package changes major version, authentication scheme changes, another public contract version is added, endpoint access metadata changes, or a second API host needs a document.

## Database governance before implementation

The first PostgreSQL slice must introduce all of these together:

1. a real capability-owned schema and migration;
2. a runtime credential without DDL, superuser or `BYPASSRLS` authority;
3. a separate deployment-time migration credential;
4. a one-shot migrator that does not seed or start the application;
5. concurrent-run coordination with a bounded wait/failure contract;
6. pending/applied migration visibility and stable exit codes;
7. real PostgreSQL tests for apply, repeat apply, concurrent apply and failure recovery;
8. a declared expand/migrate/switch/contract compatibility rule for later changes.

The IdentityAccess and Tenancy slices satisfy this contract for their global security/control schemas. The Orders slice additionally proves a narrowly scoped tenant-owned schema under a runtime role limited to its required reads/inserts, explicit tenant predicates, forced RLS, transaction-local context and pool reset. Broader business persistence must qualify independently.

## Requalification triggers

Re-review this ledger when the pinned FSH revision changes, a new FSH subsystem is considered, an adopted package changes major version, tenant-specific branding is introduced, the first protected endpoint appears, or the first PostgreSQL schema/migrator is added.
