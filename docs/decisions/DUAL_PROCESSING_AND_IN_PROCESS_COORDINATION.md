# Provisional Execution, Authoritative Admission, and In-Process Coordination

**Status:** Accepted implementation direction.  
**Applies from:** v0.0.18 baseline.  
**Authority boundary:** This record owns the Workstation/server two-stage execution model and the initial process-local coordination mechanism. Detailed trust/sync rules remain in `docs/sync/SYNC_AND_AUTHORITY.md`.

## 1. Decision summary

The old shorthand `dual processing` must not be interpreted as `store the same state twice and blindly execute the same transaction twice`.

The accepted model is:

```text
Workstation
  provisional execution
  + local durable projection
  + semantic operation envelope
          │
          ▼
Server
  authoritative admission
  + current authority checks
  + revision/dependency validation
  + selective re-evaluation where invalidated
  + authoritative commit
```

The Workstation executes enough host-independent business logic to remain responsive/offline. The server does not trust the Workstation as authority and does not merely accept its computed result. It admits the submitted operation against current authoritative facts.

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

The Workstation sync record represents the user's semantic intent and the evidence/revisions used during provisional execution. It should not embed full copies of unrelated customer/product/rule/database state.

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

## 4. Server admission fast path

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

It then compares the operation's dependency/revision evidence against current authoritative revisions.

If all relevant dependencies remain current, the server may use a **fast admission path** rather than reloading/reconstructing unrelated state.

```text
operation
  ↓
trust checks
  ↓
revisions still current?
  ├─ yes → server-required invariants/concurrency → commit
  └─ no  → determine affected decisions → selective re-evaluation → commit/adjust/reject
```

This is an optimization contract, not a security shortcut.

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

## 6. Execution receipt

The Workstation may persist/send a bounded `ExecutionReceipt` describing how the provisional decision was produced:

```text
ExecutionReceipt
├── OperationId
├── Rule/feature/config revisions used
├── input/decision hashes where useful
├── dependency revision tokens
└── provisional decisions relevant to reconciliation/explanation
```

The server treats the receipt as **untrusted optimization/explainability evidence**, not authority.

Its benefits are:

- avoid needless reprocessing when dependencies are unchanged;
- explain why the local and authoritative result differ;
- support deterministic support diagnostics;
- preserve exactly which published local snapshot produced the provisional result.

## 7. Authoritative outcomes

Do not classify every difference as a generic conflict. The server may return:

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

An `Adjusted` result includes a bounded authoritative diff/reason so the Workstation can reconcile its local projection atomically.

## 8. Web execution

The Web host is normally online and enters the same authoritative capability directly:

```text
Web UI → WebApi → authoritative application path → Capability Core → PostgreSQL
```

It does not need a duplicate `WebBusiness` implementation.

The Workstation uses provisional execution only for capabilities explicitly allowed to do so. Security/admin/global operations such as staff permission assignment or device revocation remain server-authoritative even when initiated from the Workstation UI.

## 9. Durable local flow

For an offline-capable operation:

```text
BEGIN SQLite TRANSACTION
  apply allowed provisional/local projection
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

## 13. Verification obligations

Prove at minimum:

- Workstation mutation + OperationEnvelope/outbox commit atomically;
- lost Channel wake does not lose pending work;
- duplicate upload cannot duplicate semantic business effect;
- local storage/request tampering cannot bypass server authority;
- unchanged dependency revisions exercise the fast admission path safely;
- changed dependency revisions re-evaluate only affected decisions plus mandatory server checks;
- a forged `ExecutionReceipt` cannot bypass current authorization/domain/concurrency checks;
- `Adjusted` authoritative results reconcile locally without losing the original operation/audit evidence;
- `Rejected`/`Conflict`/`AuthorizationChanged` preserve the local intent until the defined user/recovery outcome is durably recorded;
- Web and Sync ingress paths reach the same authoritative Capability Core semantics rather than separate business implementations.

## 14. Related owners

- `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`
- `docs/architecture/WEB_AND_SYNC_INGRESS.md`
- `docs/sync/SYNC_AND_AUTHORITY.md`
- `docs/workstation/LOCAL_FIRST_DESKTOP.md`
- `docs/rules/NATIVE_RULE_ENGINE.md`
- `docs/server/CORE_API_AND_WORKER.md`
- `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`
