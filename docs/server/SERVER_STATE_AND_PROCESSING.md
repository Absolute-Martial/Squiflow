# Server Runtime, Processing State, and Authoritative Persistence

**Status:** Accepted architecture direction

## 1. Decision

SquiFlow server hosts are not thin `HTTP -> SQL` facades, and they do not each own a second copy of the business database.

The server architecture separates three kinds of state:

```text
1. Ephemeral runtime state
   memory / bounded cache / rate-limit counters / session-circuit state

2. Durable processing state
   inbox / jobs / operation status / idempotency receipts / outbox

3. Authoritative business state
   PostgreSQL business records and invariants
```

This is a **logical ownership separation**. It does not require three database products.

The first implementation may keep durable processing records in PostgreSQL under explicit tables/schemas/ownership when that is the simplest reliable choice. A separate broker/cache/database is introduced only when a demonstrated workload, scaling, isolation, or availability requirement earns it.

## 2. Server request path

Do not implement endpoints as direct persistence code:

```text
HTTP endpoint
  -> ad-hoc SQL
```

The intended path is:

```text
WebApi / SyncApi / AdminApi
        |
        v
request/authentication/admission boundary
        |
        v
application use case
        |
        v
Capability Core / authoritative policy
        |
        v
persistence adapter
        |
        v
PostgreSQL authoritative transaction
```

The API host owns transport, authentication, admission, validation and composition. It does not bypass capability ownership merely because it can reach the database.

## 3. State class A — ephemeral runtime state

Ephemeral state exists to make server compute efficient or usable, not to preserve business truth.

Examples:

- process-local query/cache entries;
- bounded distributed cache where later justified;
- rate-limit/admission counters;
- short-lived request deduplication hints;
- Blazor circuit/session state;
- temporary computation state;
- temporary upload/chunk assembly metadata where loss is explicitly recoverable;
- circuit-breaker and dependency-health state.

Properties:

```text
Durable business authority: no
Must survive process restart: no
May be rebuilt/reloaded: yes
May be dropped under pressure: yes, according to policy
```

No cache may become current permission, payment, stock, credit, tenant-isolation, or other protected business authority.

A Redis-like distributed cache is therefore an optional future runtime optimization, not a mandatory business database.

## 4. State class B — durable processing state

Durable processing state records work that must survive process loss even when the final business effect has not completed yet.

Examples:

- durable inbox/admission records;
- long-running operation resources;
- background jobs;
- scheduled occurrences;
- idempotency receipts;
- transactional outbox entries;
- retry/quarantine state;
- synchronization admission/receipt state where asynchronous staging is used.

Properties:

```text
Must survive process restart: yes
Replay/recovery required: yes
Business authority by itself: normally no
May participate atomically with a business transaction: yes, where co-owned
```

An inbox/job row saying `Pending` means work is durably known. It does not by itself mean the requested business operation was authoritatively accepted.

### Initial physical placement

The initial implementation may keep these records in PostgreSQL because:

- durability/transactions are already required;
- mutation + idempotency receipt + outbox can often commit atomically;
- it avoids manufacturing a distributed transaction before scale requires one;
- it keeps recovery/backup relationships understandable.

Keep logical ownership explicit even when one PostgreSQL cluster stores both processing and business tables.

Later, a broker or dedicated processing store may be introduced when backlog scale, fan-out, throughput, retention, or failure isolation proves the need. Do not add another database merely to create the appearance of decoupling.

## 5. State class C — authoritative business state

PostgreSQL is the initial central authority for server business state.

Examples include:

- orders and quotations;
- payments/refunds/allocations;
- inventory movements/current protected state;
- customers and organizations;
- staff/membership metadata;
- device enrollment records;
- issued documents/revisions;
- workflow/rule publication metadata;
- authoritative usage/limit state where required.

This state is reached through the owning capability/application persistence path, not through arbitrary endpoint SQL or cross-module table mutation.

## 6. Interactive synchronous path

Ordinary short operations that require a definitive answer should stay synchronous.

Example:

```text
Web
 -> WebApi
 -> authenticate + TenantContext
 -> authorize
 -> validate
 -> application use case
 -> Capability Core
 -> persistence adapter
 -> PostgreSQL transaction
 -> authoritative result
```

Examples can include a normal customer update, staff administration command, or other bounded operation whose authoritative transaction fits the interactive request budget.

Do not queue every command merely because a Worker exists.

## 7. Asynchronous durable path

Long-running, resource-heavy, or after-commit consequences use durable processing state.

```text
Web
 -> WebApi
 -> authenticate/authorize/validate/idempotency
 -> durable operation/job accepted
 -> 202 + operation identifier

Worker
 -> claim durable work
 -> application use case / Capability Core
 -> PostgreSQL and/or object storage
 -> persist result/status
```

Typical candidates:

- PDF/document generation;
- large report/export;
- image processing;
- import/backfill;
- notifications;
- external integrations;
- projection/reconciliation work;
- other work that should not occupy an interactive request indefinitely.

For a committed authoritative business transaction that has asynchronous consequences, commit the business fact and outbox atomically where practical, then let Worker processing handle the consequences.

## 8. Workstation synchronization ingress

Sync traffic needs stronger admission/backpressure than ordinary interactive Web traffic because many Workstations may reconnect with backlogs at once.

The conceptual path is:

```text
Workstation
   |
   v
SyncApi
   |- authenticate user/device/session
   |- validate protocol/schema
   |- enforce item/byte/rate limits
   |- deduplicate known retries
   |- admission/backpressure/fairness
   v
bounded authoritative admission
or durable sync inbox when asynchronous staging is required
   |
   v
admission processor / Worker
   |
   v
Capability Core + authoritative checks
   |
   v
PostgreSQL authoritative commit
   |
   v
AuthoritativeReceipt
```

Not every small sync batch must be forced through a queue. Synchronous admission is valid when the operation fits the bounded server budget. Durable staging is used when burst isolation, long processing, or recovery semantics require it.

## 9. Transport acceptance is not business acceptance

If SyncApi or another ingress returns a transport/durable-acceptance response before business processing completes, the semantics must be explicit.

For example:

```text
202 Accepted / Received
OperationId = X
```

means:

> the server durably received/staged the operation for processing.

It does **not** mean:

> the business operation is authoritatively committed.

The later authoritative result remains explicit, for example:

```text
Accepted
Adjusted
Rejected
Conflict
AuthorizationChanged
Retryable
AlreadyApplied
```

This preserves the Workstation provisional-execution / authoritative-admission model.

## 10. No generic `WebApiDB -> CoreDB` chain

Do not create this as a baseline:

```text
WebApi
 -> WebApi business database
 -> authoritative Core database
```

A second general-purpose business database would immediately create questions about:

- which copy owns truth;
- cross-database transactions;
- replication delay;
- idempotency after partial failure;
- backup/restore consistency;
- migration ordering;
- reconciliation and conflict policy.

Use purpose-specific state instead:

```text
WebApi / SyncApi
   |- ephemeral cache/runtime state
   |- temporary/object staging
   |- durable inbox/job/idempotency/outbox state
   v
application + Capability Core
   v
PostgreSQL authoritative state
```

If a future workload earns a dedicated processing database or broker, its authority/recovery contract must be explicit and it must not silently become a second business authority.

## 11. Failure behavior

The architecture must remain correct when:

- an API process restarts and loses memory/cache;
- Worker crashes after claiming work;
- a request is retried after the client lost the response;
- SyncApi receives a reconnect burst;
- PostgreSQL is unavailable;
- a cache/distributed-runtime store is unavailable;
- an external provider outcome is unknown.

Rules:

- loss of ephemeral state may reduce performance/UX, not erase committed business truth;
- durable processing state must support resume/retry/quarantine/reconciliation according to semantics;
- PostgreSQL outage does not cause a cloud host to invent local SQLite authority;
- retry identity remains semantic and stable;
- no process memory is the sole source of durable work;
- an asynchronous transport acknowledgement is never mistaken for authoritative business completion.

## 12. Relationship to Workstation persistence

The Workstation remains different:

```text
Workstation
  SQLite/WAL
  local provisional business state
  durable local outbox
  offline continuity
```

Cloud/Web server processing does not copy this local-first database model. It uses server runtime/processing state around the central authoritative database.

The browser remains online-only for business operations under the current Web decision.

## 13. Related owners

- `docs/architecture/WEB_AND_SYNC_INGRESS.md`
- `docs/server/CORE_API_AND_WORKER.md`
- `docs/server/WORKER_RUNTIME_AND_SCHEDULING.md`
- `docs/data/PERSISTENCE_SELECTION.md`
- `docs/web/WEB_RUNTIME_AND_STORAGE.md`
- `docs/sync/SYNC_AND_AUTHORITY.md`
- `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`
