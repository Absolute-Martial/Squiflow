# Persistence Selection Policy

**Version:** v0.0.15

Database products remain OPEN until the implementation phase that needs them proves a real candidate. Provider portability does not require a generic repository/unit-of-work hierarchy.

## 1. Central store — Phase 3 selection

The first central DB candidate must prove the properties required by the actual sync/order slice:
- ACID transaction correctness and constraints;
- concurrency/isolation behavior;
- useful indexing/query performance;
- schema migration;
- idempotency receipt + business mutation/outbox atomicity where applicable;
- pooled tenant isolation;
- backup/restore/recovery behavior;
- mature .NET integration;
- bounded connections/resources on the actual lower-spec server class.

PostgreSQL is the strongest current central reference candidate, not an implicit final decision.

Do not require future Worker/HA/reporting features to be solved before the Phase-3 slice unless the selected DB would make a known required path impossible.

## 2. No generic persistence abstraction baseline

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

## 3. Pooled multi-tenant baseline

Tenant-owned central data uses explicit tenant scope/discriminator.

Prove:
- authoritative `TenantContext` enters the data-access path from server membership/session resolution;
- tenant-owned reads and writes cannot casually cross tenants;
- tenant-local uniqueness/indexes include tenant scope where needed;
- list/report/export paths preserve tenant scope;
- connection reuse cannot carry another tenant's context;
- maintenance/backup/migration uses deliberate privileged identities.

This does not require a generic repository interface. Tenant scope can be enforced through concrete query/application/data-access code plus DB defense in depth.

### PostgreSQL reference proof

If PostgreSQL is used:
- prove Row-Level Security on applicable tenant-owned tables;
- runtime role is not superuser/`BYPASSRLS`;
- table-owner/`FORCE ROW LEVEL SECURITY` behavior is deliberately handled;
- read and write policies are tested;
- any custom tenant setting used by RLS is transaction-local under connection pooling.

RLS is defense in depth, not a replacement for application authorization.

## 4. Physical durability

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

## 5. Restore consistency

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

## 6. Connection/resource envelope

Measure the actual selected provider/driver rather than accepting framework defaults blindly:
- normal/max pool size;
- pool wait time;
- transaction/query duration under the implemented workload;
- memory/disk/WAL/temp growth;
- migration cost on actual hardware.

Do not size pools/caches from available RAM alone.

## 7. Future dedicated tenant placement

Application/business code should not assume a physical DB filename/connection belongs permanently to every tenant, but do not implement per-tenant DB routing/pools before a real residency/compliance/SLO customer requires them.

Schema-per-tenant and DB-per-tenant are not baseline.

Owner: `docs/architecture/MULTI_TENANCY_ISOLATION.md`.

## 8. Workstation local store — Phase 2 selection

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

SQLite + WAL is the mature reference candidate. libSQL is an explicit candidate. Test both against the same small SquiFlow workload/failure cases needed to make the decision.

Server and Workstation may use different DB products without requiring a shared persistence interface.

## 9. Selection evidence

Record:
- exact product/driver/version/config;
- hardware/OS;
- workload/data size;
- transaction/concurrency/isolation results;
- resource measurements;
- backup/recovery evidence;
- known limitations;
- migration/exit implications.

Then select the product for the slice. Do not keep a decision open indefinitely merely to preserve theoretical optionality.
