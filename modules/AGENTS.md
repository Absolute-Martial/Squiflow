# Capability Module Instructions

These rules apply below `modules/` in addition to the root instructions.

## Current capability truth

The first rebuilt capability project is:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
```

Its current declared scope is only the accepted Party structural classification `Person | Organization`. It does not claim complete Party identity/lifecycle/profile behavior, Customer/Account relationships, persistence, API, runtime, synchronization, or shared Foundation.

Keep additional Party semantics out until an accepted owner/current requirement makes them knowable. In particular, do not harden discovery-sensitive `Customer / Party / Account / Commercial Relationship` distinctions or choose an internal-ID encoding merely to make this first capability larger.

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

The current Parties capability is one compact host-neutral project. Do not split it into `Core`, `Server`, `Workstation`, `Postgres`, or `Contracts` projects until a real dependency/reuse/provider/platform boundary earns that split.

## Business code rules

- Use domain language from SquiFlow product/domain docs.
- Put invariants/state transitions where the owning business concept/application use case can enforce them consistently.
- Keep money, quantities, identifiers, revisions, timestamps/business dates explicit when the implemented journey requires them.
- Historical/issued truth uses correction/revision semantics where the domain requires it; do not silently overwrite history.
- Commands may return explicit outcomes/results. Queries must not hide surprising durable side effects.
- Model concurrency/idempotency explicitly when the operation can be retried or raced.

## Cross-module interaction

- Same-process cross-capability calls are in-process through reviewed public application/query surfaces.
- Distinguish internal/domain events from public integration events.
- Avoid object-navigation chains across another capability's internals; ask that capability for the business result needed.

No cross-module application contract exists yet in the current 0B slice.

## Host neutrality

Reusable capability meaning must not reference:

- Avalonia controls/view models;
- ASP.NET controllers/endpoints/HttpContext;
- Windows APIs;
- SQL/ORM/provider SDK types;
- identity/authorization/key-management provider SDK types;
- scheduler/actor/broker/provider telemetry types.

Adapters may depend on their framework/provider but must translate into SquiFlow-owned contracts before calling core/application behavior.

The current Parties project has no package or project dependency. That boundary is mechanically protected by `SquiFlow.Parties.Tests` until a real dependency is deliberately earned and the scope/evidence are requalified.

## DO NOT

- Do not create `WebOrderService`, `WorkstationOrderService`, `SyncOrderService`, etc. that redefine the same business rule per host.
- Do not create generic repositories/UoW/service-manager layers to make a module look layered.
- Do not expose persistence entities as public API/UI contracts by default.
- Do not add HTTP/gRPC between modules in the same host.
- Do not use global last-write-wins for protected business invariants.
- Do not make feature flags equivalent to permission grants.
- Do not use local Workstation facts as current server authority for security/financial/shared-stock invariants.
- Do not move `PartyKind` into Foundation merely because Foundation is named in Phase 0B.

## Testing

- Pure invariants/value semantics: deterministic fast tests.
- Application use cases: test authority inputs, expected versions, idempotency, state transitions, failure outcomes.
- Provider-specific behavior: test against the real provider when introduced.
- Add cross-tenant/current-authority negative tests for protected operations.
- When versioned contracts are introduced, preserve old fixtures and compatibility tests.

For the current slice, only the Party-kind semantic and the capability dependency boundary are active test claims.
