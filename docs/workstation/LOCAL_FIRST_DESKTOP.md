# Windows Workstation — Local-First Architecture

**Version:** v0.0.15

**Reference:** Ink & Switch, *Local-first software: You own your data, in spite of the cloud* (2019), https://www.inkandswitch.com/essay/local-first/

## 1. What we adopt

The local-first paper gives several principles that fit SquiFlow Workstation well:
- fast interaction without waiting for network round trips;
- useful operation when the network is unavailable;
- durable local work;
- multi-device synchronization;
- understandable change/history behavior;
- user control/export/longevity;
- installed native software as a stronger offline experience than a browser tab.

## 2. What we do not copy blindly

The paper itself distinguishes document/personal-data local-first software from banking/e-commerce-like systems that are well served by centralized authority.

SquiFlow contains payments, shared inventory, credit, permissions, tenant security, workflow publication and other globally coordinated business facts.

Therefore SquiFlow uses **local-first interaction and durability**, not unrestricted peer-to-peer/global multi-master authority.

No global CRDT requirement is introduced.

## 3. Workstation interaction rule

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

The user should not wait for a server round trip before the locally permitted operation is safely stored.

`System.Threading.Channels` or another in-memory signal can wake synchronization, but the durable local store remains truth after a crash/restart.

## 4. Local state is real state, but authority is explicit

Do not call the local database “just a cache.” It contains real user work and must survive restart/offline operation.

But do not call every local value globally authoritative either.

Use explicit user/system states:

```text
LocalCommitted
PendingRemote
Authoritative
Conflict
Rejected
UpgradeRequired
```

Examples:
- local draft/order capture may become `LocalCommitted` immediately;
- a shared-stock reservation may remain provisional until server acceptance;
- permissions are never granted by the local database;
- payment/external effects may require online/server authority.

## 5. Authority classes

Every command/aggregate defines one of these behavioral classes.

### A. Local-capable / remotely reconciled
User may perform the action offline; it is durably recorded and later synchronized.

Possible examples after domain proof:
- customer/contact capture;
- order drafting;
- quotation drafting;
- allowed local document/print preparation.

### B. Local provisional
User may continue offline, but SquiFlow must clearly show that remote authority is pending.

Possible examples:
- operations affected by current credit exposure;
- operations affected by globally shared inventory;
- some final issuance/numbering actions.

### C. Server-required
The action is unavailable offline because correctness depends on current shared authority.

Examples include platform administration and may include payment/provider effects, privileged refunds, permission changes, critical stock operations or other domain-specific actions.

Do not hide this distinction from the user.

## 6. Read path

Where data is available locally, Workstation screens should read local state directly instead of waiting for the network.

Remote synchronization can update local state in the background. UI state follows the local database/change notification model.

Do not implement a UI that unnecessarily reloads the server after every local edit just to prove the local write happened.

## 7. Sync path

```text
LocalCommitted change
→ durable outbox
→ bounded batch
→ authenticated Sync API
→ server tenant derivation + authorization
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

## 8. Conflict philosophy

Do not assume one conflict algorithm fits everything.

- Customer/profile: merge/version where safe.
- Draft quotation: version/merge/manual review can be reasonable.
- Published quotation: immutable revision.
- Order: command + expected version.
- Inventory: authoritative transactional operation; no generic last-write-wins.
- Payment: immutable/idempotent event + reconciliation.
- Permission/rules/workflow publication: server authority.

CRDTs may be evaluated later for a narrow genuinely collaborative document-like feature, not for the whole business database.

## 9. History and explainability

A local-first user needs to understand what happened when remote changes arrive.

At minimum expose:
- pending sync count/state;
- last successful sync;
- rejected/conflicting operations needing attention;
- operation/decision IDs useful for support;
- whether the visible record is local pending or remotely accepted.

For important collaborative/revisioned data, keep enough version/change history to explain changes without accumulating an unbounded CRDT history model.

## 10. Network behavior

Network is optional for local-capable operations.

When network disappears:
- do not continuously show blocking errors for every local action;
- stop busy retry loops;
- continue locally allowed work;
- expose connectivity/sync state without dominating the UI;
- back off retries with jitter;
- reconcile when connectivity returns.

For server-required operations, explain why the action needs connectivity instead of pretending it succeeded.

## 11. User control and longevity

Local-first principles also imply user agency.

SquiFlow should support appropriate export/backup of business data/documents to stable formats such as JSON/CSV/PDF/images where business/security policy allows.

This does not mean raw central database access or bypassing tenant/security rules.

## 12. Security reality

A local-first Workstation means authorized business data exists on the device.

Assume a determined local user can inspect/tamper with local storage.

Therefore:
- local storage is protected using OS secure-storage/data-at-rest mechanisms where appropriate;
- secrets are minimized;
- no central DB credentials exist locally;
- local tenant IDs/permissions are not trusted by the server;
- remote synchronization reauthenticates/re-authorizes material changes;
- device revocation does not magically erase offline bytes already on a stolen device.

## 13. Long-offline behavior

A Workstation may return after weeks/months with:
- expired auth/device credentials;
- old local schema;
- old rule/config snapshot;
- old sync protocol;
- tombstones already compacted;
- pending changes to entities that were deleted/merged remotely.

Required recovery can be reauthentication, upgrade, scoped resnapshot, rebase/conflict review or export/repair.

Never silently discard local user work.

## 14. Resource behavior

Local-first must not mean “keep everything in RAM.”

Use durable local storage, bounded in-memory caches, event-driven wakeups and triggered helpers.

The Workstation remains a guest on the customer's PC and does not expand workers/caches simply because RAM is available.

## 15. Qualification tests

Test at least:
- power loss immediately after local commit;
- lost in-memory wake signal;
- app restart with pending outbox;
- sleep/hibernate during sync;
- days/months offline;
- two Workstations editing overlapping data;
- response lost after server commit;
- permission revoked while local work is pending;
- rule/protocol version changes while offline;
- local database tamper/corruption;
- 10k+ queued changes reconnecting;
- large staged attachment;
- wrong local clock/timezone.
