# Implementation Hardcoding and Rule-Compliance Audit

**Version:** v0.1.0
**Audited:** 2026-09-20
**Baseline reviewed:** `main` after `695afdf`, including the subsequent neutral artifact-identity correction
**Scope:** current production/test projects, executable configuration, migrations, CI wrapper, closest `AGENTS.md` rules and current implementation claims

## 1. Result

The current implementation remains a narrow production-honest baseline. The audit found no recreated Parties slice, generic repository/unit-of-work layer, fake durable store, cross-module HTTP, default-tenant fallback, embedded production credential, business rule duplicated in host code, or introduced-but-unqualified business capability.

The audit did find convenience defaults and hardcoded runtime behavior that conflicted with the fail-fast, least-surprise, retry-ownership, minimal-public-surface, and codename-separation rules. They were corrected in the same change rather than accepted as later hardening.

`BLOCKED = none` after the corrections and recurring tests described below.

## 2. Corrected findings

| Finding | Why it conflicted | Correction | Regression guard |
|---|---|---|---|
| Migrator advisory-lock key encoded the development codename | The value crossed into PostgreSQL coordination state and made an internal codename a hardcoded operational identity | `Migration__AdvisoryLockKey` is now a required nonzero deployment-specific signed 64-bit value; `MigrationRunner` receives it explicitly | real PostgreSQL competing-migrator test uses an explicit synthetic key |
| `AllowedHosts` was `*` | A wildcard-all checked-in fallback accepted every Host header for ease | checked-in value is blank, startup requires one or more validated exact hosts or left-most-label wildcard DNS hosts, and wildcard-all is rejected | CoreApi configuration tests cover missing, wildcard, duplicate, padded, scheme/port/path and valid patterns |
| Npgsql connection strings could enable sensitive diagnostics | `Log Parameters`, provider error detail, failed batch commands, or retained security info could disclose protected values | startup rejects all four and the built data source forces them off | runtime database configuration tests cover every switch and effective settings |
| Provider-wide EF retries were enabled with a duplicated count of three | A generic provider retry has no command/idempotency owner and can obscure ambiguous failure semantics | removed `EnableRetryOnFailure`; future retry belongs to a named idempotent query/command/transaction policy with metrics and a bounded budget | source audit plus build; future retry introduction must satisfy `PERSISTENCE_SELECTION.md` |
| OIDC backchannel timeout and token clock skew lived as literals in `Program.cs` | Security/operations policy was hidden in composition code | both are required bounded `Authentication` configuration values | configuration tests reject missing/out-of-range values and preserve exact valid values |
| Public bootstrap cache age lived in the endpoint literal | Branding freshness was not deployment-visible | `Branding:CacheMaxAgeSeconds` is required and bounded from 0 through 86400 | configuration and real HTTP cache-header tests |
| Profile-runtime limits had code defaults as well as checked-in configuration | Missing configuration silently fell back instead of failing the resource-bound gate | code defaults were removed; options validation now rejects omitted values | host startup/options validation and profile-runtime tests |
| Several host/migrator implementation helpers were public | They were assembly implementation details rather than reusable contracts | endpoint helpers, access result and metadata types, migration runner and migration exception are now internal; explicit friend assemblies expose migrator internals only to their integration tests | successful real-host OpenAPI, endpoint and PostgreSQL migrator tests |
| Migrator ignored operator cancellation | The one-shot process did not carry cancellation into lock waits/list/apply operations | Ctrl+C now cancels the owned operation and returns exit code 130 | build plus existing cancellation-aware operations; process-signal proof remains part of deployment qualification |
| Compiled projects, assemblies, namespaces and synthetic PostgreSQL test resources retained the development codename | Build logs and binaries embedded an identity that must remain replaceable and must not become a product assumption | active solution, project, directory, namespace, assembly, test and synthetic resource identities now use neutral `Application.*`/`application_*` names | repository test scans active project/solution paths and loaded assembly identities; the normal build log exposes neutral output names |

## 3. Deliberate constants that remain

Hardcoded does not automatically mean incorrect. These constants express compatibility or safety contracts and should not become arbitrary deployment inputs:

- `/api/v1`, OpenAPI document `v1`, stable endpoint names and error codes are current wire-contract identifiers.
- `identity_access` and `tenancy` schemas, migration-history table names, migration identifiers, constraint/index names and enum storage values are persistence contracts.
- feature count/dependency/identifier bounds, identity component sizes and branding field/URL limits are input/resource safety bounds with tests.
- `v0.1.0` is the explicitly requested product-version lock until the production-capable product gate changes it.
- `Application.CoreApi.PrimaryDatabase` and `Application.CoreApi.ProfileRuntime` are brand-neutral internal telemetry namespaces; they contain no public product or development codename.
- The external GitHub repository name may still place the codename in a hosted-runner checkout path. That path is controlled by the repository name rather than the .NET solution or compiled artifacts; removing it requires a separate repository rename.
- event IDs, advisory-lock polling interval and maximum lock-timeout bound are implementation/operations protocol constants with named ownership, rather than tenant/business policy.

The checked-in pool sizes, retention periods, authentication timings and cache age are explicit deployable starting configuration. They are not measured production capacity claims and must be overridden when the qualified deployment evidence requires different values.

## 4. Rule-by-rule audit

### Current truth and scope

- Current projects and responsibilities match `README.IMPLEMENTATION.md`.
- Dormant Autofac profile-runtime mechanics are still not presented as durable profile authority or active implementation switching.
- ApplicationProfiles remains a bounded pure compiler without a production feature catalog.
- No historical Parties type/project/test/build surface was restored.

### Dependency and authority direction

- Host-neutral ApplicationProfiles, Branding, IdentityAccess and Tenancy projects contain no ASP.NET, EF/Npgsql, Autofac, OpenFGA, scheduler, broker, UI or OS dependencies.
- PostgreSQL types stay inside provider adapters and executable composition.
- CoreApi authenticates and resolves current account/membership authority; it does not trust a query/header TenantId or fall back to a default tenant.
- There is no Web/Sync/Worker/Admin duplicate business implementation and no in-process module call converted to HTTP/gRPC.

### Persistence and execution

- Capability-owned concrete queries are used; there is no `IRepository<T>`, universal unit of work or provider-neutral persistence hierarchy.
- Runtime DbContexts share one bounded/resetting Npgsql data source.
- DbMigrator remains a separate privileged one-shot process; CoreApi does not run migrations at startup.
- There is no in-memory queue presented as durable work and no fake persistence.
- The `Task.Run` inside the profile registry is owned by a `Lazy<Task<...>>`, awaited by acquisitions/retirement/shutdown, bounded by a semaphore and observed for failure. It is not untracked fire-and-forget work.
- Automatic provider retries are absent. A future retry must own semantic idempotency, whole-transaction boundaries, classification, budget and telemetry.

### Security and sensitive material

- Checked-in production configuration contains no credential, token, key or customer value.
- Test credentials and identities are explicit synthetic Testcontainers fixtures and remain limited to tests.
- JWT validation requires exact configured HTTPS issuer/audience, signature, lifetime and one issuer/subject identity.
- Account/tenant results use `no-store`; public bootstrap caching is explicit and bounded.
- Npgsql parameter/error/failed-command diagnostics and security-info retention fail startup when enabled.
- Host validation has no permissive checked-in fallback.

### Versioning and branding

- Product version remains exactly v0.1.0.
- Runtime branding and OpenAPI title come from required configuration without a codename fallback.
- Public responses and checked-in runtime configuration have tests preventing codename exposure.
- Active project, namespace, assembly, test and synthetic resource identities are neutral and contain no development codename.

## 5. Known non-claims

This audit does not qualify unintroduced responsibilities. In particular, it does not claim:

- production deployment/TLS/edge readiness;
- PostgreSQL RLS or tenant-owned business persistence;
- OpenFGA authorization;
- durable application-profile publication/activation;
- Worker, Sync, Web, Workstation or Admin implementation;
- backup/restore, HA, external pooler, measured rack capacity or zero downtime;
- real ZITADEL topology and browser/native session flows.

These remain `NOT_INTRODUCED`, not deferred hardening of an active path.

## 6. Requalification triggers

Repeat this audit when any of the following changes:

- a new executable, public endpoint, persistent schema or serialized contract is introduced;
- a retry, cache, queue, scheduler, actor, broker, external pooler or runtime plugin path is activated;
- tenant-owned data/RLS, OpenFGA, profile publication or implementation variants become active;
- product branding/versioning/package identity changes;
- deployment topology gains another API/Worker replica or privileged operations path.
