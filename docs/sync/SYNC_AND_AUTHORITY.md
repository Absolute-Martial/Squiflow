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

If power is lost after commit, both exist. If commit fails, neither becomes successful business state.

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
→ validate current business state/rules
→ apply central transaction
→ record receipt/change feed
→ return per-item result
→ persist result locally
```

Prefer per-item results unless a group of changes is intentionally one business atomic unit.

## 5. Response-loss case

The server may commit and the network may fail before the client sees the response.

Retrying the same semantic operation must return `AlreadyApplied`/the previous result through a stable idempotency key rather than create a duplicate order/payment/etc.

## 6. Remote changes

```text
request changes after cursor
→ receive authorized scoped changes
→ local transaction:
     apply remote changes
     advance cursor
→ commit
```

Never advance the cursor independently of durable local apply.

## 7. Conflict policy is per aggregate

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

## 8. Permission changes while offline

Permission grants are Web-only. A Workstation may still hold a stale local permission snapshot while offline.

When it reconnects, the server reauthorizes pending operations. If authority was revoked, return an explicit `AuthorizationChanged`/review result and preserve the local evidence rather than silently discard it.

## 9. Long-offline recovery

A client may return after weeks/months with:
- expired user/device credentials;
- old protocol;
- old local schema;
- old rule/config snapshot;
- compacted tombstones;
- remotely deleted/merged entities;
- large outbox;
- missing staged attachment.

Recovery can be reauth, upgrade, resnapshot, rebase, conflict review or export/repair.

Never silently delete pending user work.

## 10. Backpressure

When reconnecting many clients or large backlogs:
- bounded batch count/bytes;
- Retry-After/backoff;
- exponential backoff + jitter;
- tenant/device fairness;
- server admission control;
- no tight reconnect loop.
