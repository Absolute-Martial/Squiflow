# Phase 3A — PostgreSQL Authority and Tenant Isolation

## Required persistence foundation

Qualify the selected PostgreSQL/.NET data-access implementation with the real Customer/Order workload:

- explicit normalized authoritative schema;
- transaction boundaries;
- connection pooling/resource cost;
- TenantId scoping;
- least-privilege runtime DB identity;
- RLS defense-in-depth where selected;
- pool/session-context hygiene across tenants;
- constraints/indexes tied to named queries/invariants;
- migrations and restart/recovery;
- WAL/checkpoint/autovacuum/temp/disk behavior on the target deployment class.

## No generic repository mandate

Persistence adapters are capability/application owned. Do not create `IRepository<T>`/universal UoW merely because PostgreSQL is introduced.

## Workload profile

Record representative read/write mix, row/item sizes, tenant/data skew, normal concurrency, reconnect/import bursts, hot queries, consistency needs, and current HA assumptions before tuning/indexing.

## Exit gate

PostgreSQL is proven as central authority for the first slice, cross-tenant access remains blocked even when another security layer is wrong, and selected indexes/transactions have measured rationale.