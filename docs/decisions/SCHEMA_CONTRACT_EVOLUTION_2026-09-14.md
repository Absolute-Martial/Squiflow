# Schema and Contract Evolution Decision — 2026-09-14

**Status:** Accepted refinement  
**Version:** v0.0.20  
**Base:** `dd0ffd53e872fa9f3ab744e9067acf7f960c792c`  
**Current owner:** `docs/architecture/SCHEMA_AND_CONTRACT_EVOLUTION.md`

## 1. Audit result

The new proposal does not replace SquiFlow's existing migration/compatibility policy. It strengthens and organizes decisions that were already partly present across persistence, API, deployment, sync, Workstation, NFR, and review documents.

Already accepted before this refinement:

- supported old/new backend processes may overlap;
- Workstations may skip releases;
- pending local sync and durable jobs/messages may outlive a server deployment;
- database evolution uses additive `expand -> migrate/backfill -> switch -> contract` behavior where practical;
- destructive contraction requires reader/writer/data inventory plus drain/compatibility and rollback/roll-forward evidence;
- API/sync compatibility is mandatory and unsupported versions fail explicitly;
- product SemVer does not equal DB/API/sync/message compatibility;
- canary/blue-green/rolling releases require compatible data/contracts rather than process-health checks alone;
- a separately deployed schema-registry platform was deferred until a real independently versioned producer/consumer ecosystem justifies it.

This refinement preserves those decisions.

## 2. Newly explicit refinements

### 2.1 Version overlap is a named architectural state

SquiFlow now names the transition explicitly:

```text
Expand
-> Overlap / Transition
-> Migrate / Backfill
-> Cutover
-> Contract
```

`Overlap` is not an accidental temporary condition. It is a supported compatibility window with known readers, writers, stored data, durable work, and retirement criteria.

### 2.2 Compatibility domains are separate

SquiFlow does not have one universal `SchemaVersion`.

The following evolve independently where applicable:

- product/release version;
- PostgreSQL migration/schema version;
- Workstation SQLite schema version;
- domain/public contract version;
- REST API version;
- sync protocol version;
- sync operation/envelope version;
- gRPC/Protobuf version;
- durable job/message/event version;
- Guard/Workstation IPC version;
- rule/workflow/form/configuration snapshot version;
- authorization-model revision.

A change in one does not automatically increment or prove compatibility in another.

### 2.3 Compatibility is directional

Compatibility decisions distinguish backward, forward, full, and incompatible paths according to the actual contract family.

There is no universal table that can safely decide SQL, JSON/HTTP, Protobuf, durable messages, SQLite, and future GraphQL compatibility identically.

### 2.4 Source-controlled compatibility registry is baseline governance

SquiFlow accepts a logical compatibility/schema registry now in the form of source-controlled contract metadata, generated descriptors where useful, compatibility fixtures/tests, and boundary-specific runtime tables/code where needed.

Typical metadata includes:

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
TransformerId
MigrationId
```

This is not a new business authority.

### 2.5 A deployable schema-registry service is still deferred

The earlier decision that a schema registry is a later scale trigger is preserved for a **separately deployed/runtime registry product or service**.

Create such a service only when independently deployed/versioned producers and consumers, external integration teams, or scale make centralized machine-enforced registration materially safer than source-controlled governance.

Do not add a mandatory registry network hop to ordinary modular-monolith application execution.

### 2.6 Resolver/normalizer is a responsibility, not automatically a service

At a versioned boundary, compatibility resolution determines whether an incoming/stored contract is:

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

Resolution then maps a supported old representation into the current application command/query/value model.

This responsibility can be implemented locally in REST adapters, SyncApi, Worker durable-job readers, Workstation migration code, or other owning boundaries. There is no requirement for one global network `SchemaResolver` service.

Resolution never replaces authentication, authorization, tenant isolation, idempotency, concurrency, domain invariants, or current server authority.

### 2.7 Compatibility matrices and historical fixtures become explicit CI/release evidence

Every long-lived contract family maintains an appropriate producer/consumer/version matrix and historical fixtures for the versions SquiFlow still promises to support.

The first real compatibility slice must exercise as applicable:

- old reader + new writer;
- new reader + old writer;
- old Workstation + new server;
- rolling old/new backend processes;
- non-empty historical data;
- pending local intent created before upgrade;
- durable jobs/messages created before upgrade and executed after upgrade;
- interrupted backfill/resume;
- explicit unsupported-version behavior without partial durable mutation;
- rollback/roll-forward after schema expansion/cutover;
- contraction blocked while known old consumers remain.

### 2.8 Workstation compatibility preserves pending intent

An incompatible Workstation operation must not be silently dropped or reinterpreted.

Where possible, preserve the pending local intent/evidence and return an explicit upgrade, resnapshot, rebase, or reconciliation path.

The synchronization transport remains independent of this rule. HTTP or gRPC may carry the same compatibility semantics.

### 2.9 Database migration, contract transformation, and domain transition are different operations

```text
Database migration
= changes persisted state representation

Contract transformation
= translates a supported old wire/durable representation into the current application contract

Domain transition
= executes current business behavior under current authority/invariants
```

Do not treat one as proof or implementation of the others.

### 2.10 Contract retirement is evidence-driven

Before removing an old contract/schema shape, inventory as applicable:

- supported Workstation releases;
- old/new backend deployments;
- pending sync;
- durable jobs/messages/events;
- stored idempotency results;
- rule/workflow/form/config snapshots;
- external integrations;
- restore artifacts capable of reintroducing old state;
- migration/backfill completeness;
- old read/write paths.

Then explicitly drain, migrate, transform, resnapshot, require upgrade, quarantine/reconcile, use a maintenance cutover, or retain compatibility longer.

## 3. What this decision does not introduce

This refinement does **not** newly select or require:

- a standalone schema-registry server/container;
- a central resolver network service;
- Kafka or another broker;
- event sourcing;
- GraphQL;
- gRPC as the final Sync transport;
- one universal compatibility algorithm;
- one global schema version;
- automatic dual-write for every migration;
- zero-downtime releases on the current single-node rack;
- an indefinite compatibility promise for every historical version.

## 4. Still open

The following remain implementation/operational decisions:

- exact REST API version transport (URI/header/media type/narrow combination);
- final Workstation Sync transport;
- exact supported Workstation/server overlap window;
- exact deprecation/retirement duration for each contract family;
- exact schema/contract generation and compatibility-test tooling;
- hand-written versus generated transformers for each boundary;
- whether a future independent producer/consumer ecosystem earns a runtime schema-registry product/service.

## 5. Supersession clarification

Earlier review wording such as `schema registry later / scale trigger` is **not** superseded when it refers to a standalone registry service/product.

It is clarified as follows:

> Source-controlled contract registry metadata, compatibility matrices, historical fixtures, and boundary-specific resolver behavior are accepted baseline governance. A separately deployed schema-registry service remains deferred until an independent producer/consumer ecosystem makes it worthwhile.

## 6. Architectural invariant

> SquiFlow treats schema and long-lived contract evolution as a release/sync/data-correctness concern. Supported versions may coexist; compatibility is directional and contract-family-specific; migration uses expand-overlap-migrate-cutover-contract; transformations are explicit; retirement is evidence-driven; and infrastructure grows only when the real producer/consumer topology earns it.
