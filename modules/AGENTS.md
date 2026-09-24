# Capability Module Instructions

These rules apply below `modules/` in addition to the root instructions.

## Current capability truth

The current capability projects are `Application.Profiles`, `Application.Branding`, `Application.IdentityAccess`, `Application.Tenancy`, `Application.Customers`, `Application.Orders`, and the earned provider-isolated IdentityAccess/Tenancy/Customers/Orders PostgreSQL adapters. ApplicationProfiles owns bounded feature definitions, catalog graph validation and deterministic effective-selection compilation; it owns no production feature catalog, durable tenant profile authority, activation, authorization or Autofac integration. Branding owns bounded public deployment identity. IdentityAccess owns stable external `(issuer, subject)` account binding. Tenancy owns the tenant registry, current account membership query and membership-derived `TenantContext`; it owns no OpenFGA role/permission model. Customers owns immutable tenant-owned customer organizations and child programs with bounded create/read/browse and caller-scoped semantic idempotency. Orders owns immutable priced draft create/read, bounded newest-first browse and one-way abandonment, with caller-scoped semantic idempotency per command and optional validated customer/program attribution. Abandonment requires the distinct `order_abandoner` permission and expected revision, changes `draft` to `abandoned`, records abandoning account/timestamp and increments revision, while preserving priced content and the original create receipt. Its PostgreSQL adapter owns the tenant-owned draft, line and command-receipt persistence boundary, including bounded lifecycle-column updates. Draft editing/deletion and the broader Order lifecycle remain absent. The former `Application.Parties` enum-only slice remains purged.

The next slice starts from a useful capability journey and the source-admission rule in `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md`. Do not harden discovery-sensitive `Customer / Party / Account / Commercial Relationship` distinctions or choose an internal-ID encoding until the active journey and accepted owner require them.

## Ownership

A module/capability owns one coherent area of business meaning. It may expose host-neutral public application/query contracts, but other modules must not reach into its private persistence tables/provider internals.

A capability may grow progressively across phases. The existence of a module does not imply that every future persistence, sync, Worker, Web, or Workstation adapter must exist now.

## Preferred logical shape

Use only the parts earned by current work:

```text
Capability
├── domain/core meaning
├── public contracts/application operations
├── capability-owned permissions/features/settings
└── host/provider adapters only when required
```

Separate `.csproj` files are earned when compiler-enforced neutrality, real cross-host reuse, provider isolation, packaging, or complexity justifies them. Do not scaffold every theoretical project.

Prefer one compact host-neutral capability project first. Split it into `Core`, `Server`, `Workstation`, `Postgres`, or `Contracts` projects only when a real dependency/reuse/provider/platform boundary earns that split.

## Business code rules

- Use domain language from the repository's product/domain docs.
- Put invariants/state transitions where the owning business concept/application use case can enforce them consistently.
- Keep money, quantities, identifiers, revisions, timestamps/business dates explicit when the implemented journey requires them.
- Historical/issued truth uses correction/revision semantics where the domain requires it; do not silently overwrite history.
- Commands may return explicit outcomes/results. Queries must not hide surprising durable side effects.
- Model concurrency/idempotency explicitly when the operation can be retried or raced.

## Cross-module interaction

- Same-process cross-capability calls are in-process through reviewed public application/query surfaces.
- Distinguish internal/domain events from public integration events.
- Avoid object-navigation chains across another capability's internals; ask that capability for the business result needed.

The Tenancy membership schema refers to stable IdentityAccess account IDs through an explicit database foreign key, while each capability keeps its provider rows private. No generic cross-module repository or runtime service contract exists.

## Host neutrality

Reusable capability meaning must not reference:

- Avalonia controls/view models;
- ASP.NET controllers/endpoints/HttpContext;
- Windows APIs;
- SQL/ORM/provider SDK types;
- identity/authorization/key-management provider SDK types;
- scheduler/actor/broker/provider telemetry types.

Adapters may depend on their framework/provider but must translate into repository-owned contracts before calling core/application behavior.

The current unit tests mechanically protect ApplicationProfiles, Branding, IdentityAccess, Tenancy, Customers and Orders from ASP.NET, EF/Npgsql, OpenFGA and FSH dependencies. Preserve and extend those checks as capability boundaries grow.

## DO NOT

- Do not create `WebOrderService`, `WorkstationOrderService`, `SyncOrderService`, etc. that redefine the same business rule per host.
- Do not create generic repositories/UoW/service-manager layers to make a module look layered.
- Do not expose persistence entities as public API/UI contracts by default.
- Do not add HTTP/gRPC between modules in the same host.
- Do not use global last-write-wins for protected business invariants.
- Do not make feature flags equivalent to permission grants.
- Do not use local Workstation facts as current server authority for security/financial/shared-stock invariants.
- Do not recreate or move the deleted `PartyKind` representation merely because it appeared in former Phase 0B.

## Testing

- Pure invariants/value semantics: deterministic fast tests.
- Application use cases: test authority inputs, expected versions, idempotency, state transitions, failure outcomes.
- Provider-specific behavior: test against the real provider when introduced.
- Add cross-tenant/current-authority negative tests for protected operations.
- When versioned contracts are introduced, preserve old fixtures and compatibility tests.

Current active claims are limited to bounded feature graph/selection compilation, bounded Branding values, exact external-account binding, current active tenant-membership queries and membership-derived `TenantContext`, immutable customer organization/program context, plus optionally attributed immutable priced order-draft create/read/browse/abandon. Abandonment is a one-way revision-checked transition with a distinct permission, lifecycle metadata and caller-scoped idempotency; no broader order lifecycle is implied. Provider behavior is covered by real PostgreSQL evidence as defined by the focused Orders owner.
