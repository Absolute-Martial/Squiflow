# Dual Processing, Server Authority, and Initial In-Process Coordination

**Status:** Accepted implementation direction.  
**Applies from:** v0.0.18 baseline.  
**Authority boundary:** This record clarifies the accepted implementation mechanism for Workstation/server dual processing and the initial process-local coordination mechanism. It does not replace the detailed authority rules in `docs/sync/SYNC_AND_AUTHORITY.md`, the Workstation local-first contract, the rule/fact authority classes, or the Worker lifecycle in `docs/server/CORE_API_AND_WORKER.md`.

## 1. Decision summary

SquiFlow intentionally performs some business processing twice when the Workstation is offline-capable:

```text
Workstation/local execution
= immediate user feedback
+ local business-rule evaluation where allowed
+ local durable transaction
+ offline continuity

Server execution
= current authentication/device/session validation
+ authoritative TenantContext
+ current OpenFGA authorization
+ current feature/configuration/rule/fact state
+ central idempotency/concurrency/limit checks
+ authoritative central transaction
```

The two executions serve different authority levels and are therefore not considered accidental duplicate work.

The initial in-process asynchronous coordination mechanism is **bounded `System.Threading.Channels`**, used only for process-local wake-up/backpressure where useful.

`System.Threading.Channels` is never authoritative durable business state and is never the durable queue for Workstation sync or server background work.

Durable state is committed first to the owning SQLite/PostgreSQL transaction/outbox/job state. A Channel only accelerates discovery of already-durable work.

MassTransit, RabbitMQ, Kafka, and another external/distributed broker are **not baseline** for the initial implementation. They remain replaceable future delivery mechanisms if a measured workload proves that the simple durable-store + Channel design is insufficient.

## 2. Why dual processing is intentional

The Workstation is a trusted SquiFlow user tool but is not a trusted server authority. Assume a local user can inspect or modify local storage, memory, configuration, requests, or even the application process.

Therefore a successful local operation means only that the Workstation accepted and durably recorded the user's intent under the locally available compatible state.

It does **not** prove that current central authority still permits the effect.

Example:

```text
Staff creates an order while offline
→ Workstation validates locally
→ SQLite commits Order + durable local outbox
→ UI shows LocalCommitted / PendingRemote

while offline:
Owner revokes orders.create
or current stock/credit/config/rules change

Workstation reconnects
→ server authenticates current session/device
→ derives current TenantContext
→ checks current feature/permission/OpenFGA state
→ re-evaluates current required rules/facts
→ applies idempotency/concurrency/limit checks
→ commits centrally only if currently valid
```

The server may return `Authoritative`, `Conflict`, `Rejected`, `AuthorizationChanged`, `Retryable`, or `UpgradeRequired` as appropriate.

The local intent/evidence is preserved even when central authority does not accept it.

## 3. Share deterministic policy, not client authority

Where the same deterministic host-independent policy is semantically valid in both places, SquiFlow should reuse the same module/application/domain implementation rather than write unrelated client and server validators.

However, code reuse does not imply fact/authority reuse.

Keep the existing fact/decision distinction:

```text
LocalSafe
= may be decided from local facts for the defined local effect

LocalProvisional
= may be evaluated locally for guidance/offline UX but must be re-evaluated or validated before an authoritative central effect

ServerRequired
= current central fact/authority is required; local state cannot establish final acceptance
```

Examples of state that normally requires current server authority include current tenant membership, sensitive permissions, shared stock/credit exposure, payment/refund authority, provider outcomes, strict hard limits, and another centrally owned fact whose staleness could cause an unsafe business effect.

Do not compare a client-computed result to a server-computed result and call equality proof of authority. The server evaluates the operation from authoritative state and commits its own accepted result.

## 4. Workstation durable flow

For an offline-capable Workstation operation:

```text
BEGIN SQLite TRANSACTION
  apply allowed local business mutation
  write semantic local outbox/change record
COMMIT

best-effort Channel wake signal
```

Correctness depends on the SQLite transaction, not on the signal.

If the process crashes after the durable commit but before the Channel signal:

```text
business state     durable
outbox item        durable
Channel signal     lost
```

nothing important is lost. On startup/recovery/periodic reconciliation, the sync processor discovers the pending durable outbox item again.

If a local transaction fails, writing to a Channel must never make the operation appear durable.

## 5. Server durable-work flow

When server-side durable background work is introduced, use the same principle:

```text
BEGIN PostgreSQL TRANSACTION
  authoritative business mutation where applicable
  idempotency/audit/outbox/job state where co-owned
COMMIT

best-effort Channel wake signal
```

The Worker/processor then claims durable work from PostgreSQL using the accepted job/outbox lifecycle.

The Channel is not the job payload authority. Prefer a small wake signal such as `WorkAvailable` rather than treating an in-memory `Channel<T>` as a second durable state store.

Recovery paths include:

```text
Channel wake
OR process startup scan
OR periodic bounded reconciliation scan
→ claim durable work
→ execute with bounded concurrency
```

A lost wake-up therefore affects latency, not correctness.

## 6. Bounded Channels and backpressure

Use bounded Channels when they carry queued process-local work/signals whose production could otherwise outrun consumption.

The exact capacity and full-mode behavior are workload-specific and measured rather than guessed globally.

For a coalescing `work available` wake signal, a very small capacity may be sufficient because many committed jobs do not require many identical in-memory wake messages.

Do not create one global `Channel<object>` or universal application event bus.

Different workload classes may later require different coordination because they have different cost, concurrency, priority, retry, and fairness characteristics.

## 7. Dependency boundary and replaceability

Application/domain modules must not depend directly on `Channel<T>`, `ChannelReader<T>`, or `ChannelWriter<T>` as business contracts.

Where a replacement seam is genuinely useful, model the narrow responsibility instead of pretending every queue technology has identical semantics.

For example, a process-local wake abstraction may be conceptually equivalent to:

```text
IWorkWakeSignal
  Signal()
  WaitAsync(cancellationToken)
```

Its meaning is:

> durable work may now be available; wake a processor.

It does not mean:

> this interface is the durable message broker.

Likewise, the durable work store owns claim/lease/retry/completion/quarantine semantics. Do not hide these semantics behind a generic `IMessageQueue` merely to make a future broker look interchangeable.

Create an interface only where the concrete replacement/responsibility boundary is real; do not introduce one-interface-per-class ceremony.

## 8. Upgrade path

The expected progression is:

```text
Initial
SQLite/PostgreSQL durable state
+ bounded System.Threading.Channels wake/backpressure
+ SquiFlow-owned processing loops

Later, only if earned
same business semantics
same idempotency identities
same authority model
same Workstation/Web contracts
+ MassTransit or another messaging adapter
+ durable SQL/broker transport where justified

Later still, only if earned
broker/service-bus/stream infrastructure
for proven routing, independent-consumer, replay, throughput,
retention, cross-node coordination, or operational needs
```

A transport/framework change must not force the Workstation or Web to know RabbitMQ, MassTransit, queue names, exchanges, broker addresses, consumer topology, or other server delivery internals.

MassTransit is therefore a **deferred positive candidate**, not an initial dependency. RabbitMQ/Kafka remain non-baseline until broker/stream-specific semantics are demonstrated.

## 9. What does not change

This decision does not change the following accepted architecture:

- ordinary short authoritative business commands remain synchronous when an immediate definitive result is required;
- genuinely long-running/resource-heavy consequences may use durable asynchronous operation/Worker semantics;
- Workstation sync correctness is transport-independent;
- HTTP remains the simple baseline while gRPC remains an evidence-gated candidate for a real boundary;
- Core API independently enforces current authentication, tenant context, authorization, domain/rule/workflow validity, concurrency, idempotency, and relevant limits;
- committed business facts and after-commit events remain different from untrusted client commands/intents;
- at-least-once/retryable delivery still requires semantic idempotency and reconciliation where outcomes can be ambiguous;
- no system-wide `exactly once` claim is introduced;
- no event-driven decomposition is introduced between ordinary in-process modules.

## 10. Verification obligations

### Workstation

Prove that:

- local business mutation + local outbox commit atomically in SQLite;
- killing Workstation after commit but before Channel signaling does not lose pending work;
- restart discovers durable pending work without relying on the prior process memory;
- Channel overflow/backpressure cannot delete already committed outbox work;
- pending work remains after `UpgradeRequired`, authorization change, conflict, or server rejection until the defined user/recovery outcome is persisted;
- changing local SQLite/memory/request payload cannot bypass server authority.

### Server/Worker when implemented

Prove that:

- durable work is committed before a best-effort wake signal;
- lost Channel wake is recovered by startup or periodic bounded reconciliation;
- duplicate wake signals cannot duplicate the semantic business effect;
- bounded Channel/concurrency behavior prevents unbounded process memory growth;
- graceful shutdown/drain does not acknowledge durable work before its completion state is persisted;
- poison/retry/quarantine behavior is owned by durable work state, not by ephemeral Channel contents.

### Dual processing

For representative operations, prove that local acceptance cannot force central acceptance when current permission, rule/configuration, stock/credit, expected version, limit, or another authoritative server fact has changed.

## 11. Revisit triggers

Reconsider the in-process Channel + DB-backed durable-work mechanism when evidence shows a concrete need such as:

- multiple Worker processes/nodes require broker-managed delivery/coordination that the DB claim model handles poorly;
- several independent consumers need separate durable delivery lifecycles;
- durable replay/history/independent offsets become a real requirement;
- sustained throughput or database contention shows PostgreSQL-backed work coordination is the bottleneck;
- routing/topology complexity exceeds the simple owned Worker model;
- delayed delivery/scheduling/consumer middleware requirements make a mature messaging framework materially safer or cheaper than continuing custom implementation;
- operational support/diagnostics/recovery burden is demonstrably lower with a messaging framework/broker;
- MassTransit or another candidate passes dependency, licensing, removal, upgrade, resource, and failure-mode review.

Until one of these triggers is demonstrated, do not add a broker/framework merely to match a reference architecture.

## 12. Related owners

- `docs/sync/SYNC_AND_AUTHORITY.md` — detailed Workstation/server trust and synchronization contract.
- `docs/workstation/LOCAL_FIRST_DESKTOP.md` — Workstation local-first authority classes and durability behavior.
- `docs/rules/NATIVE_RULE_ENGINE.md` — LocalSafe / LocalProvisional / ServerRequired fact semantics.
- `docs/server/CORE_API_AND_WORKER.md` — synchronous/async boundary, durable Worker lifecycle, retries, idempotency, queue/event semantics.
- `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md` — semantic idempotency, response loss, compatibility, retry ownership.
- `docs/decisions/CURRENT_DECISIONS.md` — current accepted direction catalogue.
- `docs/decisions/MATERIAL_DECISION_HISTORY.md` — supersession/history convention.
