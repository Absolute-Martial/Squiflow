# Windows Workstation — Local-First Architecture

**Version:** v0.0.15

**Reference:** Ink & Switch, *Local-first software: You own your data, in spite of the cloud* (2019), https://www.inkandswitch.com/essay/local-first/

## 1. What SquiFlow adopts

The Workstation should provide:
- fast interaction without waiting for network round trips for explicitly local-capable work;
- durable local work;
- useful operation when the network is unavailable;
- background synchronization;
- understandable pending/conflict/history state;
- export/recovery paths for user work.

SquiFlow does **not** copy a peer-to-peer document-editor authority model into payments, stock, credit, permissions or other centrally coordinated business facts.

No global CRDT requirement exists.

## 2. Baseline process model

Start with one process:

```text
SquiFlow.Workstation
```

Do not create an always-running Guard/supervisor/helper process during the baseline.

If a future updater, printer/native library, document parser or other component proves it can hang/crash/leak in a way that warrants process isolation, add one narrow process then. The problem must exist before the helper does.

## 3. Local transaction rule

For an operation explicitly allowed offline:

```text
User action
→ local validation
→ applicable compatible local rule/config snapshot
→ one durable local transaction
     business state + outbox/change record
→ immediate local UI update
→ background synchronization later
```

The durable local store, not an in-memory queue/channel, survives restart.

## 4. Local state versus server authority

Local storage contains real user work, not disposable cache data.

Use explicit states such as:

```text
LocalCommitted
PendingRemote
Authoritative
Conflict
Rejected
AuthorizationChanged
UpgradeRequired
```

Do not tell the user a server accepted something merely because the local transaction committed.

## 5. Authority classes

### Local-capable
May be completed locally and reconciled later, for example selected customer/order/quotation drafting operations after domain proof.

### Local-provisional
Can continue locally but remote authority is still pending, for example an operation influenced by current shared credit/stock.

### Server-required
Unavailable offline because correctness requires current central authority, for example permission changes, many payment/provider effects, privileged refunds, and selected shared-stock/financial actions.

Each real command chooses deliberately; do not create a huge generic policy framework before commands exist.

## 6. Read path

Where relevant data is local, Workstation screens read local state directly. Do not round-trip to the server after every local write merely to redisplay the same value.

Remote synchronization updates local state in the background.

## 7. Sync path

```text
LocalCommitted change
→ durable outbox
→ bounded batch
→ authenticated Sync API
→ authoritative tenant + permission + business/rule/concurrency validation
→ idempotent central transaction
→ per-item result
→ durable local acknowledgement
```

Remote changes + cursor advancement commit together locally. Never advance the cursor before the changes are durably applied.

## 8. Conflict policy

No one algorithm fits every aggregate.

- Customer/profile: merge/version where safe.
- Draft quotation: version/merge/manual review can be reasonable.
- Published quotation: immutable revision.
- Order: command + expected version.
- Inventory: authoritative transactional operation, no generic last-write-wins.
- Payment: idempotent/immutable effect + reconciliation.
- Permissions/rules/workflow publication: server authority.

## 9. Network behavior

When offline:
- continue local-capable work;
- do not busy-loop retries;
- show connectivity/sync state without blocking ordinary local interaction;
- back off retries with jitter;
- explain why server-required actions need connectivity.

## 10. Local disk/staging

Bound local DB, pending attachment staging, temp data, logs/diagnostics and application-managed exports.

When disk space becomes low:
- preserve committed local business work;
- stop optional heavy new processing;
- never delete unsynced business evidence just to recover space;
- explain which SquiFlow-managed data is consuming space where practical.

## 11. Printing

Printing is a device side effect and begins inside the ordinary Workstation process using the supported Windows printing/spooler path.

```text
committed business document
→ print request
→ local printer/spooler
→ success/failure/unknown physical output
```

Printer failure does not undo the sale/order/invoice. Retry and alternate-printer selection are separate actions.

Do not claim physical paper output merely because the spooler accepted a job.

Other hardware integrations are added only after a real customer journey requires them.

## 12. Security/session reality

Assume a determined local user can inspect/tamper with local storage.

Therefore:
- no central DB credentials are stored locally;
- local tenant IDs/permissions are not server authority;
- synchronization reauthenticates/reauthorizes material changes;
- device/session expiry does not delete pending local business work;
- a stolen/revoked device cannot be assumed to have had already-downloaded bytes remotely erased.

Exact shared-Windows-profile/user-switch behavior remains an implementation decision before that scenario is supported.

## 13. Long-offline recovery

A Workstation can return with expired credentials, old schema/protocol/rules, compacted tombstones, or pending work against remotely changed/deleted data.

Recovery may require reauthentication, upgrade, resnapshot, rebase/conflict review or export/repair.

Never silently discard pending user work.

## 14. Resource behavior

Use durable storage and bounded in-memory work. Do not increase cache/worker count simply because more RAM/CPU is present.

Avoid helper processes until actual process isolation is justified.

## 15. Qualification cases

Test at least:
- process termination immediately after local commit;
- lost in-memory sync wake signal;
- restart with pending outbox;
- sleep/hibernate during sync;
- disk full/low space;
- days/months offline;
- two Workstations editing overlapping data;
- response lost after server commit;
- permission revoked while local work is pending;
- rule/protocol version changes while offline;
- local DB tamper/corruption;
- large pending queue reconnecting;
- large staged attachment;
- wrong local clock/timezone;
- printer unavailable/spooler error after business commit.
