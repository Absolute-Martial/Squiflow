# Persistence Selection Policy

**Version:** v0.0.15

The persistence products remain OPEN decisions.

## Central store requirements

Must prove ACID correctness, constraints, concurrency control, indexing/query performance, migrations/expand-contract, backup/restore and DR as required, mature .NET integration, tenant isolation, observability, constrained-node viability and a future HA/read-scale path.

PostgreSQL is the strongest current central reference candidate, not an implicit final decision.

## Pooled multi-tenant baseline

The ordinary v0.0.15 central data model is designed for pooled storage with an explicit `TenantId`/equivalent discriminator on tenant-owned authoritative records.

The selected provider must prove that tenant isolation does not depend only on every developer remembering a `WHERE TenantId = ...` clause.

Required proof includes:
- typed authoritative TenantContext propagated from the application boundary;
- tenant-scoped repositories/query contracts;
- write-side as well as read-side isolation;
- tenant-aware uniqueness/indexing where uniqueness is tenant-local;
- cross-tenant negative tests for reads, writes, reports, search, jobs and migrations;
- deliberate privileged identities for maintenance/backup/migration paths;
- safe behavior under connection pooling.

### PostgreSQL reference proof

If PostgreSQL remains the central reference candidate, its adapter/POC must also prove Row-Level Security for pooled tenant-owned tables.

The runtime role must not be superuser or `BYPASSRLS`. Table-owner bypass/`FORCE ROW LEVEL SECURITY` behavior must be deliberately handled. `USING`/`WITH CHECK` coverage must protect applicable reads and writes.

If a custom PostgreSQL setting carries tenant context, it must be transaction-local so a reused pooled connection cannot retain a previous tenant's scope.

RLS is defense in depth alongside SquiFlow tenant/resource authorization, not a replacement for application authorization.

## Future placement profiles

The persistence abstraction must not hard-code the assumption that every tenant will forever share one physical database.

A future tenant may be routed to dedicated storage because of residency, compliance, contractual isolation, enterprise scale or customer-managed deployment requirements.

Do not implement per-tenant database routing/pools before a real dedicated-data requirement exists. The current requirement is only to avoid domain/application contracts that make later placement impossible.

Schema-per-tenant is not a baseline target because it increases migration/operational complexity while still sharing the database process and SquiFlow currently expects tenant variation to be expressed through configuration/rules/forms/workflows rather than tenant-specific table definitions.

See `docs/architecture/MULTI_TENANCY_ISOLATION.md`.

## Local Workstation store requirements

Must prove atomic business + outbox writes, crash/power-loss recovery, bounded resource use, schema migration, backup/recovery, long-offline behavior, corruption handling, file/object references and .NET/Windows integration.

SQLite + WAL is the mature reference candidate. libSQL is an explicit candidate and must be evaluated with the same workload/failure tests.

Server and Workstation may choose different database products.
