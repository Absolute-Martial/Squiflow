# Persistence Selection Policy

**Version:** v0.0.15

The persistence products remain OPEN decisions, but an implementation phase that needs persistence must select and prove a real adapter for that slice.

## 1. Central store requirements

A central candidate must prove:
- ACID transaction correctness and constraints;
- concurrency/isolation behavior under SquiFlow workloads;
- indexing/query performance;
- migrations/expand-contract compatibility;
- idempotency receipt + business mutation/outbox atomicity where applicable;
- durable Worker claim/lease/fencing primitives;
- pooled tenant isolation;
- backup/restore and recovery behavior;
- mature .NET integration;
- observability;
- bounded connection/resource use on the actual lower-spec deployment class;
- a credible later HA/read-scale path without requiring it now.

PostgreSQL is the strongest current central reference candidate, not an implicit final decision.

## 2. Pooled multi-tenant baseline

The ordinary v0.0.15 central data model is designed for pooled storage with an explicit `TenantId`/equivalent discriminator on tenant-owned authoritative records.

The selected provider must prove that tenant isolation does not depend only on every developer remembering a `WHERE TenantId = ...` clause.

Required proof includes:
- typed authoritative `TenantContext` from the application boundary;
- tenant-scoped repositories/query contracts;
- read **and write** isolation;
- tenant-aware uniqueness/indexing where uniqueness is tenant-local;
- cross-tenant negative tests for reads, writes, reports, search, jobs, diagnostics and migrations;
- deliberate privileged identities for maintenance/backup/migration paths;
- safe behavior under connection pooling.

### PostgreSQL reference proof

If PostgreSQL remains the central reference candidate, its adapter/POC must also prove Row-Level Security for pooled tenant-owned tables.

The runtime role must not be superuser or `BYPASSRLS`. Table-owner bypass/`FORCE ROW LEVEL SECURITY` behavior must be deliberately handled. `USING`/`WITH CHECK` coverage must protect applicable reads and writes.

If a custom PostgreSQL setting carries tenant context, it must be transaction-local so a reused pooled connection cannot retain the previous tenant's scope.

RLS is defense in depth alongside SquiFlow tenant/resource authorization, not a replacement for application authorization.

## 3. Physical durability is deployment evidence, not a database slogan

`COMMIT`/`fsync` semantics from a database do not by themselves prove the accepted recovery behavior of the current desktop-class rack hardware.

The chosen central adapter/deployment must be qualified for:
- abrupt process/OS restart;
- power-loss-equivalent recovery appropriate to the available hardware;
- disk full/low free space;
- database restart/recovery time;
- integrity/check/repair path;
- restore onto replacement hardware;
- behavior when storage is slower than expected and connection/Worker queues back up.

UPS, enterprise SSD power-loss protection, ECC memory, RAID/ZFS or similar features are **not silently assumed**. Their need follows the accepted RPO/RTO and actual hardware risk/budget.

See `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

## 4. Restore consistency extends beyond the database

A restore can be unsafe even if the SQL database itself restores cleanly.

Qualification must consider the relationship between:
- business records;
- idempotency receipts;
- outbox/job state;
- object metadata;
- durable object bytes;
- versioned rules/workflows/config;
- security/audit state needed for correctness.

Important hostile case:

```text
business effect restored
idempotency receipt not restored
→ late retry could attempt duplicate effect
```

Restore verification must prove the selected recovery point does not accidentally recreate completed external/business effects or silently orphan required objects.

## 5. Connection/resource envelope

The selected central adapter must expose measurable limits for:
- max/normal connection-pool size;
- pool wait time;
- statement/transaction timeout policy where safe;
- long-running report/query isolation;
- Worker claim/batch size;
- migration resource behavior;
- memory/disk/WAL/temp growth.

Pool sizes are derived from the actual server/database capacity, not from framework defaults or available RAM alone.

## 6. Future placement profiles

The persistence abstraction must not hard-code the assumption that every tenant will forever share one physical database.

A future tenant may be routed to dedicated storage because of residency, compliance, contractual isolation, enterprise scale or customer-managed deployment requirements.

Do not implement per-tenant database routing/pools before a real dedicated-data requirement exists. The current requirement is only to avoid domain/application contracts that make later placement impossible.

Schema-per-tenant is not baseline because it increases migration/operational complexity while still sharing the DB process, and SquiFlow expects tenant variation to be expressed through configuration/rules/forms/workflows rather than tenant-specific table definitions.

See `docs/architecture/MULTI_TENANCY_ISOLATION.md`.

## 7. Local Workstation store requirements

A local candidate must prove:
- atomic business + outbox transaction;
- crash/power-loss/restart recovery;
- bounded resource use;
- schema migration across skipped releases;
- backup/recovery/export;
- long-offline behavior;
- corruption detection/repair or safe recovery path;
- file/object reference durability;
- lock/contention behavior;
- disk-full/low-space behavior;
- .NET/Windows integration and packaging.

SQLite + WAL is the mature reference candidate. libSQL is an explicit candidate and must be tested under the same SquiFlow workload/failure matrix.

Do not select libSQL merely because it is newer, and do not select SQLite merely because it is familiar.

Server and Workstation may choose different database products.

## 8. Selection evidence

A provider decision is accepted only when its POC records:
- exact product/driver/version/config;
- exact hardware/OS used;
- test workload and data size;
- failure/concurrency/isolation results;
- resource measurements;
- backup/restore evidence;
- known limitations/workarounds;
- exit/migration implications.

A reference-project directory or architecture preference is not proof of selection.
