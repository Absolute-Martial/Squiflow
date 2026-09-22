# Workstation Synchronization and Authority

**Version:** v0.1.0

## 1. Trust model

The Workstation is trusted as a user tool but **not** as server authority.

Assume a local user can inspect/modify local storage, memory, requests and configuration.

Therefore the server independently performs the trust-boundary checks required by the operation, including:
- ZITADEL-backed authentication/session/device validation;
- authoritative SquiFlow `TenantContext` derivation;
- current OpenFGA authorization;
- schema/protocol/input validation;
- idempotency/deduplication;
- tenant isolation;
- authoritative concurrency/database constraints;
- current server-required business facts/invariants.

The Workstation never receives central DB credentials and never writes OpenFGA tuples.

Detailed two-stage execution owner: `docs/decisions/DUAL_PROCESSING_AND_IN_PROCESS_COORDINATION.md`.

## 2. Durable local transaction

For an offline-capable operation:

```text
BEGIN LOCAL TRANSACTION
  apply allowed provisional/local business projection
  write semantic OperationEnvelope/outbox state
COMMIT

best-effort process-local wake signal
```

If the local commit succeeds, the provisional business state and pending operation are durable according to the selected SQLite contract. If it fails, neither becomes successful local business state.

`LocalCommitted` means the operation intent and provisional projection are safely stored on that Workstation. It does not mean the central business transition has executed or been accepted.

An in-memory Channel/signal can wake sync but is never the durable queue.

Guard may restart the Workstation after failure, but sync recovery always comes from durable local state rather than Guard/process memory.

## 3. Semantic OperationEnvelope

Synchronization transports **business intent and relevant execution evidence**, not arbitrary table replication and not a second giant copy of the local database.

Conceptually:

```text
OperationEnvelope
├── OperationId / semantic idempotency key
├── capability + operation kind + schema version
├── tenant/device references required by the protocol
├── intent payload
├── expected/base aggregate version where applicable
├── dependency revision tokens used by local preparation
│   ├── RuleSetRevision
│   ├── CustomerRevision
│   ├── PricingRevision
│   └── operation-specific dependencies
├── optional bounded LocalDecisionEvidence / decision hashes
└── correlation/causation metadata
```

The exact envelope is capability/operation-specific. Do not create one unbounded `object` payload that silently carries entire aggregates/provider state.

Large attachment bytes are staged/transferred separately and referenced from the semantic operation where necessary.

## 4. Local status versus remote authority

Use explicit states/outcomes. At minimum:

- `LocalCommitted`;
- `PendingRemote`;
- `Authoritative` / accepted;
- `Adjusted`;
- `Conflict`;
- `Rejected`;
- `Retryable`;
- `AuthorizationChanged`;
- `UpgradeRequired`;
- `AlreadyApplied`.

A local save is not server acceptance.

`Adjusted` means the operation remains valid but authoritative values/result differ from the provisional local decision. It is not automatically a user conflict.

## 5. Upload, authoritative admission and reconciliation

```text
select bounded pending operation batch
→ send authenticated SyncBatch
→ validate session/device context
→ derive authoritative TenantContext
→ deduplicate/idempotency receipt
→ authorize each semantic operation through current OpenFGA/SquiFlow path
→ compare relevant dependency/version evidence with current state
→ invoke the capability operation's declared admission strategy
     ServerRequired / ValidateAndCommit / ExpectedRevision
     ConvergentMerge / BoundedDelegation
→ always perform mandatory current invariants/concurrency/constraints
→ commit authoritative transaction
→ record receipt/change feed/outbox where co-owned
→ return per-item authoritative result/diff/reason
→ durably reconcile result locally, then acknowledge the pending operation
```

Prefer per-item results unless a group is intentionally one atomic business operation.

Batch limits are bounded by item count **and encoded bytes**.

Unchanged dependency evidence may avoid unrelated fact loads or calculations inside `ValidateAndCommit`; it never authorizes the server to persist a client-computed state transition. `ConvergentMerge` and `BoundedDelegation` require operation-specific invariant and failure proofs rather than being generic conflict fallbacks.

## 6. Revision/dependency validation

Do not use one magic global revision that invalidates every operation.

An operation records only the dependency tokens material to its provisional decision. Capability rules/decisions should expose enough dependency metadata to determine what becomes invalid when a fact changes.

Example:

```text
CreditApprovalDecision depends on:
- Customer.CreditState revision
- Order.Total
- Payment.Term
- current authorization
```

A change to unrelated catalog metadata must not automatically force all credit logic to reload. A changed credit-state dependency does require the affected decision to be re-evaluated.

Mandatory server-only checks such as current authorization, tenant isolation and strict shared invariants still run even when all client revision tokens match.

## 7. LocalDecisionEvidence is evidence, not authority

A Workstation may persist/send bounded `LocalDecisionEvidence` with:

- OperationId;
- rule/feature/config revisions used;
- dependency revisions;
- input/decision hashes where useful;
- provisional decision values needed for reconciliation/explanation.

The server treats it as **untrusted optimization and explainability evidence**. An authoritative `OperationReceipt` is a separate server-owned record/result created by admission.

Forging or modifying local decision evidence must never bypass current authorization, server-required facts, concurrency, constraints or domain invariants.

## 8. Authorization snapshot versus current OpenFGA state

The Workstation may keep an effective permission snapshot for local UX/offline eligibility.

It can include:
- SquiFlow `TenantAuthorizationRevision`;
- relevant OpenFGA authorization-model context/version identifier for diagnostics/compatibility;
- effective local permission summary used by UX.

It is never a server capability token.

On reconnect the server checks current OpenFGA authorization again. A local snapshot cannot override:
- revoked role/relationship;
- removed tenant membership;
- changed entitlement/capability ceiling;
- changed resource relationship;
- platform/tenant isolation boundary.

## 9. Permission change while offline

Example:

```text
Staff creates local order offline under permission snapshot R17
Owner removes orders.create through an authoritative tenant-admin surface
OpenFGA revocation applies
TenantAuthorizationRevision becomes R18
Staff reconnects
```

The server must not accept the operation merely because the old snapshot was valid when created.

Return `AuthorizationChanged`/review according to command semantics while preserving local intent/evidence. An authorized actor may need to recreate/approve the action online rather than silently losing it.

## 10. OpenFGA dependency failure during sync

Distinguish:
- explicit deny;
- provider unavailable/timeout;
- authorization model/config mismatch;
- ambiguous/reconciliation state after an authorization mutation.

Provider failure must not become `allowed=true`.

For actor-authorized pending commands, inability to obtain required current authorization results in retryable/degraded/fail-closed behavior rather than guessed permission state.

## 11. Semantic sync versus attachment transfer

Large file transfer must not starve small business sync.

Use separate enough scheduling/resource limits that:
- semantic operation batches remain responsive;
- attachment transfers use bounded concurrency;
- bandwidth/provider budgets are respected;
- large transfers resume/retry where supported;
- transfer backlog/age is observable.

The semantic outbox references staged attachment metadata rather than embedding giant bytes.

Server-side retained object transfer goes through `IObjectStore`; the Workstation does not depend on provider-specific object-storage APIs.

## 12. Response-loss case

The server can commit and lose the response.

Retrying the same semantic operation with the same idempotency key returns `AlreadyApplied`/the previous semantic result rather than duplicating the order/payment/etc.

Same idempotency identity with changed intent is rejected.

## 13. Remote changes

```text
request changes after cursor
→ receive authorized scoped changes
→ local transaction:
     apply remote changes
     reconcile provisional/authoritative projection where applicable
     advance cursor
→ commit
```

Never advance cursor independently of durable local apply.

If local disk cannot safely stage/apply the next batch, stop before corrupting durable local work and surface storage recovery state.

## 14. Conflict policy is per aggregate

- Customer/contact: merge/version where safe.
- Product/catalog: server-authoritative/versioned as appropriate.
- Inventory: transactional authoritative operation; no global LWW.
- Order: semantic operation + expected version; may return Accepted/Adjusted/Conflict/Rejected.
- Payment: immutable/idempotent effect + reconciliation.
- Credit: current authoritative exposure.
- Quotation draft: version/merge/manual review where defined.
- Published quotation: immutable revision.
- Roles/permissions: OpenFGA/server authority; never offline-authoritative.
- Rules/workflow publication: server authority.
- Device enrollment/revocation: server authority.
- Attachments: immutable object identity + metadata version.

## 15. Rules/facts while offline

Rule/workflow/feature snapshots can be stale just like permissions.

The Workstation may use compatible published snapshots for provisional/local processing according to the fact/rule authority class and feature offline policy.

On reconnect the server applies the operation's declared admission strategy against current authoritative facts. A `ServerRequired` fact such as current shared credit/stock/security context cannot become authoritative merely because a local evaluation once succeeded.

## 16. Web versus Workstation

Web is normally online and reaches the authoritative application path directly through interactive Web/API ingress.

Workstation semantic synchronization enters through the dedicated Sync ingress workload when that split is implemented.

Both reach the same Capability Core/server authority. Do not maintain separate Web and Sync business implementations.

See:
- `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`;
- `docs/architecture/WEB_AND_SYNC_INGRESS.md`.

## 17. Supported offline window, tombstones and compaction

The protocol declares a supported incremental-history window based on actual retention/capacity policy.

If a cursor is older than retained safe history, return explicit resnapshot/upgrade recovery rather than pretending incremental sync is complete.

Do not silently drop deletion/merge evidence because tombstones were compacted.

Exact retention duration is a product/deployment decision; do not promise forever history by default.

## 18. Resnapshot/rebase recovery

A resnapshot is not `delete local DB and download server state` when unsynced local work exists.

Preserve:
- pending semantic OperationEnvelopes/local intent;
- staged/unsynced attachment references;
- conflict/review evidence;
- account/device/store identity needed to reassociate safely.

Then:

```text
preserve/export pending local intent
→ reauthenticate if needed
→ obtain authorized current server snapshot
→ refresh current permission/feature/rule snapshots
→ rebuild/upgrade local authoritative mirror
→ rebase/review pending operations using current dependency state
→ resume sync
```

Silent local-work deletion is prohibited.

## 19. Long-offline recovery

A client may return with:
- expired session/device credentials;
- revoked/suspended device;
- old protocol/local schema;
- old permission/rule/feature/config snapshots;
- compacted tombstones;
- remotely deleted/merged entities;
- large outbox;
- missing staged attachment;
- insufficient local disk.

Recovery can be reauth, upgrade, resnapshot, rebase, conflict review or export/repair.

Never silently delete pending user work.

## 20. Local capacity and Guard interaction

Track:
- pending operation count/bytes/oldest age;
- staged attachment bytes;
- local DB size/free space;
- retry/conflict/rejected items requiring user action;
- Workstation/Guard diagnostic/update temp usage where application-controlled.

When space is low:
- preserve already committed work;
- stop optional large files/work before total exhaustion;
- allow export/support recovery;
- never discard old unsynced intent merely to shrink the queue.

Guard can surface/recover process lifecycle and resource pressure, but it does not delete business outbox rows or acknowledge sync results.

## 21. Backpressure and constrained bandwidth

When reconnecting many clients/large backlogs:
- bounded batch count/bytes;
- `Retry-After`/backoff/jitter;
- tenant/device fairness;
- Sync API admission control;
- DB work budget;
- OpenFGA/ZITADEL dependency-call budgets;
- network transfer budget;
- no tight reconnect loop.

Sync ingress scales/throttles independently from ordinary interactive Web/API traffic when the host split is implemented.

## 22. Device and sync visibility

Synchronization health is a tenant capability, not merely a green network indicator.

Expose safe states such as last successful semantic sync, backlog age/count, conflict/retry state, device authorization/revocation, app/protocol compatibility and attachment backlog according to current permission.

Workstation can show detailed current-device local evidence. Web can show authorized tenant-wide device/sync inventory without receiving unrestricted local diagnostics.

Detailed owner: `docs/workstation/DEVICE_AND_SYNC_MANAGEMENT.md`.

## 23. Verification obligations

Prove at minimum:

- local business projection + OperationEnvelope/outbox commit atomically in SQLite;
- killing Workstation after commit but before wake does not lose pending work;
- restart discovers pending work without prior process memory;
- duplicate upload cannot duplicate the semantic business effect;
- a forged receipt/revision cannot bypass server authority;
- unchanged dependency revisions can use the safe fast-admission path;
- changed dependency revisions trigger affected decision re-evaluation plus mandatory checks;
- `Adjusted` results reconcile atomically without losing original audit/intent evidence;
- rejection/authorization-change preserves local intent until a defined recovery outcome is durable;
- large attachments cannot starve semantic sync;
- WebApi and SyncApi paths preserve the same authoritative business semantics.
