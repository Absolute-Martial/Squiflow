# Capability Core and Host-Specific Execution

**Status:** Accepted architecture direction  
**Version:** v0.1.0

## 1. Vocabulary

SquiFlow does not use a generic `shared modules` layer. The stable model is:

```text
Foundation
   ↓
Capability-owned business meaning
   ├── Capability Core (host-neutral/deterministic where reusable)
   └── Authoritative Application (server current authority/facts/commit)
   ↓
Host / infrastructure adapters
```

- **Foundation** contains small product-wide technical/domain primitives with no business-capability ownership.
- **Capability Core** owns the host-neutral/deterministic center of one business capability where such reusable logic exists.
- **Authoritative Application** owns server-side authoritative entry use cases, current authority/facts, admission, concurrency/idempotency and commit orchestration for that capability.
- **Host/infrastructure adapters** supply transport, presentation, provider/persistence integration and host-specific effects.

Examples of capabilities are Customers, Orders, Inventory, Quotations, Staff and Devices.

Detailed server-authority owner: `docs/architecture/AUTHORITATIVE_CAPABILITY_MODULES.md`.

## 2. One capability, one business meaning

A capability has one source implementation of its business meaning. Do not create parallel business implementations such as:

```text
Orders.WorkstationBusiness
Orders.ServerBusiness
Orders.WebBusiness
Orders.SyncBusiness
Orders.WorkerBusiness
```

For deterministic decisions that are genuinely reusable, model the processor once:

```text
Intent + Facts + Rule/Policy Snapshot
              ↓
       Capability Core
              ↓
           Decision
```

The execution path supplies facts and performs effects.

For Orders:

```text
                    Orders Capability
                          │
                  Capability Core
                    /           \
                   /             \
                  v               v
       local facts/SQLite   authoritative facts/PostgreSQL
                  │               │
       Workstation execution   authoritative application
```

The deterministic processor may be the same code even though authority and persistence differ.

## 3. Same authoritative modules behind WebApi and SyncApi

Interactive Web/API traffic and Workstation synchronization are separate backend workload hosts, but they converge after transport/admission into the same authoritative modules.

```text
WebApi -----┐
            ├──> Orders / Customers / Inventory / ... authoritative modules
SyncApi ----┘                         │
                                     ▼
                              PostgreSQL + Outbox
```

`WebApi` and `SyncApi` may invoke different entry use cases because their inputs/trust states differ. Example:

```text
WebApi  -> Orders.CreateOrder
SyncApi -> Orders.AdmitProvisionalOrder
Worker  -> Orders.ExpireOrder
```

These entry points belong to one Orders capability and share its business meaning/invariants. They are not separate Order implementations.

The API hosts own protocol/workload concerns. The authoritative module owns current business authorization where required, current facts/rules, invariants, concurrency/idempotency and authoritative commit.

## 4. What belongs in a Capability Core

A Capability Core may own:

- entities/value objects where they protect real business meaning;
- operation/intention contracts;
- fact contracts;
- deterministic validation and calculations;
- business decision results;
- deterministic rules/policies;
- stable feature definitions;
- stable permission definitions;
- stable setting definitions;
- domain/business events.

A Capability Core must not depend on host/provider technologies such as:

- Avalonia;
- ASP.NET Core;
- Windows APIs;
- EF Core/Npgsql/SQLite provider APIs;
- OpenFGA/ZITADEL SDKs;
- Quartz/TickerQ;
- Proto.Actor;
- MassTransit/RabbitMQ;
- provider-specific telemetry/storage SDKs.

`net10.0` shared projects remain the baseline. .NET 10 LTS is the selected runtime baseline for the current implementation.

## 5. Host/application adapters

### Workstation execution

May own:

- Avalonia presentation and ViewModels;
- local fact providers backed by SQLite/local snapshots;
- operation preparation and provisional projection orchestration;
- local operation/projection persistence and outbox;
- local hardware/platform integration;
- local process/runtime integration.

### Server authoritative application

May own/orchestrate:

- authoritative fact providers;
- current authorization through appropriate abstractions/integration;
- PostgreSQL transaction/concurrency boundaries;
- current rule/configuration versions;
- semantic idempotency;
- authoritative admission/commit;
- outbox creation;
- provider effects through explicit boundaries.

### Web presentation adapter

May own:

- Web-specific pages/components/presentation state;
- task-oriented API client calls;
- Web-only administration UX where appropriate.

Web presentation does not contain another business implementation.

The Web client does **not** own a persistent local business database. SQLite/WAL is a Workstation-only persistence choice. Browser storage, if used, is limited to disposable UI/session cache or temporary transfer state that can be deleted without losing authoritative or pending business truth. Offline-authoritative Web/PWA persistence requires a separate explicit architecture decision.

### API hosts/adapters

`WebApi` and `SyncApi` are different server workload/API adapters into the same authoritative modules. They may both support commands and reads. They differ in protocol, identity/device context, batching, cursor, fairness, rate/backpressure and scaling concerns without duplicating business meaning.

## 6. Processing modes

Each capability operation declares/documents the execution mode that applies on a host:

```text
DeviceLocal
LocalProvisional
ServerAuthoritative
```

- **DeviceLocal** exists only on the Workstation/device and has no server business effect.
- **LocalProvisional** may prepare an operation and update a clearly provisional local projection for offline continuity; it later requires authoritative admission before it has a central business effect.
- **ServerAuthoritative** requires current server authority and does not become valid merely because a client UI exposed it.

Examples:

| Capability | Workstation | Web |
|---|---|---|
| Create order | LocalProvisional where allowed | ServerAuthoritative |
| Create/update customer | LocalProvisional where allowed | ServerAuthoritative |
| Assign staff permissions | ServerAuthoritative | ServerAuthoritative |
| Register/revoke device | ServerAuthoritative | ServerAuthoritative |
| SQLite maintenance | DeviceLocal | unavailable |
| Printer/scanner integration | DeviceLocal | unavailable |
| Guard/update recovery | DeviceLocal | unavailable |

## 7. One semantic operation, staged authority

Do not describe the model as “the Workstation executes and the server executes again.” A `LocalProvisional` path has one semantic operation identity and two different responsibilities:

```text
Workstation: prepare
  -> validate locally available shape and business facts
  -> compute immediate provisional projection/user feedback
  -> atomically persist operation + projection + outbox in SQLite
  -> submit semantic OperationEnvelope + dependency evidence
  -> SyncApi

Server: admit and commit
  -> deduplicate OperationId
  -> authenticate and authorize current actor/device/tenant
  -> load current authoritative facts, rules and dependency versions
  -> choose the operation-owned admission strategy
  -> commit authoritative state + receipt + outbox atomically where one store owns them

Workstation: reconcile
  -> durably apply authoritative receipt/change
  -> replace, adjust or retain the provisional projection for review
```

The local durable commit means the user's intent will survive restart and can remain useful offline. It does not claim that PostgreSQL, another device, an external provider or a central invariant has accepted the operation.

The server owns the only authoritative state transition. It can reuse the same deterministic Capability Core, but it does so as part of admission against trusted facts. The client result is evidence and a provisional projection; it is never a trusted state delta or permission grant.

### 7.1 Operation envelope semantics

When introduced by a real capability, the transport-independent envelope carries the minimum semantic evidence required by that operation, such as:

- stable `OperationId` and operation kind/version;
- tenant, actor and device references that the server independently validates;
- semantic intent rather than arbitrary table writes;
- expected aggregate/base revisions and the identifiers of facts on which the provisional result depended;
- rule/configuration/schema versions relevant to compatibility;
- payload integrity and client timing metadata where useful for diagnosis, never as server authority.

The envelope is not a serialized dependency-injection container, a client database changeset, or permission to replay provider effects.

### 7.2 Admission strategy belongs to the operation

There is no universal “run it again” rule. Each introduced operation declares one of these strategies and proves that it preserves its invariants:

| Strategy | Use when | Server behavior |
|---|---|---|
| `ServerRequired` | Current security, external authority or a protected shared invariant is required before useful local progress | Workstation may save a draft/intent, but does not present a provisional business success; server performs the authoritative operation online |
| `ValidateAndCommit` | The intent is locally useful, but the final transition depends on current authority or shared facts | Server validates current facts and computes the authoritative transition; unchanged dependency evidence can avoid unrelated reads/work |
| `ExpectedRevision` | The operation targets a versioned aggregate and concurrent edits must be detected | Server applies only against the expected revision, then accepts, reports conflict, or invokes an operation-specific merge/rebase policy |
| `ConvergentMerge` | The operation is proven mergeable while preserving its named invariants | Server merges the semantic operation and records the canonical result; generic last-write-wins is insufficient proof |
| `BoundedDelegation` | A scarce capability can be safely leased in advance with an explicit limit and expiry | Workstation consumes a signed/identified grant offline; server verifies single use and limits during admission |

`BoundedDelegation` is an optional capability-specific optimization, for example a preallocated identifier range or explicitly reserved quantity. It is not general offline authority and must define issuance, expiry, revocation limits, exhaustion, duplicate use and reconciliation before adoption.

### 7.3 Receipts are the reconciliation contract

An authoritative receipt is stable by `OperationId` and distinguishes at least the semantic outcomes needed by the operation. Candidate outcome classes are:

```text
Accepted
Adjusted
Conflict
Rejected
AuthorizationChanged
UpgradeRequired
AlreadyApplied
```

The receipt identifies the authoritative revision/result or the reason and recovery action. A repeated equivalent submission returns the stored outcome; the same `OperationId` with changed intent is rejected. The Workstation acknowledges/removes an outbox item only after it has durably applied the corresponding receipt or authoritative change. Rejected or conflicting user work remains available for explanation, correction, export or retry according to policy.

### 7.4 Why this is a hybrid rather than a universal sync algorithm

Database-change capture can efficiently transport rows, but raw SQLite changesets require compatible schemas/base state and an application conflict handler. A local-database sync engine can manage local persistence, upload queues and server-to-client replication, but its own model still sends writes through an application backend that may accept, modify or deny them. Neither mechanism replaces capability authorization or invariant admission.

CRDT/convergent structures are appropriate only for operations whose merge is proven to preserve the required business invariants. Invariant-confluence research gives the relevant test: if independently valid states can merge into an invalid state, coordination or server admission is required. Expected revisions/optimistic concurrency remain the ordinary choice for non-mergeable aggregate edits.

No general sync engine or CRDT framework is selected by this decision. A future capability may POC a transport/storage mechanism against its real payload, conflict, encryption, migration and recovery obligations while preserving this semantic contract. The focused decision owner is `docs/decisions/DUAL_PROCESSING_AND_IN_PROCESS_COORDINATION.md`; detailed trust/sync rules are in `docs/sync/SYNC_AND_AUTHORITY.md`.

Evidence informing this boundary includes the [SQLite Session Extension](https://www.sqlite.org/sessionintro.html), [PowerSync's server-authoritative upload path](https://powersync.com/blog/checkpoint-requests-client-synced-now), [Automerge conflict semantics](https://automerge.org/docs/reference/documents/conflicts/), the [invariant-confluence paper](https://www.vldb.org/pvldb/vol8/p185-bailis.pdf), [RFC 9110 conditional requests](https://www.rfc-editor.org/rfc/rfc9110.html#section-13.1.1), and [EF Core optimistic concurrency guidance](https://learn.microsoft.com/en-us/ef/core/saving/concurrency). These are pattern evidence, not selected dependencies.

## 8. Presentation sharing rule

Share business/domain contracts and deterministic processing, not UI framework state.

Do not share one Avalonia/Web ViewModel merely to increase reuse. Workstation and Web can share:

- operation inputs;
- query/read contracts where semantically identical;
- validation definitions;
- feature/permission/setting definitions;
- domain and decision logic.

They keep host-specific presentation state and navigation separate.

## 9. Repository naming and project boundaries

The repository category is `foundation/`, not `BuildingBlocks`, `Common`, or a universal `Shared` bucket.

A small capability may remain compact:

```text
modules/orders/
└── SquiFlow.Orders/
    ├── Domain/
    ├── Application/
    ├── Admission/
    ├── Queries/
    ├── Decisions/
    ├── Rules/
    ├── Contracts/
    └── Events/
```

If real cross-host reuse/provider isolation/dependency pressure earns a physical split, an explicit later shape may be:

```text
modules/orders/
├── SquiFlow.Orders.Domain/
├── SquiFlow.Orders.Contracts/
├── SquiFlow.Orders.Application/
├── SquiFlow.Orders.Postgres/
├── SquiFlow.Orders.Sqlite/
├── SquiFlow.Orders.Workstation/
├── SquiFlow.Orders.Web/
└── SquiFlow.Orders.Api/
```

This is not a mandate to create empty projects. Physical decomposition is earned by real compile-time/provider/platform/reuse/packaging pressure.

## 10. Worker and Scheduler ownership

Worker invokes the same authoritative capability modules; it does not own duplicate business services.

Scheduler owns when work should become durable, not the mutation:

```text
Scheduler
  -> durable occurrence/job
  -> Worker
  -> owning authoritative module
  -> PostgreSQL/object-storage effect
```

## 11. Reads and persistence ownership

Reads remain module-owned operations. API endpoints do not bypass module ownership with arbitrary SQL merely because they can reach PostgreSQL.

Optimized read-only projections are allowed where they preserve tenant/security/data ownership and do not become a second business authority.

Persistence/provider code lives behind the module/application boundary. PostgreSQL is authoritative server business state; SQLite/WAL is Workstation-local/provisional state; object storage owns large objects; caches are disposable; durable processing state has explicit ownership/recovery.

## 12. Mechanical enforcement

Architecture tests should enforce project boundaries that actually exist and keep Foundation/physically separated Capability Core code free of forbidden host/provider dependencies.

The intended dependency direction is:

```text
Foundation
   ↑
Capability business meaning / Capability Core
   ↑
Authoritative application + host/provider adapters
   ↑
Executable composition roots
```

Modular ownership is mandatory; physical project decomposition is earned.
