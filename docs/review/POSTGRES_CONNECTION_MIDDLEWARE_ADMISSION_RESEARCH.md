# PostgreSQL Connection Middleware Admission Research

**Version:** v0.1.0
**Reviewed:** 2026-09-23
**Decision owner:** `docs/data/PERSISTENCE_SELECTION.md`
**Status:** Direct Npgsql pooling is the current runtime baseline. External PostgreSQL middleware is `NOT_INTRODUCED`.

## 1. Decision

CoreApi currently uses one process-wide, named `NpgsqlDataSource`. IdentityAccess, Tenancy and Orders DbContexts share that data source and therefore share one bounded driver pool for the exact runtime connection configuration. The checked-in values are conservative starting values, not a measured production capacity claim:

```text
ConnectionMode                    Direct
MaximumPoolSize                  20 per CoreApi process
MinimumPoolSize                  0
ConnectionIdleLifetimeSeconds    300
ConnectionPruningIntervalSeconds 10
ConnectionLifetimeSeconds        3600
```

The host rejects disabled pooling, `No Reset On Close=true`, Npgsql multiplexing, and every connection mode other than `Direct`. It names the data source `Application.CoreApi.PrimaryDatabase` so Npgsql's built-in `System.Diagnostics.Metrics` instruments have a stable non-secret pool identifier. Query parameter logging remains disabled.

No PgBouncer, PgDoorman, Odyssey, PgCat, PgDog, or Pgpool-II process is part of the current deployment. Adding one now would create another network, authentication, TLS, upgrade, health, saturation, failover, diagnostics, and recovery boundary without evidence that direct bounded pools exceed PostgreSQL's connection envelope.

When an external transaction pool becomes necessary, qualify **PgBouncer and PgDoorman against the same Npgsql/EF/PostgreSQL workload**. Do not select the winner from project benchmarks alone. PgBouncer is the conservative ecosystem reference; PgDoorman is the strongest current Npgsql-focused challenger because it explicitly supports Npgsql's unnamed prepared-statement behavior, a shared multithreaded pool, database-wide connection caps, and built-in metrics.

PgCat, PgDog, and Pgpool-II are not the first candidates for connection pressure alone. Their routing, sharding, replica, or HA behavior is admitted only when SquiFlow owns that larger topology. Odyssey remains a credible transaction-pool alternative, but the current workload provides no differentiator that earns it ahead of the two-candidate proof.

## 2. The SquiFlow workload being decided

The current facts are narrow:

- CoreApi is the only runtime database client process.
- DbMigrator is a separate one-shot privileged process.
- PostgreSQL is a single-primary central authority at the current baseline.
- IdentityAccess and Tenancy perform small, indexed security/control reads; Orders adds bounded draft create/read transactions and a keyset-paginated header browse.
- The narrow Orders schema is tenant-owned and uses forced RLS. There is no Worker, Sync API, read replica, sharding topology, or HA manager yet.
- Tenant-owned pooled tables use application tenant scope plus PostgreSQL RLS defense in depth.
- The Orders RLS setting is transaction-local in the same explicit transaction as protected operations, with real pool-reset tests.
- Runtime and migration identities remain separate.

The future pressure is also known: CoreApi replicas, Worker concurrency, Web/API bursts, and offline Workstation reconnects can multiply client concurrency. That is an admission trigger for measurement, not proof that a proxy is already necessary.

## 3. Candidate comparison

| Candidate | What it actually contributes | Fit for the present baseline | Main qualification risk | Decision |
|---|---|---|---|---|
| Direct Npgsql `NpgsqlDataSource` | In-process pooling, reset-on-reuse, connection lifetime/pruning, standard .NET metrics, no extra hop | Strongest: one process, one primary and low current query breadth | Each process owns a pool; aggregate connections grow with replica/process count | **NOW** |
| PgBouncer | Focused external session/transaction/statement pooling; protocol-level named prepared-statement tracking in transaction mode | Strong future reference when aggregate clients materially exceed safe PostgreSQL backends | Transaction mode invalidates session assumptions; Npgsql reset/prepared behavior and DDL cache invalidation need exact configuration | **QUALIFY ON TRIGGER** |
| PgDoorman | Multithreaded shared transaction/session pools, explicit Npgsql unnamed-statement support, database-level cap/coordinator, metrics and graceful upgrade features | Strong future challenger for .NET OLTP pressure | Smaller public adoption surface than PgBouncer; its performance and failure claims still need independent SquiFlow evidence | **QUALIFY ON TRIGGER** |
| Odyssey | Multithreaded transaction/session pooling, per database/user limits, cancellation and abandoned-transaction rollback | Technically credible, but no current differentiating requirement | Another operational stack with less direct SquiFlow/Npgsql evidence | **DEFER** |
| PgCat | Transaction/session pooling plus replica routing, sharding, mirroring and query parsing | Excess breadth for one primary; useful only when routing/sharding is earned | SQL parsing/routing correctness, session feature limits, and a larger failure surface | **DO NOT ADMIT FOR POOLING ALONE** |
| PgDog | Pooling plus a rapidly evolving distributed query/sharding engine | No current sharding workload | Cross-shard SQL support is deliberately partial and the project remains pre-1.0 | **DEFER UNTIL SHARDING EXISTS** |
| Pgpool-II | Connection reuse plus read load balancing, failover, online recovery and Watchdog HA | Too broad for present pooling need | It becomes part of database HA/routing authority and adds configuration/state/quorum complexity | **DO NOT ADMIT FOR POOLING ALONE** |

## 4. Why direct Npgsql is the correct current baseline

Npgsql recommends constructing one data source and reusing it throughout the application. A data source is thread-safe and normally corresponds to a driver connection pool. Disposing a logical connection returns its physical connection to that pool, and Npgsql resets pooled state before reuse by default.

That already removes PostgreSQL handshake cost inside one CoreApi process. A proxy cannot make the current two small query paths more correct. It would move pooling to another process and may reduce aggregate PostgreSQL backends only after there are enough application processes or client sessions for multiplexing transactions to matter.

The new host configuration fixes a real current issue: relying on Npgsql's default maximum of 100 connections would let each future replica independently reserve an unsuitable envelope. The explicit `20` is a replaceable starting bound. Production qualification still has to measure pool wait, query time, backend memory/process cost, reconnect bursts, WAL/checkpoints, and the total of all runtime and operational reserves on the actual rack.

## 5. Pooler-specific findings

### PgBouncer

PgBouncer's transaction mode returns a server connection after each transaction. Its current documentation describes protocol-level prepared-statement tracking in transaction/statement modes when `max_prepared_statements` is nonzero. Npgsql's own compatibility guide says that many PgBouncer deployments should disable the Npgsql pool; if both layers remain active with transaction/statement pooling, Npgsql requires `No Reset On Close=true` because its normal `DISCARD ALL` reset has no stable session behind the proxy.

SquiFlow must not copy that switch into direct mode. For a future PgBouncer proof, the safe first configuration to test is:

```text
CoreApi Npgsql pooling disabled
→ PgBouncer transaction pool
→ PostgreSQL
```

Keeping a small Npgsql pool in front of PgBouncer is a separate measured variant. It cannot be enabled merely to reduce client-side opens because it creates two wait queues and changes reset behavior.

### PgDoorman

PgDoorman supports transaction and session modes and documents `SET LOCAL` as compatible with transaction mode. Its project documentation explicitly names .NET/Npgsql, remaps unnamed prepared statements into a shared backend cache, supplies database-level connection caps, and exports pool metrics. These are directly relevant to a future multi-process CoreApi/Worker workload.

Those are promising properties, not production evidence for SquiFlow. The public ecosystem is newer and smaller than PgBouncer's, so binary/package provenance, upgrade/rollback, malformed-protocol behavior, cancellation, schema-change cache invalidation, saturation fairness, and failure under PostgreSQL restart must be tested locally.

### Odyssey

Odyssey is a production-used multithreaded pooler with transaction tracking, cancellation, abandoned-transaction rollback, and per database/user limits. It is a credible fallback when its operational model or measured behavior materially beats both qualified candidates. No current requirement identifies that advantage, so operating three pooler POCs would add breadth without improving the decision.

### PgCat

PgCat's value is larger than pooling: it includes read routing, replicas, failover handling, mirroring, and sharding. Its transaction mode requires transaction-scoped alternatives such as `SET LOCAL` and transaction advisory locks instead of session `SET` or session advisory locks. Query-parser routing also makes the proxy participate in SQL semantics.

SquiFlow has one selected primary and no read-replica consistency contract or shard key. Enabling parser-driven routing now would create correctness questions that do not exist with a direct primary connection.

### PgDog

PgDog combines pooling with load balancing and database sharding. The project documents partial or unsupported forms for some cross-shard queries. Its rapid current development is useful evidence of activity, but SquiFlow has neither a shard topology nor a distributed-query contract. It should be reconsidered only with a real shard key, cross-shard operation inventory, rebalance/recovery plan, and provider test corpus.

### Pgpool-II

Pgpool-II deliberately combines connection pooling with read load balancing, automatic failover, online recovery, and Watchdog-based HA. Those features can be appropriate when one team explicitly assigns database-cluster routing and failover authority to Pgpool-II. They are not free additions to a connection pool.

SquiFlow currently has not selected a PostgreSQL HA topology. Introducing Pgpool-II would silently decide part of that open architecture and create a second HA state machine before the database recovery proof exists.

## 6. Mandatory external-pooler qualification gate

An external pooler is earned when representative evidence shows at least one of these:

- the safe PostgreSQL backend limit cannot serve the aggregate bounded pools of the required CoreApi/Worker replica count;
- backend connection memory/process cost materially limits the supported workload;
- connect/reconnect storms violate an accepted recovery or latency objective;
- a managed deployment already provides a compatible transaction pool and bypassing it is operationally worse.

Before changing `Database:ConnectionMode`, the candidate must pass all of the following.

### Protocol and EF/Npgsql compatibility

- EF Core queries, commands, explicit transactions, cancellation, timeouts and retryable transaction strategy;
- Npgsql extended protocol, unnamed/named prepared statements, auto-prepare if later enabled, and batches/COPY only where the product uses them;
- schema migration followed by safe pool reconnect/cache invalidation;
- no reliance on cross-transaction temporary tables, session advisory locks, `LISTEN`, held cursors, session `SET`, or session-local identity;
- deterministic behavior on PostgreSQL/pooler restart, network break and cancellation race.

### Tenant/RLS isolation

For each protected transaction:

```sql
BEGIN;
SET LOCAL application.tenant_id = '<validated tenant>';
-- tenant-scoped reads/writes
COMMIT;
```

The real proxy/provider test must force backend reuse and prove:

1. Tenant A can access only Tenant A rows.
2. Tenant B immediately reusing the backend cannot see Tenant A rows.
3. Missing tenant context fails closed.
4. Read and write policies both reject cross-tenant access.
5. Rollback, cancellation, timeout, retry and broken-client paths clear transaction state.
6. Runtime credentials cannot bypass RLS and do not own protected tables unless `FORCE ROW LEVEL SECURITY` behavior was deliberately qualified.

### Operational boundary

- runtime traffic uses the pooler endpoint; DbMigrator, backup, restore, replication, administrative maintenance and pooler health probes use deliberately separate/direct paths as appropriate;
- client-to-pooler and pooler-to-PostgreSQL authentication/TLS are both configured and rotated;
- the pooler cannot map a least-privilege runtime identity to a more privileged backend identity accidentally;
- rollout, drain, binary/config upgrade, rollback and configuration reload are rehearsed;
- CoreApi liveness remains process-only while readiness distinguishes pooler unavailable, PostgreSQL unavailable, saturation and authentication/configuration failure;
- pooler failure is treated as a dependency outage, not silently bypassed through an ungoverned direct connection.

### Capacity and fairness

Measure at least:

- client connections, active/idle backend connections and hard database cap;
- queue depth and checkout wait percentiles;
- transaction/query duration and timeout/cancellation counts;
- connection creation, reset, eviction and failure rates;
- CoreApi versus Worker allocation/reserve under a noisy tenant and reconnect burst;
- PostgreSQL CPU, memory, locks, temp spill, WAL/checkpoint and recovery behavior;
- latency/throughput difference against the direct `NpgsqlDataSource` control.

A lower median benchmark is insufficient if tail latency, ambiguous failure, tenant isolation, operational recovery, or privileged-path separation regresses.

## 7. What middleware does not solve

None of these products independently provides:

- authenticated `TenantContext`;
- application authorization;
- PostgreSQL RLS policy correctness;
- tenant fair-share scheduling or durable quotas;
- query/index/transaction design;
- Worker durability or idempotency;
- PostgreSQL backup, restore, WAL or autovacuum governance;
- safe per-tenant database placement;
- transparent exactly-once or zero-downtime behavior.

A connection pool protects a finite database connection resource. Noisy-neighbor control still needs bounded requests, transactions, Worker admission, per-tenant quotas/fairness where earned, and measured database workload behavior.

## 8. Primary sources

- [Npgsql basic usage and data-source pooling](https://www.npgsql.org/doc/basic-usage.html)
- [Npgsql connection-string and pool controls](https://www.npgsql.org/doc/connection-string-parameters)
- [Npgsql pooled-state reset](https://www.npgsql.org/doc/performance.html)
- [Npgsql metrics](https://www.npgsql.org/doc/diagnostics/metrics.html)
- [Npgsql PgBouncer compatibility notes](https://www.npgsql.org/doc/compatibility.html#pgbouncer)
- [PgBouncer configuration and pooling modes](https://www.pgbouncer.org/config)
- [PgBouncer prepared-statement FAQ](https://www.pgbouncer.org/faq.html#how-to-use-prepared-statements-with-transaction-pooling)
- [PgDoorman pool modes](https://ozontech.github.io/pg_doorman/concepts/pool-modes.html)
- [PgDoorman pool configuration](https://ozontech.github.io/pg_doorman/reference/pool.html)
- [PgDoorman project](https://github.com/ozontech/pg_doorman)
- [Odyssey project and design goals](https://github.com/yandex/odyssey)
- [PgCat project and mode/routing documentation](https://github.com/postgresml/pgcat)
- [PgDog project and sharding support](https://github.com/pgdogdev/pgdog)
- [Pgpool-II feature overview](https://www.pgpool.net/docs/latest/en/html/intro-whatis.html)
