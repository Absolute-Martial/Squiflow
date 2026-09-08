# Workstation Synchronization and Authority

**Version:** v0.0.15

## 1. Trust model

The Workstation is trusted as a user tool but **not** as server authority.

Assume a local user can inspect/modify local storage, requests and configuration.

Therefore the server independently performs:
- ZITADEL-backed authentication/session/device validation;
- authoritative SquiFlow TenantContext derivation;
- current OpenFGA authorization;
- schema/input validation;
- business/rule/workflow validation;
- idempotency;
- concurrency/conflict checks;
- central transaction.

The Workstation never receives central DB credentials and never writes OpenFGA tuples.

## 2. Durable local transaction

For an offline-capable action:

```text
BEGIN LOCAL TRANSACTION
  business change
  outbox/change record
COMMIT
```

If local commit succeeds, both business state and outbox exist according to the selected local-store durability contract. If commit fails, neither becomes successful local business state.

An in-memory Channel/signal can wake sync but is never the durable queue.

Guard may restart the Workstation after failure, but sync recovery always comes from this durable local state rather than Guard memory.

## 3. Local status versus remote authority

Use explicit states:
- `LocalCommitted`;
- `PendingRemote`;
- `Authoritative`;
- `Conflict`;
- `Rejected`;
- `Retryable`;
- `AuthorizationChanged`;
- `UpgradeRequired`.

A local save is not server acceptance.

## 4. Upload flow

```text
select bounded pending batch
→ send authenticated SyncBatch
→ validate ZITADEL-backed session/device context
→ derive authoritative TenantContext
→ deduplicate/idempotency receipt
→ authorize each semantic operation through current OpenFGA/SquiFlow authorization path
→ validate current business/rule/workflow/fact state
→ apply central transaction
→ record receipt/change feed
→ return per-item result
→ persist result locally
```

Prefer per-item results unless a group is intentionally one atomic business operation.

Batch limits are bounded by item count **and encoded bytes**.

## 5. Authorization snapshot versus current OpenFGA state

The Workstation may keep an effective permission snapshot for local UX/offline eligibility.

It can include:
- SquiFlow `TenantAuthorizationRevision`;
- relevant OpenFGA authorization-model context/version identifier for diagnostics/compatibility;
- effective local permission summary used by UX.

It is never a server capability token.

On reconnect the server checks current OpenFGA authorization again. A local snapshot cannot override:
- revoked role/relationship;
- removed tenant membership;
- changed entitlement;
- changed resource relationship;
- platform/tenant isolation boundary.

## 6. Permission change while offline

Example:

```text
Staff creates local order offline under permission snapshot R17
Owner removes orders.create via Web
OpenFGA revocation applies
TenantAuthorizationRevision becomes R18
Staff reconnects
```

The server must not accept the operation simply because the old snapshot was valid when the local action was created.

Return `AuthorizationChanged`/review according to the command semantics while preserving the local user intent/evidence. The user may need an Owner/authorized actor to recreate/approve the action online rather than silently losing it.

## 7. OpenFGA dependency failure during sync

Distinguish:
- explicit OpenFGA deny;
- OpenFGA provider unavailable/timeout;
- authorization model/config mismatch;
- ambiguous/reconciliation state after a role mutation.

Provider failure must not become `allowed=true`.

For ordinary actor-authorized pending commands, inability to obtain the required current authorization results in retryable/degraded/fail-closed behavior, not a guessed permission result.

Already committed business consequences are a different Worker concern and do not use this actor-command rule blindly.

## 8. Semantic sync versus attachment transfer

Large file transfer must not starve small business sync.

Use separate enough scheduling/resource limits that:
- semantic operation batches remain responsive;
- attachment transfers use bounded concurrency;
- provider/rack bandwidth is respected;
- large transfers resume/retry where supported;
- transfer backlog/age is observable.

The business outbox references staged attachment metadata rather than embedding giant file bytes.

Server-side retained object transfer goes through the `IObjectStore` boundary; the Workstation does not depend on Hugging Face-specific APIs.

## 9. Response-loss case

The server can commit and lose the response.

Retrying the same semantic operation with the same idempotency key returns `AlreadyApplied`/the previous semantic result rather than duplicating the order/payment/etc.

## 10. Remote changes

```text
request changes after cursor
→ receive authorized scoped changes
→ local transaction:
     apply remote changes
     advance cursor
→ commit
```

Never advance cursor independently of durable local apply.

If local disk cannot safely stage/apply the next batch, stop before corrupting already durable local work and surface storage recovery state.

## 11. Conflict policy is per aggregate

- Customer/contact: merge/version where safe.
- Product/catalog: server-authoritative/versioned as appropriate.
- Inventory: transactional authoritative operation; no global LWW.
- Order: command + expected version.
- Payment: immutable/idempotent effect + reconciliation.
- Credit: current authoritative exposure.
- Quotation draft: version/merge/manual review may be supported.
- Published quotation: immutable revision.
- Roles/permissions: OpenFGA/server authority.
- Rules/workflow publication: server authority.
- Attachments: immutable object identity + metadata version.

## 12. Rules/facts while offline

Rule/workflow snapshots can be stale just like permissions.

On reconnect the server revalidates current required rule/workflow/fact state. If a rule requires a `ServerRequired` fact such as current shared credit/stock/security context, the offline result cannot become authoritative merely because local evaluation once succeeded.

## 13. Supported offline window, tombstones and compaction

The protocol declares a supported incremental-history window based on actual retention/capacity policy.

If cursor is older than retained safe history, return explicit resnapshot/upgrade recovery rather than pretending incremental sync is complete.

Do not silently drop deletion/merge evidence because tombstones were compacted.

Exact retention duration is a product/deployment decision; do not promise forever history by default.

## 14. Resnapshot/rebase recovery

A resnapshot is not `delete local DB and download server state` when unsynced local work exists.

Preserve:
- pending local semantic operations;
- staged/unsynced attachment references;
- conflict-review evidence;
- account/device/store identity needed to reassociate safely.

Then:

```text
preserve/export pending local intent
→ ZITADEL reauthentication if needed
→ obtain authorized current server snapshot
→ refresh current OpenFGA-derived effective permissions
→ rebuild/upgrade local authoritative mirror
→ rebase/review pending local operations
→ resume sync
```

Silent local-work deletion is prohibited.

## 15. Long-offline recovery

A client may return with:
- expired ZITADEL/session/device credentials;
- old protocol/local schema;
- old OpenFGA permission snapshot/model context;
- old rule/config snapshot;
- compacted tombstones;
- remotely deleted/merged entities;
- large outbox;
- missing staged attachment;
- insufficient local disk.

Recovery can be reauth, upgrade, resnapshot, rebase, conflict review or export/repair.

Never silently delete pending user work.

## 16. Local capacity and Guard interaction

Track:
- pending item count/bytes/oldest age;
- staged attachment bytes;
- local DB size/free space;
- retry/conflict items requiring user action;
- Workstation/Guard diagnostic/update temp usage where application-controlled.

When space is low:
- preserve already committed work;
- stop optional large files/work before total disk exhaustion;
- allow export/support recovery;
- never discard old unsynced work merely to shrink the queue.

Guard can surface/recover process lifecycle and local resource pressure, but it does not delete business outbox rows or acknowledge sync results.

## 17. Backpressure and constrained bandwidth

When reconnecting many clients/large backlogs:
- bounded batch count/bytes;
- `Retry-After`/backoff/jitter;
- tenant/device fairness;
- server admission control;
- DB work budget;
- OpenFGA/ZITADEL dependency-call budgets;
- network transfer budget;
- no tight reconnect loop.

Do not let clients reconnect after an outage and overwhelm the lower-spec rack or security dependencies.

## 18. Sync observability/support

Expose enough safe evidence to distinguish:
- offline/network issue;
- Guard/Workstation process recovery issue;
- pending healthy backlog;
- server throttling;
- ZITADEL authentication/device issue;
- OpenFGA authorization deny/unavailable/model mismatch;
- `AuthorizationChanged` after revocation;
- business conflict;
- protocol/upgrade required;
- local storage low/full;
- attachment transfer stalled.

Important measures include oldest pending age and last successful **semantic** sync, not only connectivity state.
