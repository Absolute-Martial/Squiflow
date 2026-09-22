# Staged Authority and In-Process Coordination

**Status:** Accepted implementation direction.
**Product version:** v0.1.0.
**Authority boundary:** This record owns the Workstation/server two-stage execution model and the initial process-local coordination mechanism. Detailed trust/sync rules remain in `docs/sync/SYNC_AND_AUTHORITY.md`.

## 1. Decision summary

Retire the shorthand `dual processing`. It suggests two authoritative executions and hides the actual trust boundary.

The accepted model is **one semantic operation with staged authority**:

```text
Workstation
  prepare intent
  + compute provisional projection
  + atomically persist operation/projection/outbox
          │
          │ OperationEnvelope + dependency evidence
          ▼
Server
  deduplicate
  + current authority/fact checks
  + operation-owned admission strategy
  + one authoritative commit
          │
          │ authoritative receipt/change
          ▼
Workstation
  reconcile local projection and pending intent
```

The Workstation may run host-independent deterministic logic to remain responsive/offline, but that computation prepares the operation and its provisional view. The server owns the authoritative business transition. Reusing a deterministic processor on both sides is code reuse across trust stages, not the same transaction being committed twice.

## 2. Shared processor, different facts

Where business meaning is identical, reuse one Capability Core decision processor:

```text
Intent + Facts + Rule/Policy Snapshot
              ↓
       Capability Core
              ↓
           Decision
```

The Workstation and server differ primarily in fact source and authority:

```text
Workstation facts                  Server facts
SQLite/local snapshots             PostgreSQL/current state
published local rule snapshot      current effective rules
local permission snapshot (UX)     current OpenFGA authority
local known stock/credit            authoritative shared stock/credit
```

Code reuse never converts local facts into central authority.

## 3. Operation envelope, not database duplication

The Workstation sync record represents the user's semantic intent and the evidence/revisions used during local preparation. It should not embed full copies of unrelated customer/product/rule/database state.

Conceptual envelope:

```text
OperationEnvelope
├── OperationId / IdempotencyKey
├── Tenant/Device identity references
├── Capability + OperationKind + schema version
├── Intent payload
├── Base aggregate/version token where applicable
├── Dependency tokens/revisions used locally
│   ├── RuleSetRevision
│   ├── PricingRevision
│   ├── CustomerRevision
│   └── other operation-specific facts
├── provisional Decision/Result hash where diagnostically useful
└── correlation/causation metadata
```

Large staged files are referenced rather than copied into the semantic envelope.

The local business projection and outbox/envelope still commit atomically in SQLite where the local operation is allowed.

## 4. Operation-owned admission strategy

On receipt, the server always performs trust-boundary checks that cannot be delegated to a client:

- authenticate session/device as required;
- derive authoritative TenantContext;
- current authorization/OpenFGA checks;
- tenant isolation;
- idempotency/deduplication;
- protocol/schema compatibility;
- critical security/domain invariants;
- database concurrency/constraints;
- server-required facts such as authoritative inventory/credit/payment state where applicable.

After those checks, the owning operation applies the narrowest strategy that preserves its named invariants:

| Strategy | Meaning |
|---|---|
| `ServerRequired` | No provisional business success; current server/external authority is needed before the action can succeed |
| `ValidateAndCommit` | Validate the semantic intent against current authoritative facts and compute the authoritative transition |
| `ExpectedRevision` | Apply only to the expected aggregate revision, then accept, report conflict, or invoke a defined merge/rebase policy |
| `ConvergentMerge` | Merge only where the operation has evidence that concurrent valid states remain valid after merge |
| `BoundedDelegation` | Verify and consume a previously issued, limited, expiring offline grant such as a reserved range or quantity |

The strategy is part of the capability contract and evidence. It is not selected dynamically by transport middleware.

For `ValidateAndCommit`, the server may compare dependency/revision evidence against current authoritative revisions. If all relevant dependencies remain current, it can avoid reloading or recomputing unrelated decisions. This is a **bounded admission optimization**, not acceptance of a client-computed transition.

```text
semantic operation
  ↓
trust checks
  ↓
revisions still current?
  ├─ yes → mandatory current invariants/concurrency → compute and commit
  └─ no  → load affected facts → compute → commit/adjust/reject/conflict
```

`ConvergentMerge` and `BoundedDelegation` require separate capability-level proof. Neither is a generic fallback when revision validation fails.

## 5. Dependency-driven re-evaluation

Rules/decisions should expose enough dependency metadata to identify what can become invalid when a fact changes.

Example:

```text
CreditApprovalDecision depends on:
- Customer.CreditState
- Order.Total
- Payment.Term
- current authorization
```

If only `CatalogRevision` changed and the operation does not depend on changed catalog facts, do not re-evaluate unrelated credit/approval decisions solely because `something changed`.

If `CustomerCreditRevision` changed, re-evaluate decisions that depend on it.

The dependency graph must remain explicit/bounded enough to test; do not create a magical global revision that invalidates every operation.

## 6. Local execution evidence

The Workstation may persist/send bounded `LocalDecisionEvidence` describing how the provisional projection was produced:

```text
LocalDecisionEvidence
├── OperationId
├── Rule/feature/config revisions used
├── input/decision hashes where useful
├── dependency revision tokens
└── provisional decisions relevant to reconciliation/explanation
```

The server treats this as **untrusted optimization/explainability evidence**, not authority. The term `receipt` is reserved for the stable server result returned after authoritative admission.

Its benefits are:

- avoid needless reprocessing when dependencies are unchanged;
- explain why the local and authoritative result differ;
- support deterministic support diagnostics;
- preserve exactly which published local snapshot produced the provisional result.

## 7. Authoritative receipts and outcomes

The server stores/returns an authoritative receipt keyed by `OperationId`. Repeating equivalent intent returns the stored outcome; reusing the same identity for changed intent is rejected. Do not classify every difference as a generic conflict. The server may return:

```text
Accepted      authoritative result matches/accepts the operation
Adjusted      operation is valid but authoritative values differ
Rejected      current authority/business state cannot accept it
Conflict      user/business reconciliation is required
Retryable     dependency/provider/server state is temporarily unavailable
AuthorizationChanged
UpgradeRequired
AlreadyApplied
```

An `Adjusted` result includes a bounded authoritative diff/reason so the Workstation can reconcile its local projection atomically. The Workstation removes/acknowledges the pending outbox item only after durably applying this receipt or the corresponding authoritative change.

## 8. Web execution

The Web host is normally online and enters the same authoritative capability directly:

```text
Web UI → WebApi → authoritative application path → Capability Core → PostgreSQL
```

It does not need a duplicate `WebBusiness` implementation.

The Workstation prepares a provisional operation only for capabilities explicitly allowed to do so. Security/admin/global operations such as staff permission assignment or device revocation remain server-authoritative even when initiated from the Workstation UI.

## 9. Durable local flow

For an offline-capable operation:

```text
BEGIN SQLite TRANSACTION
  write semantic operation intent
  apply allowed provisional projection
  write semantic OperationEnvelope/outbox state
COMMIT

best-effort bounded Channel wake signal
```

Correctness depends on SQLite, not the Channel.

A crash after commit but before wake loses only latency; startup/periodic reconciliation discovers the durable pending operation again.

## 10. Server durable-work flow

When accepted server work has asynchronous consequences:

```text
BEGIN PostgreSQL TRANSACTION
  authoritative mutation
  idempotency/receipt/outbox/job state where co-owned
COMMIT

best-effort process-local wake signal
```

The durable work store owns claim/retry/completion/quarantine semantics. `System.Threading.Channels` is only a bounded process-local coordination/wake/backpressure mechanism.

## 11. Bounded Channels

`System.Threading.Channels` remains the initial process-local mechanism where useful.

Rules:

- bounded capacity where production can outrun consumption;
- no global `Channel<object>` event bus;
- application/domain contracts do not depend on Channel types;
- Channel contents are never the durable business/job/sync authority;
- startup/periodic bounded scans recover lost wake signals.

A narrow wake abstraction may exist when it represents a real replacement boundary, e.g. `durable work may now be available`.

## 12. Transport/framework position

MassTransit, RabbitMQ, Kafka and another broker are not baseline merely because SquiFlow may scale later.

Expected progression:

```text
SQLite/PostgreSQL durable state
+ bounded Channels/process-local coordination

then only if earned:
MassTransit or another delivery adapter

then only if earned:
external broker/stream platform for proven routing/replay/consumer/throughput needs
```

Transport changes must not change Capability Core business semantics or leak queue topology into Workstation/Web contracts.

Database changesets and local-database sync engines can be evaluated later as transport/projection mechanisms. They do not replace semantic intent, current authorization, capability admission or authoritative receipts. CRDT/convergent merge is limited to operations proven safe for their invariants; it is not the product-wide consistency model. Expected revisions remain the ordinary edit mechanism for non-mergeable aggregate changes.

## 13. Industry mechanism comparison

The selected contract is a semantic operation/outbox plus server-owned admission and receipt. Adjacent mechanisms remain narrower tools:

| Mechanism | Useful contribution | Why it does not replace the selected contract |
|---|---|---|
| SQLite Session Extension | Records and applies row changesets | Requires compatible table shape/base state and an application conflict handler; row deltas do not express current authorization or business intent |
| PowerSync-style local SQLite sync | Local persistence, upload queue, partial server-to-client replication and retry | Client CRUD still goes through an application backend that can modify, persist or deny it; capability admission remains application-owned |
| CRDT/Automerge-style merge | Convergent collaboration for suitable data | Concurrent property changes can still expose conflicts, and convergence alone does not prove payments, stock, credit, permissions or hard limits remain valid |
| Expected revision / optimistic concurrency | Detects stale aggregate edits without a long-lived lock | Conflict resolution remains operation-specific; it does not provide offline durability or synchronization transport |
| Bounded delegated authority | Permits a narrowly limited offline use of a pre-issued resource | Adds issuance, expiry, exhaustion, replay and reconciliation obligations and cannot become general client authority |

The proof boundary for coordination-free merge follows invariant confluence: if independently invariant-preserving operations can merge into an invalid state, that operation needs coordination or authoritative admission. Sources informing this comparison are the [SQLite Session Extension](https://www.sqlite.org/sessionintro.html), [PowerSync upload/checkpoint flow](https://powersync.com/blog/checkpoint-requests-client-synced-now), [Automerge conflict behavior](https://automerge.org/docs/reference/documents/conflicts/), [Coordination Avoidance in Database Systems](https://www.vldb.org/pvldb/vol8/p185-bailis.pdf), [RFC 9110 `If-Match`](https://www.rfc-editor.org/rfc/rfc9110.html#section-13.1.1), and [EF Core optimistic concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency). They are comparison evidence, not admitted dependencies.

## 14. Verification obligations

Prove at minimum:

- Workstation mutation + OperationEnvelope/outbox commit atomically;
- lost Channel wake does not lose pending work;
- duplicate upload cannot duplicate semantic business effect;
- local storage/request tampering cannot bypass server authority;
- unchanged dependency revisions avoid only unrelated work while the server still computes/commits the authoritative transition;
- changed dependency revisions re-evaluate only affected decisions plus mandatory server checks;
- a forged `LocalDecisionEvidence` cannot bypass current authorization/domain/concurrency checks;
- every offline-capable operation declares and proves its admission strategy;
- a `ConvergentMerge` operation preserves its named invariants under concurrent valid changes;
- a `BoundedDelegation` operation cannot exceed, duplicate, outlive or cross the scope of its grant;
- a stored authoritative receipt makes response-loss retry deterministic for the same operation identity and intent;
- `Adjusted` authoritative results reconcile locally without losing the original operation/audit evidence;
- `Rejected`/`Conflict`/`AuthorizationChanged` preserve the local intent until the defined user/recovery outcome is durably recorded;
- Web and Sync ingress paths reach the same authoritative Capability Core semantics rather than separate business implementations.

## 15. Related owners

- `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`
- `docs/architecture/WEB_AND_SYNC_INGRESS.md`
- `docs/sync/SYNC_AND_AUTHORITY.md`
- `docs/workstation/LOCAL_FIRST_DESKTOP.md`
- `docs/rules/NATIVE_RULE_ENGINE.md`
- `docs/server/CORE_API_AND_WORKER.md`
- `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`
