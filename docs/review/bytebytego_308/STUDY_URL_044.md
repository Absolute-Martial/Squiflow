# URL 044 — What Are the Differences Among Database Locks?

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: a comparison, checklist, pattern catalog, popularity claim, maturity ladder, or source diagram never selects SquiFlow architecture by itself. The review must first identify what SquiFlow is actually doing at the corresponding boundary, why that mechanism exists, what authority it owns, what it costs, and what evidence would justify changing it.

## A. Identification

- **URL occurrence:** `044`
- **PDF page:** `288`
- **Source URL:** `https://blog.bytebytego.com/p/ep118-what-are-the-differences-among`
- **Source access:** public newsletter section accessible.
- **Related supplied visual:** archive page 202 SQL execution visual (not a lock taxonomy diagram).
- **Visual inspected:** PDF page `288` at full size.

## B. Core concept

### SOURCE

The accessible section lists shared, exclusive, update, schema, bulk-update, key-range, row-level, page-level and table-level locks. It describes read/write compatibility, metadata protection, phantom/range protection, bulk behavior and granularity trade-offs between concurrency and lock-management overhead.

### INFERENCE

The source is useful as a vocabulary survey, but the SquiFlow decision should start from the invariant and the selected database's actual concurrency model rather than choosing a lock type from the list.

### EXTERNAL KNOWLEDGE / CAVEAT

Lock names and semantics are database-specific. The source taxonomy is strongly reminiscent of SQL Server terminology; PostgreSQL relies heavily on MVCC plus table/row/advisory/predicate-style mechanisms and does not expose the same lock set in the same way. `Key-range lock prevents phantom reads` is not a provider-neutral implementation statement. Stronger isolation can use predicate locking/serialization detection rather than explicit key-range locks. Deadlocks can occur at any granularity when acquisition order conflicts.

## C. Important concepts

- optimistic version checking;
- MVCC and visibility;
- shared/exclusive compatibility;
- row/table/schema/advisory locks;
- predicate/range/phantom protection;
- lock granularity and escalation/provider behavior;
- deadlock and lock ordering;
- serialization failures;
- short transaction boundaries;
- whole-transaction bounded retry under semantic idempotency;

## D. Diagram / visual explanation

The PDF reuses the SQL-query execution visual showing a storage-engine Lock Manager. It does not illustrate the nine listed lock types. The correct SquiFlow takeaway is simply that locking is one storage-engine concurrency mechanism beneath application/domain semantics; the infographic is not evidence to choose SQL Server-style lock modes or table-level locking.

## E. How it works — step by step

1. Name the invariant: e.g. prevent lost update, enforce unique issuance, serialize a hot counter, protect a multi-row allocation.
2. Use the simplest mechanism that expresses it: expected version, DB constraint, atomic conditional update, or transaction isolation.
3. If a lock is required, use the selected provider's actual supported mechanism and keep transaction duration bounded.
4. Define stable acquisition order where multiple resources are locked.
5. Classify deadlock/serialization/timeout separately from permanent business conflicts.
6. Retry the whole transaction only when provider semantics say retry is safe and under the command's semantic idempotency budget.
7. Measure contention under real reconnect/import and interactive concurrency.

## F. Why it matters

SquiFlow has collaborative edits, offline reconnect bursts and business invariants where silent last-write-wins is unacceptable. But globally pessimistic locking would create unnecessary blocking on a small rack. The current design therefore uses optimistic versions/constraints by default and escalates only for a specific invariant.

## G. Trade-offs / limitations

Optimistic control minimizes blocking but can increase retries under contention. Pessimistic locking can simplify a hot invariant but creates wait/deadlock/throughput risk. Stronger isolation can protect cross-row predicates but increases aborts or blocking. Provider-specific lock behavior must be measured, not assumed.

## H. Alternatives / comparisons — fit, not winner/loser

```text
expected version / ETag
    -> ordinary collaborative lost-update protection

unique/check/FK constraint
    -> database-owned invariant

atomic conditional update
    -> compact state transition / counter-like invariant

row/advisory/other provider lock
    -> specific hot/multi-step critical section

serializable / stronger isolation
    -> predicate/multi-row invariant when justified

single-thread/global lock
    -> generally too coarse unless a tiny, proven critical resource requires it
```

## I. Real implementation considerations

Every adoption/change is required to state its owner/authority, failure behavior, recovery path, implementation evidence and operating burden. A source list is not implementation evidence.

### Implications for the Current Implementation

- **KEEP:** expected-version optimistic concurrency as ordinary edit contract, DB constraints for database-owned invariants, short transactions.
- **KEEP:** locks/isolation are selected per invariant, not globally.
- **IMPROVE NOW (Phase-3 implementation gate):** prove lost-update, concurrent state transitions, deadlock/serialization/lock-timeout classification and bounded whole-transaction retry against the real DB candidate.
- **NEEDS MEASUREMENT:** provider-specific lock/isolation choice for hot stock/numbering/allocation cases and reconnect/import contention.
- **LATER / SCALE TRIGGER:** advisory/range/stronger isolation only when a real invariant cannot be safely expressed with simpler mechanisms.
- **AVOID:** copying the source's lock names as PostgreSQL design requirements, long transactions across external calls, or one global pessimistic-lock rule.

**What are we actually doing and why?** We are using optimistic expected versions, constraints and atomic transactional operations as the ordinary concurrency toolbox because they protect current SquiFlow invariants without unnecessary global blocking. We would introduce stronger provider-specific locking/isolation when a named invariant and measured contention prove the simpler mechanism insufficient.

**Implementation-evidence status:** the repository is still documentation/planning only at the root (no application source tree committed). These are accepted design requirements and future verification gates, not claims that the controls/behavior already exist in running code.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What problem do shared versus exclusive locks solve?
2. How does lock granularity affect concurrency and overhead?
3. Why are lock taxonomies provider-specific?

**Critical reasoning**

1. Which SquiFlow invariants need version checks versus DB constraints versus stronger locking?
2. Why should reconnect/import bursts be part of lock testing?
3. What makes a serialization failure different from a permanent workflow conflict?
4. Why must external provider calls stay outside avoidable DB lock windows?
5. When could advisory locking be appropriate and what failure mode does it introduce?

**Trade-off**

1. At what contention level does optimistic retry become worse than bounded pessimistic locking?
2. When does serializable isolation buy a real invariant rather than unnecessary cost?
3. How should lock ordering be designed across multi-resource operations?

**Failure / edge**

1. Two Workstations approve the same quote version concurrently. What happens?
2. Two transactions deadlock during stock allocation. What is retried?
3. A process dies while holding a DB lock. What does the DB guarantee?
4. One transaction holds a lock while an external payment call stalls. Why is this dangerous?

**Implementation**

1. Which provider metrics expose lock waits/deadlocks/serialization aborts?
2. How are conflicts mapped to stable API/sync outcomes?
3. What whole-transaction retry budget is allowed?
4. What tests prove no silent lost update?

**System design interview**

1. Protect a unique issued-document number under concurrent requests.
2. Design stock allocation under low contention today and explain the trigger for stronger locking later.

**Challenge**

1. A reconnect storm makes optimistic stock updates conflict heavily. Compare version retries, atomic conditional updates, row locks, and stronger isolation based on the invariant and measured contention rather than choosing a universal lock mode.
