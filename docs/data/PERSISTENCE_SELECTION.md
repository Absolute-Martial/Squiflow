# Persistence Selection Policy

**Version:** v0.0.17

PostgreSQL is selected as the initial central transactional database, and SQLite with WAL is selected as the initial Workstation embedded database. Their Phase-3 and Phase-2 proofs are production-qualification gates rather than product-selection contests. Provider portability does not require a generic repository/unit-of-work hierarchy.

This supersedes the previous decision to keep both products open pending SQLite/libSQL and central-database comparisons. The decision changed because SquiFlow's authoritative business state is predominantly relational and constraint-heavy, while its variable data is bounded semi-structured configuration and its truly unstructured data belongs in object storage. Operating an additional database by default would add transaction, reconciliation, backup, tenancy, migration, and maintenance responsibilities without an established workload.

## 1. Central store — PostgreSQL qualification in Phase 3

The selected PostgreSQL implementation must prove the properties required by the actual sync/order slice:
- ACID transaction correctness and constraints;
- concurrency/isolation behavior;
- useful indexing/query performance;
- schema migration;
- idempotency receipt + business mutation/outbox atomicity where applicable;
- pooled tenant isolation;
- backup/restore/recovery behavior;
- mature .NET integration;
- bounded connections/resources on the actual lower-spec server class.

PostgreSQL is the selected initial central transactional product. Selection does not waive or predetermine successful completion of the Phase-3 proof.

Do not require future Worker/HA/reporting features to be solved before the Phase-3 slice unless the selected DB would make a known required path impossible.

## 2. Workload characterization comes before tuning

A database proof is invalid if it optimizes an undefined workload.

Before comparing/tuning candidates, record the implemented slice's representative profile:
- read/write/delete mix;
- typical and large row/document sizes;
- number of tenants and expected tenant/data skew;
- normal interactive concurrency;
- offline Workstation reconnect burst concurrency;
- sync/import/backfill write bursts;
- current hot queries and expected larger cardinalities;
- consistency/transaction requirements for each protected invariant;
- expected page/report/export sizes;
- initial HA/geographic assumptions (currently single-region/single-node first unless changed by evidence).

The same DB can behave very differently under a read-heavy Web demo and under a reconnecting Workstation backlog that writes many rows while maintaining indexes and WAL.

Measure the real slice first, then decide whether the problem is query shape, index choice, lock contention, connection pressure, storage I/O, WAL/checkpoint behavior, data-model shape, or something else.

## 3. No generic persistence abstraction baseline

Do not create:

```text
IRepository<T>
IUnitOfWork
IPersistenceProvider
one interface per DB implementation
persistence/abstractions project
```

merely for provider neutrality or mocking.

Instead:
- keep SQL/ORM/provider-specific code inside the infrastructure/data-access portion of the application;
- expose application/business operations in domain/use-case terms rather than leaking provider APIs upward;
- test provider-specific behavior against the real provider;
- extract a narrow interface/project only if an actual replacement, dual provider, plugin/process boundary or dependency-inversion problem requires it.

Containment is enough until migration is real.

## 4. Authoritative schema design

The authoritative relational model starts normalized around real business identities and relationships because write correctness, understandable constraints, and maintainable evolution matter more than optimizing one screen prematurely.

Examples of things that deserve explicit schema/constraints rather than generic blobs include:
- payments/refunds/allocations;
- inventory movements/adjustments;
- tenant membership/role metadata;
- issued documents/revisions;
- orders/quotations and their state/version;
- workflow/rule publication metadata.

Do not use JSON/EAV/arbitrary tenant-specific DDL as a shortcut for core relational invariants. Bounded custom fields/forms may use an extensibility representation, but that does not redefine the core business schema per tenant.

Denormalized/materialized read structures are allowed when a real query/report proves the value. Every such structure must declare:
- authoritative source;
- update/refresh mechanism;
- freshness expectation;
- rebuild/reconciliation path;
- tenant/authorization scope;
- behavior when the projection is stale or unavailable.

A denormalized projection is reconstructable state, not a second independent business authority.

## 5. Pooled multi-tenant baseline

Tenant-owned central data uses explicit tenant scope/discriminator.

Prove:
- authoritative `TenantContext` enters the data-access path from server membership/session resolution;
- tenant-owned reads and writes cannot casually cross tenants;
- tenant-local uniqueness/indexes include tenant scope where needed;
- list/report/export paths preserve tenant scope;
- connection reuse cannot carry another tenant's context;
- maintenance/backup/migration uses deliberate privileged identities.

This does not require a generic repository interface. Tenant scope can be enforced through concrete query/application/data-access code plus DB defense in depth.

### Selected PostgreSQL proof

The PostgreSQL implementation must:
- prove Row-Level Security on applicable tenant-owned tables;
- runtime role is not superuser/`BYPASSRLS`;
- table-owner/`FORCE ROW LEVEL SECURITY` behavior is deliberately handled;
- read and write policies are tested;
- any custom tenant setting used by RLS is transaction-local under connection pooling;
- measure connection-pool size against PostgreSQL backend-process/memory cost on the actual rack;
- measure WAL growth and checkpoint latency during reconnect/import bursts;
- observe autovacuum behavior under update/delete churn rather than assuming defaults are free;
- observe temp/sort spill and disk usage for representative reports/queries;
- bound/monitor archive/log/WAL growth so finite disk cannot be exhausted silently;
- measure crash/restart recovery time and verify backup/restore behavior with the chosen WAL/archive settings.

RLS is defense in depth, not a replacement for application authorization.

## 6. Consistency policy

Do not label the whole database/application as simply `strong` or `eventual`.

The authoritative transactional write model must provide the consistency required by the business invariant being committed. Temporary disagreement is not acceptable where it can create unsafe effects such as:
- duplicate/ambiguous payment authority;
- shared stock/credit decisions;
- tenant isolation;
- current sensitive authorization;
- unique issued-document/numbering truth;
- expected-version workflow/business transitions.

Eventual consistency is acceptable for explicitly derived/reconstructable state such as some reports/search projections/caches where stale-read behavior is visible/tolerated and the projection has a rebuild/reconciliation path.

For asynchronous projection updates, handle duplicate and out-of-order delivery through stable source versions/sequence/effect identity as appropriate to that projection. Do not let late derived data overwrite newer authoritative meaning.

### Concurrency, isolation, and locks

Concurrency control is selected per invariant rather than globally:

- expected-version/optimistic concurrency is the ordinary collaborative edit contract;
- unique/check/foreign-key constraints protect database-owned invariants;
- atomic conditional updates are preferred when they express the transition clearly;
- stronger transaction isolation or row/range/advisory locks are used only when the real invariant cannot be protected safely by a simpler mechanism;
- transactions stay short and do not wait on user interaction or avoidable external provider calls;
- code that takes multiple locks follows a deliberate stable ordering where applicable;
- deadlock/serialization/lock-timeout results are classified separately from permanent business conflicts.

When a provider reports a genuinely retryable deadlock/serialization conflict, retry the **whole transaction**, not an arbitrary fragment, with a bounded budget under the command's existing semantic-idempotency contract. Observe lock wait/deadlock/abort evidence during normal and reconnect/import burst tests.

### Compatible schema evolution

Database evolution must account for more than the currently deployed server binary. Supported old/new backend instances, skipped Workstations, pending local sync, durable jobs/messages, stored idempotency results, and rule/workflow/form snapshots may coexist.

Prefer an additive sequence where practical:

```text
expand compatible schema/contract
→ deploy readers/writers that understand the overlap
→ backfill/migrate with bounded observable work
→ switch authoritative reads/writes
→ verify old consumers/work are drained or explicitly unsupported
→ contract/remove obsolete shape
```

Before contraction, inventory the remaining readers/writers/data and document rollback versus roll-forward behavior. A backup is recovery evidence, not a substitute for a compatible migration. Destructive changes that cannot be rolled back safely require an explicit maintenance/recovery plan.

## 7. Indexing policy

Indexes are justified by a real query or invariant, not by column availability.

For each important index, know what it protects and measure its cost to:
- inserts/updates/deletes;
- offline Workstation backlog synchronization;
- imports/backfills;
- WAL/log/storage growth;
- migration/rebuild time;
- memory/cache pressure.

Tenant-scoped queries and tenant-local uniqueness commonly need tenant-aware index keys, but do not blindly prefix or index every field. Confirm index order/selectivity/cardinality with the real provider query plan.

Avoid `index every filterable field`. Periodically review unused/redundant indexes when the real workload exists.

Index selection should be tested against both normal small-tenant data and projected larger cardinalities so a design is not approved only because a 10K-row development database is fast.

## 8. Physical durability

Database `COMMIT` semantics do not by themselves prove recovery on the current rack.

Test the selected central/local stack for the relevant cases:
- process/OS restart;
- power-loss-equivalent recovery where practical;
- disk full/low space;
- database restart/recovery time;
- integrity/check/repair path;
- restore onto replacement hardware.

UPS, ECC, RAID/ZFS, enterprise SSDs etc. are deployment choices derived from real RPO/RTO/risk, not architecture defaults.

Owner: `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

## 9. Restore consistency

Restore correctness can involve:
- business records;
- idempotency receipts;
- outbox/job state once present;
- object metadata/bytes;
- rules/workflow/config needed to interpret state.

Example hostile case:

```text
business effect restored
idempotency receipt lost
→ late retry may duplicate effect
```

Restore qualification must consider this relationship rather than checking only `database starts`.

## 10. Connection/resource envelope

Measure the actual selected provider/driver rather than accepting framework defaults blindly:
- normal/max pool size;
- pool wait time;
- transaction/query duration under the implemented workload;
- memory/disk/WAL/temp growth;
- migration cost on actual hardware.

Connection pooling is an efficiency technique, not permission for unbounded connections. A pooled connection must not retain Tenant A's tenant/RLS context when reused for Tenant B.

Do not size pools/caches from available RAM alone.

## 11. Future dedicated tenant placement

Application/business code should not assume a physical DB filename/connection belongs permanently to every tenant, but do not implement per-tenant DB routing/pools before a real residency/compliance/SLO customer requires them.

Schema-per-tenant and DB-per-tenant are not baseline.

Owner: `docs/architecture/MULTI_TENANCY_ISOLATION.md`.

## 12. Workstation local store — SQLite qualification in Phase 2

A local candidate must prove the actual local-first requirements:
- atomic business + outbox transaction;
- crash/restart recovery;
- bounded resource use;
- schema migration;
- long-offline queue persistence;
- lock/contention behavior;
- disk-full behavior;
- corruption/recovery path;
- .NET/Windows packaging/integration.

SQLite with WAL is the selected initial Workstation store. Test the actual SQLite driver and configuration against the same local transaction, recovery, migration, resource, and failure cases that previously formed the candidate comparison.

libSQL is deferred and is not installed beside or inside SQLite. Reopen it only when a concrete requirement benefits from a libSQL-specific capability such as embedded replication or remote access, and only when the selected .NET/Windows integration, offline write/consistency behavior, recovery, packaging, and migration path pass SquiFlow's proof.

Server and Workstation may use different DB products without requiring a shared persistence interface.

## 13. Dedicated NoSQL and unstructured data

A dedicated server or Workstation NoSQL database is not part of the initial foundation.

Bounded form, workflow, rule, module-setting, and custom-field definitions may use explicit versioned JSON/document representations where that is their natural shape. On the server, PostgreSQL JSONB is the initial mechanism when relational columns are not the natural representation; on Workstation, use an explicit SQLite representation. This flexibility must not move payments, stock movements, tenant membership, issued-document truth, idempotency, or other protected invariants into opaque arbitrary documents.

A specialized document, search, graph, vector, time-series, or key-value store is introduced only when a named implemented workload proves material value beyond PostgreSQL/SQLite. That decision must define:

- authoritative versus derived ownership;
- transaction and concurrency boundaries;
- consistency and freshness;
- tenant isolation and authorization;
- outbox/reconciliation and rebuild behavior;
- backup, restore, migration, and deletion;
- .NET support, deployment resources, and operational ownership.

Do not create a distributed unit of work across PostgreSQL and another store. When PostgreSQL owns the business fact, commit the fact and outbox atomically there, then update a secondary store through idempotent, replayable Worker processing.

Artwork, PDFs, images, scans, and other large unstructured objects remain behind `IObjectStore`. PostgreSQL/SQLite retain structured metadata such as ownership, object key, size/hash, lifecycle state, and business linkage.

## 14. Qualification evidence

Record:
- exact product/driver/version/config;
- hardware/OS;
- workload profile from section 2;
- workload/data size and projected cardinality;
- representative query plans and index choices;
- write/index overhead under sync/import-style bursts;
- transaction/concurrency/isolation results;
- provider-specific maintenance behavior (including WAL/checkpoint/vacuum/temp behavior when applicable);
- resource measurements;
- backup/recovery evidence;
- known limitations;
- migration/exit implications.

If a selected product fails a material gate, record the evidence and create an explicit superseding decision. Do not silently introduce a second store or swap providers merely to preserve theoretical optionality.
