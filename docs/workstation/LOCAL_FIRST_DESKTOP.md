# Windows Workstation — Local-First Architecture

**Version:** v0.0.15

**Reference:** Ink & Switch, *Local-first software: You own your data, in spite of the cloud* (2019), https://www.inkandswitch.com/essay/local-first/

## 1. What SquiFlow adopts

The Workstation should provide:
- fast local interaction without waiting for ordinary network round trips;
- useful operation during network loss for explicitly local-capable work;
- durable local work that survives process restart;
- background synchronization;
- understandable pending/conflict/rejection state;
- user control/export/recovery where policy allows.

## 2. What SquiFlow does not copy blindly

Payments, shared inventory, credit, permissions, tenant security, workflow publication and similar shared facts need central authority.

Therefore SquiFlow uses **local-first interaction and durability**, not unrestricted peer-to-peer/global multi-master authority. There is no global CRDT requirement.

## 3. Desktop process model

```text
SquiFlow.Guard
└── SquiFlow.Workstation
```

Guard is part of the local-first reliability story: process crash/hang/update failure must not make durable local work disappear or leave the application in an unexplained restart loop.

Guard supervises lifecycle only. The Workstation owns UI, local business/application logic, local DB/outbox, sync, and device interactions. See `docs/workstation/GUARD_AND_RECOVERY.md`.

## 4. Local transaction rule

For a business operation explicitly allowed offline:

```text
User action
→ local validation
→ applicable immutable local rule/config snapshot
→ one durable local transaction
     business record/state + outbox/change record
→ immediate local UI update
→ background synchronization later
```

If the Workstation crashes after the local commit, Guard may restart the process, but recovery comes from the durable local store—not from Guard memory.

In-memory channels/signals may wake synchronization but are never durable truth.

## 5. Local state is real state, but authority is explicit

Use states such as:

```text
LocalCommitted
PendingRemote
Authoritative
Conflict
Rejected
AuthorizationChanged
UpgradeRequired
```

Examples:
- order/customer capture can be locally committed where allowed;
- operations affected by current shared stock/credit can be provisional or server-required;
- permission assignment is never locally authoritative;
- provider payment/refund effects may require server/provider authority.

## 6. Authority classes

### Local-capable / remotely reconciled
The user can perform the action offline and keep working after durable local commit.

### Local provisional
The user can proceed locally but the UI must show that current server authority/facts can still reject or alter the final outcome.

### Server-required
The action is unavailable offline because current shared/security/external authority is required.

Examples include role/permission changes and platform controls, and can include payments/refunds/critical stock/credit actions according to domain policy.

## 7. Authorization while offline

ZITADEL authenticates the user when connectivity/session allows; OpenFGA is the current server authorization engine.

The Workstation may keep a **versioned effective permission snapshot** for local UX/offline eligibility. That snapshot includes SquiFlow authorization revision/model context sufficient for diagnostics/refresh, but it is not a server capability token.

On sync the server repeats:
- current account/device/session validation;
- authoritative TenantContext derivation;
- current OpenFGA permission/resource check;
- current domain/workflow/state validation.

If the Owner revoked permission while the Workstation was offline, pending work remains locally preserved but can return `AuthorizationChanged`/review rather than being silently discarded or incorrectly accepted.

## 8. Read path

Where authorized data is already local, Workstation screens should read local state directly rather than block on a server read after every edit.

Remote synchronization updates local state in the background.

## 9. Sync path

```text
LocalCommitted change
→ durable outbox
→ bounded batch
→ authenticated Sync API
→ server TenantContext
→ OpenFGA authorization
→ business/rule/concurrency validation
→ idempotent central transaction
→ per-item result/receipt
→ local durable acknowledgement
```

Remote changes:

```text
server change feed/cursor
→ authorized scoped changes
→ local transaction: apply changes + advance cursor
→ UI updates
```

Never advance the cursor independently from successful local apply.

## 10. Conflict policy is per aggregate

- Customer/profile: merge/version where safe.
- Draft quotation: version/merge/manual review where useful.
- Published quotation: immutable revision.
- Order: command + expected version.
- Inventory: authoritative transactional operation; no generic LWW.
- Payment: immutable/idempotent effect + reconciliation.
- Permission/roles: OpenFGA/server authority.
- Rules/workflow publication: server authority.
- Attachments: immutable object identity + metadata version.

## 11. Long-offline behavior

A returning Workstation may have:
- expired ZITADEL/session/device credentials;
- old local schema/protocol;
- old OpenFGA-derived permission snapshot;
- old rule/config snapshot;
- compacted tombstones;
- pending operations against deleted/changed entities.

Recovery can require reauthentication, upgrade, resnapshot, rebase/conflict review, or export/repair.

Never silently discard durable user work.

## 12. Local capacity

Local-first must not mean unlimited disk/RAM usage.

Track/bound:
- local DB growth;
- pending outbox count/bytes/age;
- staged attachments;
- application-controlled temp files;
- logs/diagnostics/update data;
- helper output where helpers later exist.

Low-space behavior preserves already committed work and stops optional large work before complete disk exhaustion.

## 13. Guard interaction

Guard may:
- restart the Workstation after a crash;
- detect sustained hang using the defined heartbeat policy;
- coordinate safe mode/update recovery;
- collect bounded diagnostic/process evidence.

Guard must not:
- repair/rewrite business rows by guessing;
- grant permissions or call OpenFGA as a business actor;
- acknowledge sync work;
- delete pending local operations merely to resolve a crash loop.

Guard resource goals are measured on supported hardware. Do not reduce functionality simply to chase an arbitrary tiny-memory number.

## 14. Network behavior

Network loss for local-capable operations:
- continues local work;
- does not busy-loop errors/retries;
- shows connectivity/sync state without blocking the whole application;
- backs off with jitter;
- reconciles on reconnect.

Server-required operations explain that current connectivity/authority is required instead of showing false success.

## 15. User control and longevity

Support appropriate export/backup of business data/documents to stable formats where business/security policy permits. This does not mean raw central DB access or bypassing tenant authorization.

## 16. Security reality

Authorized business data exists locally by design. Assume a determined local user can inspect/tamper with local storage.

Therefore:
- minimize local secrets;
- no central DB credentials locally;
- local tenant IDs/permissions are never trusted by server authority;
- server reauthenticates/reauthorizes material changes;
- Windows secure-storage/data protection is used for credentials as proven by the POC;
- revoking a device does not magically erase bytes already present on a stolen machine.

## 17. Qualification tests

Test at least:
- power/process loss immediately after local commit;
- Guard restarts Workstation without losing pending work;
- Guard crash while Workstation remains healthy;
- repeated Workstation startup crash enters bounded recovery/safe mode;
- sleep/hibernate during heartbeat/sync;
- days/months offline;
- two Workstations editing overlapping data;
- response lost after server commit;
- OpenFGA permission revoked while local work is pending;
- model/rule/protocol version changes while offline;
- local DB tamper/corruption;
- 10k+ queued changes reconnecting;
- large staged attachment;
- wrong local clock/timezone;
- disk nearly full during recovery/update.
