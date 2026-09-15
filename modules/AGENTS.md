# Capability Module Instructions

These rules apply below `modules/` in addition to the root instructions.

## Current capability truth

Two real capability projects now exist:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj      # qualified 0B
modules/payments/SquiFlow.Payments/SquiFlow.Payments.csproj   # qualified 0E
```

`SquiFlow.Parties` owns only the accepted Party structural classification `Person | Organization`. It does not claim complete Party identity/lifecycle/profile behavior, Customer/Account relationships, persistence, API, runtime, synchronization, or shared Foundation.

`SquiFlow.Payments` currently owns only the documented payment-status vocabulary:

```text
NotStarted
Pending
Succeeded
Failed
OutcomeUnknown
PartiallyRefunded
Refunded
Reversed
```

The Payments slice does **not** introduce payment identity, amount/currency/rounding, transition rules, retry/idempotency, outcome-unknown reconciliation, settlement, persistence, API, authorization, or runtime behavior. Those responsibilities remain `NOT_INTRODUCED` until a real payment operation earns them.

Keep additional semantics out until an accepted owner/current requirement makes them knowable. In particular, do not harden discovery-sensitive `Customer / Party / Account / Commercial Relationship` or `Order / Request / Job / Work / Transaction / Sale` distinctions merely to make a capability larger.

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

The current Parties and Payments capabilities are compact host-neutral projects. Do not split either into `Core`, `Server`, `Workstation`, `Postgres`, or `Contracts` projects until a real dependency/reuse/provider/platform boundary earns that split.

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

No cross-module application contract exists yet. Parties and Payments currently have no project/package dependency and do not reference each other.

## Host neutrality

Reusable capability meaning must not reference:

- Avalonia controls/view models;
- ASP.NET controllers/endpoints/HttpContext;
- Windows APIs;
- SQL/ORM/provider SDK types;
- identity/authorization/key-management provider SDK types;
- scheduler/actor/broker/provider telemetry types.

Adapters may depend on their framework/provider but must translate into SquiFlow-owned contracts before calling core/application behavior.

The qualified Parties dependency boundary is mechanically protected by `SquiFlow.Parties.Tests`. The qualified Payments dependency boundary is mechanically protected by `SquiFlow.Payments.Tests`, with successful executable evidence recorded in `docs/implementation/phases/phase-0/0E_STATUS.md`.

## Shared Foundation rule

The existence of two capabilities is not, by itself, evidence for a shared Foundation project.

Extract a shared product-wide primitive only when real capability work demonstrates the same stable semantic/contract and keeping it capability-owned would duplicate business meaning or violate dependency direction. Do not manufacture a shared abstraction merely because Parties and Payments now coexist.

## DO NOT

- Do not create `WebOrderService`, `WorkstationOrderService`, `SyncOrderService`, etc. that redefine the same business rule per host.
- Do not create generic repositories/UoW/service-manager layers to make a module look layered.
- Do not expose persistence entities as public API/UI contracts by default.
- Do not add HTTP/gRPC between modules in the same host.
- Do not use global last-write-wins for protected business invariants.
- Do not make feature flags equivalent to permission grants.
- Do not use local Workstation facts as current server authority for security/financial/shared-stock invariants.
- Do not move `PartyKind` or `PaymentStatus` into Foundation merely to create reuse evidence.
- Do not infer payment transition rules from enum ordering or numeric values.

## Testing

- Pure invariants/value semantics: deterministic fast tests.
- Application use cases: test authority inputs, expected versions, idempotency, state transitions, failure outcomes.
- Provider-specific behavior: test against the real provider when introduced.
- Add cross-tenant/current-authority negative tests for protected operations.
- When versioned contracts are introduced, preserve old fixtures and compatibility tests.

Qualified 0B currently protects the Party-kind semantic and Parties dependency boundary. Qualified 0E adds only the Payment-status semantic and Payments dependency boundary; no broader payment/runtime claim is implied.
