# Schema and Contract Evolution

**Status:** Accepted architecture direction  
**Version:** v0.1.0
**Scope:** database schema, Workstation-local schema, API contracts, synchronization contracts, durable messages/jobs/events, IPC/config snapshots, and future independently versioned schemas

## 1. Decision

Schema and contract evolution is a first-class SquiFlow architecture concern. It is part of release safety, synchronization safety, rollback/roll-forward planning, canary/rolling compatibility, durable-work correctness, and long-offline Workstation support.

SquiFlow must never assume that every Workstation, backend process, durable message, migration, integration, or stored snapshot upgrades at the same instant.

The core rule is:

> Supported old and new readers/writers/contracts may coexist temporarily. Compatibility is explicit, directional, tested, and retired only with evidence.

This document coordinates the cross-cutting model. It does **not** replace the detailed rules owned by:

- `docs/data/PERSISTENCE_SELECTION.md` for PostgreSQL/SQLite schema and migration behavior;
- `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md` for HTTP/API compatibility;
- `docs/api/TRANSPORT_SELECTION.md` for gRPC/Protobuf adoption and transport-specific compatibility;
- `docs/sync/SYNC_AND_AUTHORITY.md` for synchronization authority/long-offline semantics;
- `docs/workstation/UPDATE_MIGRATION_RECOVERY_OWNERSHIP.md` for Workstation update/migration/recovery;
- `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md` for release/rollback/roll-forward evidence.

## 2. Version overlap is normal

A release window may contain more than one supported contract/schema generation.

```text
                 current server state
                         │
          ┌──────────────┴──────────────┐
          │                             │
   older supported                 newer clients
   Workstations/jobs                and processes
          │                             │
          └──────────────┬──────────────┘
                         │
                 compatibility boundary
                         │
                         ▼
                 current canonical logic
```

Examples include:

- a Workstation that skipped one or more releases;
- old and new backend instances during a rolling/canary deployment;
- pending local sync operations created by an older client;
- durable jobs/messages created before deployment and executed after deployment;
- stored idempotency results or rule/workflow/form snapshots using older shapes;
- external integrations that cannot upgrade on the server deployment schedule.

Compatibility windows are deliberate product/operational contracts. They are not accidental indefinite support.

## 3. Do not collapse all versions into one number

SquiFlow versions different compatibility domains independently.

Conceptually:

```text
Product release version         0.0.x
Central DB migration version    142
Workstation SQLite schema        27
Domain/public contract           11
REST API contract                14
Sync protocol                     8
Sync operation envelope           7
gRPC/Protobuf contract           19
Durable job/message schema       31
Rule/workflow/form snapshot       6
OpenFGA model revision            n
Guard/Workstation IPC             4
```

These versions may move at different rates.

A product SemVer change does not prove API compatibility. An API version does not prove database compatibility. A DB migration number does not prove an old Workstation can synchronize safely.

## 4. Standard evolution ladder

The standard SquiFlow evolution sequence is:

```text
EXPAND
  -> add compatible new shape/capability

OVERLAP / TRANSITION
  -> supported old and new readers/writers coexist
  -> adapters/resolvers understand both where required

MIGRATE / BACKFILL
  -> historical data and durable state move with bounded observable work

CUTOVER
  -> new shape becomes authoritative/preferred
  -> old shape is no longer produced except where compatibility requires it

CONTRACT
  -> remove obsolete shape only after inventory/drain/retirement evidence
```

This refines the existing `expand-migrate-switch-contract` rule by making the overlap/transition state explicit.

Example: splitting `customer.name` into `first_name` and `last_name` must not begin by deleting `name`.

```text
1. Expand
   add first_name / last_name

2. Overlap
   old readers/writers remain supported according to the compatibility plan

3. Migrate
   backfill historical rows and reconcile ambiguous data deliberately

4. Cutover
   new application paths use the new authoritative representation

5. Contract
   remove old representation only after supported readers/writers are gone
```

A migration must define semantic ambiguity. Mechanical transformation is not allowed to invent business meaning when old data cannot be split safely.

## 5. Compatibility domains remain separate

SquiFlow uses one governance model but **not one universal compatibility algorithm**.

Maintain separate compatibility policy/grids for at least:

1. central PostgreSQL schema;
2. Workstation SQLite schema;
3. REST/task HTTP contracts;
4. gRPC/Protobuf contracts when adopted;
5. durable event/message/job schemas;
6. synchronization protocol and `OperationEnvelope` schemas;
7. Guard/Workstation or other durable IPC contracts;
8. rule/workflow/form/configuration snapshots;
9. GraphQL schema if GraphQL is ever adopted.

Each domain defines what backward, forward, full, or incompatible means for that technology and semantic contract.

Do not infer that a change safe for JSON/HTTP is safe for Protobuf, SQL, persisted snapshots, or SQLite.

## 6. Directional compatibility

Compatibility is directional.

The conceptual vocabulary is:

```text
Backward compatible
= a newer producer/implementation can still serve an older supported consumer as defined by that contract

Forward compatible
= a newer consumer can safely understand an older supported producer/persisted shape as defined by that contract

Full compatible
= both supported directions are safe

Incompatible
= explicit transform/upgrade/resnapshot/maintenance is required
```

The exact interpretation belongs to the contract family.

A generic change table can be used only as a review prompt, not as universal truth:

| Change | Older consumer reading newer producer | Newer consumer reading older producer |
|---|---|---|
| additive optional/nullable field | often compatible | often compatible |
| new required field | often incompatible | policy/transform dependent |
| field removal | policy dependent | often incompatible |
| rename | usually transform/version required | usually transform/version required |
| enum expansion | consumer behavior dependent | usually safer than narrowing but still contract-specific |
| enum narrowing | usually breaking | producer/data dependent |
| type change | usually breaking | usually breaking |

Technology-specific rules override this table.

## 7. Compatibility registry: source-controlled first

SquiFlow accepts a **logical compatibility/schema registry** now, but does not require a separately deployed registry service.

Initial form:

```text
source-controlled contract metadata
+ generated descriptors where useful
+ CI compatibility tests
+ runtime resolver tables/code only where a boundary needs them
```

Conceptual entries may include:

```text
ContractId
ContractFamily
Version
OwnerCapability
Producer(s)
Consumer(s)
CompatibilityMode
IntroducedAtRelease
DeprecatedAtRelease
MinimumSupportedVersion
RetirementCondition
TransformerId          # when one is required
MigrationId            # when persisted state changes
Notes / semantic constraints
```

Example logical identities:

```text
Customers.Api.CustomerDetails.v3
Orders.Sync.CreateOrder.v7
Sync.OperationEnvelope.v8
Orders.Job.ExpireOrder.v2
Customers.Event.CustomerUpdated.v4
Workstation.Customers.Sqlite.v12
```

The registry is governance metadata, not business authority.

A dedicated runtime registry product/service is introduced only when independently versioned producers/consumers, external teams/integrations, or scale make machine-enforced centralized registration materially safer than source-controlled metadata. That future service must not become a required network hop for ordinary in-process domain execution merely because the word `registry` exists.

## 8. Schema/contract resolution

Compatibility must not be implemented independently in every controller/service.

At a versioned boundary, a **contract resolver/normalizer responsibility** determines whether the submitted/requested version is supported and how it maps into the current application contract.

Conceptually:

```text
producer/persisted contract version
            │
            ▼
    contract/schema resolver
            │
      ┌─────┼───────────────┐
      │     │               │
 exact   compatible      transform
 match   supported path   required
      │     │               │
      └─────┼───────────────┘
            │
            ▼
 canonical application command/query/value model
```

Possible outcomes:

```text
Exact
Compatible
Transformed
UpgradeRequired
ResnapshotRequired
UnsupportedContractVersion
QuarantinedDurableWork
Incompatible
```

The resolver must never silently reinterpret an old request as a materially different business command.

A resolver does not replace authorization, tenant isolation, validation, idempotency, concurrency, domain invariants, or current server authority.

## 9. Resolution is boundary-specific, not one giant service

The `resolver` is a responsibility first, not automatically a network service or global `SchemaResolver` singleton.

Examples:

```text
REST API
  -> route/media/header/version adapter
  -> current request DTO / application command

SyncApi
  -> sync protocol + operation schema resolver
  -> current semantic OperationEnvelope/application admission command

Worker
  -> durable job payload version resolver
  -> current job handler command

Workstation startup
  -> local migration planner
  -> current SQLite schema
```

Where the contract is compile-time and single-process, generated/static compatibility code is preferable to an unnecessary runtime registry lookup.

## 10. Workstation synchronization

Schema negotiation is especially important at the Workstation boundary because local durable intent can outlive several server releases.

A Workstation may report version evidence such as:

```text
ApplicationVersion
SyncProtocolVersion
OperationEnvelopeVersion
CapabilityContractVersions
LocalSchemaVersion
Rule/ConfigurationSnapshotVersion
```

The server does not blindly choose an arbitrary intermediate version. It evaluates the registered supported compatibility path for the exact producer/consumer pair.

Example:

```text
Workstation operation schema v17
        │
        ▼
Sync compatibility resolver
        │
        ├── server supports direct v17 admission -> normalize and continue
        ├── registered v17 -> current transform -> normalize and continue
        ├── requires resnapshot/upgrade -> explicit result
        └── unsupported/unsafe -> reject without corrupting pending local intent
```

If the operation is incompatible, preserve pending local intent/evidence where possible so upgrade/recovery can reconcile it rather than deleting it.

Transport selection remains independent: HTTP or gRPC can carry the same compatibility semantics.

## 11. Database migration versus contract transformation

Do not conflate these operations:

```text
Database migration
= transform persisted authoritative/local state

Contract transformation
= translate a supported old wire/durable contract into the current application contract

Domain transition
= perform current business behavior under current authority/invariants
```

A request transformer must not mutate the database merely because it normalized an old request.

A DB migration must not be treated as proof that old API/sync clients are compatible.

## 12. Durable jobs, messages, and events

Durable work survives deployments, so the payload/version used when work was created may differ from the code version that eventually executes it.

Every durable envelope that can cross a release boundary carries stable kind/contract version metadata sufficient to resolve it safely.

For example:

```text
JobKind
JobSchemaVersion
OwningCapability
SemanticOperationId / idempotency identity
Tenant/scope
Configuration/rule revision where material
Payload
```

A deployment cannot delete a job/event/message reader merely because new code no longer emits that old version. Old durable state must be drained, transformed, quarantined with a recovery path, or explicitly retired according to policy.

This does not introduce event sourcing or a broker as a baseline.

## 13. Protobuf/gRPC-specific note

If gRPC/Protobuf is adopted for SyncApi or another real boundary, its compatibility rules are explicit and technology-specific.

SquiFlow must use `.proto` compatibility tests and deliberate field/enum evolution rules. The REST/JSON compatibility matrix is not reused mechanically.

Generated contracts are valuable only if their versioning/retirement behavior is also owned and tested.

## 14. GraphQL-specific note

GraphQL remains deferred until a real query-composition need justifies it.

If introduced, it receives its own schema/deprecation/field-authorization/consumer-retirement rules. The existence of this cross-cutting compatibility model does not make GraphQL baseline.

## 15. Compatibility matrix artifact

For every long-lived boundary, CI/release evidence includes a compatibility matrix appropriate to its domain.

Conceptual form:

| Producer/version | Consumer/version | Direction | Expected result | Resolver/transform | Retirement status |
|---|---|---|---|---|---|
| v1 | v1 | same | allowed | none | supported |
| v2 | v2 | same | allowed | none | supported |
| v2 | v1 | backward | allowed/warn/reject per contract | optional | supported/deprecated |
| v1 | v2 | forward | allowed/warn/reject per contract | optional | supported/deprecated |
| v3 | v1 | multi-version | resolver/transform/upgrade required | explicit | policy dependent |

Separate matrices exist for different contract families. Do not publish one misleading universal grid.

## 16. Retirement and contraction gate

Before removing an old schema/contract/version, prove what still depends on it.

Inventory as applicable:

- supported Workstation versions;
- active/rolling backend versions;
- pending local sync operations;
- durable jobs/messages/events;
- idempotency result payloads;
- stored rules/workflows/forms/configuration snapshots;
- external integrations;
- backup/restore artifacts that may reintroduce old state;
- migration/backfill completeness;
- old read/write code paths;
- telemetry showing remaining use where telemetry is reliable enough for inventory.

Then choose deliberately:

```text
drain
migrate/backfill
transform
resnapshot
require upgrade
quarantine/reconcile
maintenance cutover
retain compatibility longer
```

Only after evidence is complete may contract/schema contraction remove the old shape.

## 17. Deployment/canary implication

Canary/blue-green/rolling deployment is safe only if the data/contracts support simultaneous old/new code where the rollout strategy requires it.

Pre-deployment must answer:

```text
Can old code read/write expanded schema?
Can new code read old data?
Can old Workstations sync during the rollout?
Can durable jobs created before deployment still execute?
Can rollback binary run safely against the post-expand schema?
Does cutover make binary rollback unsafe?
What is the roll-forward path if data transformation is irreversible?
```

A green process-health check cannot prove schema/contract compatibility.

## 18. Verification requirements

Compatibility verification is not limited to empty databases or the newest client.

Required tests for the first real compatibility slice include as applicable:

- old reader + new writer;
- new reader + old writer;
- old Workstation + new server;
- new Workstation + supported older server only where the deployment contract allows that direction;
- rolling old/new backend instances against the expanded schema;
- non-empty historical data;
- pending offline sync created before upgrade;
- durable job/message created before upgrade and executed after upgrade;
- interrupted backfill/resume;
- rollback/roll-forward after partial migration;
- unsupported version fails explicitly without partial durable mutation;
- transformation is deterministic/idempotent where retries are possible;
- contraction fails CI/release gate while known old consumers remain.

Compatibility fixtures should be retained for supported historical versions rather than regenerated only from current models.

## 19. Ownership model

Contract/schema ownership follows capability/runtime ownership.

Examples:

```text
Orders owns Orders public command/query/event contracts
Synchronization owns cross-capability sync envelope/cursor protocol
Worker runtime owns generic durable job envelope
Orders owns Orders-specific job payload schema
Workstation maintenance owns local migration execution mechanics
Orders/Customers/etc. own their local capability data migrations
```

A central compatibility framework may provide metadata primitives/tests/resolution helpers. It must not become a giant global business-schema owner.

## 20. Repository direction

Initial implementation should remain lightweight and source-controlled.

A future shape may be:

```text
foundation/
└── compatibility/                  # create when implementation is real
    └── SquiFlow.Compatibility/
        ├── ContractId.cs
        ├── ContractVersion.cs
        ├── CompatibilityResult.cs
        └── resolver/test helpers

modules/<capability>/
└── ... capability-owned contract/version metadata

tests/
└── compatibility fixtures/matrices
```

Do not create this project until real code requires it. Documentation establishes ownership and invariants first.

Do not create a standalone `schema-registry` service/container merely to match an architecture diagram.

## 21. What remains open

This decision does **not** prematurely choose:

- REST version transport: URI vs header vs media type;
- final Sync transport: HTTP vs gRPC;
- exact first supported Workstation overlap window;
- exact retention period for deprecated API/sync contracts;
- exact generated-schema tooling;
- whether a future multi-producer/consumer ecosystem justifies an external schema-registry product/service;
- exact transformer code-generation versus handwritten strategy per contract family.

Those remain implementation/operational decisions under the accepted compatibility invariants.

## 22. Architectural invariant

> SquiFlow evolves durable state and public/long-lived contracts through explicit version overlap, directional compatibility, registered ownership, bounded transformations, and expand-overlap-migrate-cutover-contract retirement. Schema/version numbers are separate per compatibility domain. A source-controlled registry and resolver responsibility exist where needed, while a separately deployed registry service is introduced only when a real independent producer/consumer ecosystem earns it.