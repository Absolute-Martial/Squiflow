# URL 034 — How Databases Keep Their Sanity with Concurrency Control

## Review method

This occurrence is reviewed independently from earlier archive/URL material. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: no comparison, pattern catalog, popularity claim, or source diagram selects architecture by itself.

## A. Identification

- **URL occurrence:** `034`
- **PDF page:** `278`
- **Source URL:** `https://blog.bytebytego.com/p/how-databases-keep-their-sanity-with`
- **Source access:** paid article with public preview; no paywall bypass.
- **Related supplied visual:** archive page `202`, SQL query execution pipeline.
- **Visual inspected:** PDF page `278` at full size.

## B. Core concept

### SOURCE

The preview demonstrates a lost update using two concurrent withdrawals: both transactions can be individually correct yet overlap and produce the wrong final balance. It says concurrent overlap is normal. The visible outline covers four corruption modes, pessimistic locking, optimistic locking, techniques that reduce reader/writer blocking, isolation levels, and stronger settings intended to remain practical under load.

### INFERENCE

Concurrency correctness must be designed around the business invariant, not merely around whether one SQL statement succeeds. The correct mechanism depends on contention, conflict semantics, transaction shape and provider behavior.

### EXTERNAL KNOWLEDGE / CAVEAT

MVCC can reduce reader/writer blocking but does not automatically prevent every write-skew/lost-update anomaly. Optimistic version checks, atomic conditional updates, constraints, isolation levels and explicit locks protect different invariants. Serializable isolation is powerful but is not free and can abort transactions that applications must retry safely.

## C. Important concepts

- lost update and write skew;
- optimistic expected-version checks;
- pessimistic row/range/advisory locking;
- atomic conditional updates;
- unique/check/foreign-key constraints;
- MVCC/read snapshots;
- isolation levels;
- serialization/deadlock/lock-timeout errors;
- transaction retry boundaries;
- stable lock ordering;
- contention measurement.

## D. Diagram / visual explanation

The related SQL execution visual shows transport, query processor, execution engine and storage engine with transaction/lock/buffer/recovery managers. It correctly reminds us that concurrency is a storage-engine/transaction concern too, but it does not tell SquiFlow which isolation level or lock type to use for a particular invariant.

## E. How it works — step by step

1. name the invariant (for example quote version, stock decrement, unique issued number, refund state);
2. identify all concurrent writers that can violate it;
3. prefer a simple DB-owned constraint or atomic conditional update where it fully expresses the invariant;
4. use expected-version optimistic concurrency for ordinary collaborative edits;
5. use stronger isolation/locks only when the invariant cannot be safely expressed more simply;
6. keep transactions short and avoid external calls/user waits while locks are held;
7. classify deadlock/serialization/lock-timeout separately from permanent business conflict;
8. retry the whole transaction only when the provider failure is genuinely retryable and the command is semantically idempotent;
9. measure conflicts/lock waits during normal and reconnect/import bursts on the selected provider/hardware.

## F. Why it matters

SquiFlow has concurrent Web/Workstation/Worker actions and offline reconnect bursts. A global last-write-wins policy could silently erase legitimate business intent, while blanket pessimistic locking could destroy throughput on constrained hardware.

## G. Trade-offs / limitations

Optimistic concurrency has low blocking but shifts conflict handling to the application and can fail often under hot contention. Pessimistic locking can protect hot invariants predictably but creates waits/deadlocks and long-transaction risk. Stronger isolation reduces anomalies but may increase aborts/resource cost. Application locks can be flexible but are easy to misuse if the database invariant can be expressed directly.

## H. Alternatives / comparisons — fit, not winner/loser

```text
DB constraint
    -> invariant that the database can state directly

atomic conditional update
    -> small state transition/counter-style invariant

optimistic expected version
    -> ordinary collaborative aggregate editing

pessimistic row/range lock
    -> hot invariant where conflicting writers must serialize

serializable/stronger isolation
    -> multi-row invariant requiring transaction-wide protection when measured/verified

application/advisory lock
    -> only when provider/domain coordination requires it and failure semantics are explicit
```

## I. Real implementation considerations

Provider-specific concurrency semantics must be tested with the real central DB candidate. Test simultaneous commands, contention, deadlocks, serialization failures, reconnect bursts, retry budget, transaction duration and user-visible conflict mapping.

### Implications for the Current Implementation

- **KEEP:** expected-version/optimistic concurrency is the ordinary collaborative edit contract.
- **KEEP:** constraints and atomic conditional updates protect DB-owned invariants where they fit.
- **KEEP:** stronger isolation/locks are invariant-specific, not global defaults.
- **KEEP:** retry the whole transaction for genuine retryable serialization/deadlock failures under semantic idempotency.
- **IMPROVE NOW (Phase-3 implementation proof):** demonstrate actual lost-update/write-skew cases and the selected protection on the real DB candidate.
- **NEEDS MEASUREMENT:** contention and isolation choice for hot stock/payment/numbering paths must come from representative workload evidence.
- **AVOID:** global last-write-wins for aggregates.
- **AVOID:** holding database locks across user interaction or avoidable provider calls.
- **AVOID:** retrying arbitrary fragments of a failed transaction.

**What are we actually doing and why?** We use expected versions plus database constraints/transactions because they protect specific SquiFlow invariants with less coordination than blanket locking. We escalate only when a concrete invariant and contention pattern requires it.

**What would falsify/change this?** If optimistic conflicts become frequent for one hot resource or a multi-row invariant cannot be safely protected with constraints/conditional writes, stronger locking/isolation may become the better fit for that operation.

### Critical interrogation — answers intentionally withheld

**Foundation**
1. How can two correct transactions produce an incorrect final state?
2. What is the practical difference between optimistic and pessimistic conflict control?
3. Why does MVCC not mean “no concurrency anomalies”?

**Critical reasoning**
1. Which SquiFlow invariants are single-row, aggregate-level, and multi-row?
2. Why is expected-version conflict different from a deadlock?
3. When is a DB constraint stronger/simpler than an application lock?
4. Which reconnect workloads could create hot contention?
5. Why must a serialization retry re-execute the whole transaction?

**Trade-off**
1. When would row locking be better than optimistic versions?
2. When is serializable isolation worth its abort/throughput cost?
3. What user experience should a legitimate optimistic conflict produce?

**Failure / edge**
1. Two Workstations sell the final stock simultaneously.
2. A transaction deadlocks after an idempotency receipt is tentatively inserted.
3. Process crashes while holding a DB lock.
4. A provider call is made while a row lock is held and takes 30 seconds.

**Implementation**
1. What SQL/provider primitive proves expected version atomically?
2. How are deadlock/serialization errors mapped to stable SquiFlow codes?
3. How are lock waits/aborts observed without high-cardinality metrics?
4. What retry budget applies during reconnect storms?

**System design interview**
1. Design concurrency control for quote approval and inventory decrement.
2. Explain why no single isolation strategy should be applied to every SquiFlow transaction.

**Challenge**
An inventory aggregate has frequent conflicts during morning reconnect storms. Determine whether to keep optimistic versions, use atomic decrements, row locks, or stronger isolation, and specify the measurement/failure evidence required.
