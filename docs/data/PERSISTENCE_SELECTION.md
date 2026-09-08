# Persistence Selection Policy

**Version:** v0.0.15

The persistence products remain OPEN decisions.

## Central store requirements

Must prove ACID correctness, constraints, concurrency control, indexing/query performance, migrations/expand-contract, backup/restore and DR as required, mature .NET integration, tenant isolation, observability, constrained-node viability and a future HA/read-scale path.

PostgreSQL is the strongest current central reference candidate, not an implicit final decision.

## Local Workstation store requirements

Must prove atomic business + outbox writes, crash/power-loss recovery, bounded resource use, schema migration, backup/recovery, long-offline behavior, corruption handling, file/object references and .NET/Windows integration.

SQLite + WAL is the mature reference candidate. libSQL is an explicit candidate and must be evaluated with the same workload/failure tests.

Server and Workstation may choose different database products.
