# Workstation Synchronization and Authority

**Version:** v0.0.15

## 1. Trust model

The Workstation is trusted as a user tool but **not** as a server authority.

Assume a local user can inspect or modify local storage, requests and configuration.

Therefore the server independently performs:
- authentication;
- authoritative tenant derivation;
- authorization;
- schema/input validation;
- business/rule validation;
- idempotency;
- concurrency/conflict checks;
- central transaction.

The Workstation never receives central database credentials.

## 2. Durable local transaction

For an offline-capable action:

```text
BEGIN LOCAL TRANSACTION
  business change
  outbox/change record
COMMIT
```

If the local database says commit succeeded, both business change and outbox are present according to the selected local-store durability contract. If commit fails, neither becomes successful business state.

An in-memory Channel/signal may wake the sync loop but is never the durable queue.

## 3. Local status versus remote authority

A local save must not be confused with server acceptance.

Use explicit states such as:
- `LocalCommitted`;
- `PendingRemote`;
- `Authoritative`;
- `Conflict`;
- `Rejected`;
- `Retryable`;
- `AuthorizationChanged`;
- `UpgradeRequired`.

## 4. Upload flow

```text
select bounded pending batch
→ send authenticated SyncBatch
→ deduplicate/idempotency receipt
→ authorize each semantic operation
→ validate current business/rule/fact state
→ apply central transaction
→ record receipt/change feed
→ return per-item result
→ persist result locally
```

Prefer per-item results unless a group of changes is intentionally one business atomic unit.

Batch limits are bounded by both **item count and encoded bytes**. A batch that is cheap in row count can still be too expensive if payloads are large.

## 5. Semantic sync versus attachment transfer

Large file transfer must not starve small business synchronization.

Separate enough scheduling/resource budget that:
- semantic operation batches remain responsive;
- attachment uploads/downloads use bounded concurrent transfers;
- provider/rack bandwidth limits are respected;
- large transfers can resume/retry where supported;
- transfer backlog/age is observable.

The business outbox can reference staged attachment metadata rather than embedding giant file bytes in the semantic sync envelope.

## 6. Response-loss case

The server may commit and the network may fail before the client sees the response.

Retrying the same semantic operation must return `AlreadyApplied`/the previous semantic result through a stable idempotency key rather than create a duplicate order/payment/etc.

## 7. Remote changes

```text
request changes after cursor
→ receive authorized scoped changes
→ local transaction:
     apply remote changes
     advance cursor
→ commit
```

Never advance the cursor independently of durable local apply.

Remote batches are bounded. If the local disk cannot safely stage/apply the next batch, stop before corrupting/losing already durable local work and surface a storage-recovery state.

## 8. Conflict policy is per aggregate

- Customer/contact: merge/version where safe.
- Product/catalog: server-authoritative/versioned as appropriate.
- Inventory: transactional authoritative operation; no generic last-write-wins.
- Order: command + expected version.
- Payment: immutable/idempotent effect + reconciliation.
- Credit: current authoritative exposure.
- Quotation draft: version/merge/manual review may be supported.
- Published quotation: immutable revision.
- Rules/workflow/permissions: server-published authority.
- Attachments: immutable object identity + metadata version.

## 9. Permission/rule changes while offline

Permission grants and rule/workflow publication are Web-only. A Workstation may hold stale local snapshots while offline.

On reconnect, the server reauthorizes/revalidates pending operations. If authority or required current facts changed, return an explicit `AuthorizationChanged`, `Conflict`, `RequiresServerFact` or equivalent review result and preserve the local evidence rather than silently discarding it.

The Workstation permission/rule snapshot version is context/evidence, not a server capability token.

## 10. Supported offline window, tombstones and compaction

`Can be offline for months` is not implementable unless the server defines how long change/tombstone history remains sufficient for incremental catch-up.

The sync protocol therefore declares a supported incremental-history window based on actual retention/capacity policy.

If the Workstation cursor is older than retained safe history, the server returns an explicit resnapshot/upgrade recovery requirement rather than pretending incremental sync is complete.

Do not silently drop deletion/merge evidence because tombstones were compacted.

Exact retention duration is a deployment/product decision based on expected offline behavior and storage cost; do not invent a forever-retention guarantee.

## 11. Resnapshot/rebase recovery

A resnapshot is not `delete local DB and download server state` when unsynced local work exists.

Recovery flow must conceptually preserve:
- pending local semantic operations;
- staged/unsynced attachment references;
- local evidence needed for conflict review;
- user/account/device identity needed to reassociate the local store safely.

Then:

```text
preserve/export pending local intent
→ obtain authorized current server snapshot
→ rebuild/upgrade local authoritative mirror
→ rebase/review pending local operations
→ resume synchronization
```

Exact implementation can be optimized later, but silent local-work deletion is prohibited.

## 12. Long-offline recovery

A client may return after weeks/months with:
- expired user/device credentials;
- old protocol;
- old local schema;
- old rule/config snapshot;
- compacted tombstones;
- remotely deleted/merged entities;
- large outbox;
- missing staged attachment;
- insufficient local disk for the current snapshot.

Recovery can be reauth, upgrade, resnapshot, rebase, conflict review or export/repair.

Never silently delete pending user work.

## 13. Local capacity and outbox growth

The local outbox/staging area is durable but **bounded by real customer disk capacity**.

Track at least:
- pending item count;
- encoded bytes where useful;
- oldest pending age;
- staged attachment bytes;
- local DB file size/free space;
- retry/conflict items requiring user action.

When space is low:
- preserve already committed work;
- stop accepting optional large files/work before total disk exhaustion;
- allow export/support recovery;
- do not discard old unsynced work simply to shrink the queue.

Workstation device/disk behavior is owned by `docs/workstation/LOCAL_FIRST_DESKTOP.md`.

## 14. Backpressure and constrained-site bandwidth

When reconnecting many clients or large backlogs:
- bounded batch count/bytes;
- `Retry-After`/backoff;
- exponential backoff + jitter;
- tenant/device fairness;
- server admission control;
- DB connection/work budget;
- network transfer budget;
- no tight reconnect loop.

Do not let all Workstations come online after an outage and create synchronized retry traffic that overwhelms the lower-spec rack/uplink.

## 15. Sync observability/support

Expose enough safe evidence for user/support to distinguish:
- offline/network issue;
- pending but healthy backlog;
- server throttling;
- authentication/device problem;
- authorization changed;
- business conflict;
- protocol/upgrade required;
- local storage low/full;
- attachment missing/transfer stalled.

Important measures include oldest pending age and last successful **semantic** sync, not only a green connectivity icon.
