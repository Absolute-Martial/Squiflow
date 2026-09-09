# URL 032 — The Read Path versus the Write Path: Strategies and Techniques

## Review method

This occurrence is reviewed independently from earlier archive/URL material. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: no comparison, pattern catalog, popularity claim, or source diagram selects architecture by itself.

## A. Identification

- **URL occurrence:** `032`
- **PDF page:** `276`
- **Source URL:** `https://blog.bytebytego.com/p/the-read-path-versus-the-write-path`
- **Source access:** paid article with a public preview; no paywall bypass.
- **Related supplied visual:** archive page `318`, Redis query lifecycle.
- **Visual inspected:** PDF page `276` at full size.

## B. Core concept

### SOURCE

The preview distinguishes writes that record facts from reads that answer questions. It gives a progression where a slow read is first helped by an index, later a cache, and later a read replica; each fix is needed at a different time for a different reason. These optimizations place additional copies/representations away from the source, so a user can write a value and temporarily read an older one. The outline covers indexes, denormalization, caching, replicas, materialized views, purpose-built read stores, fan-out on write/read, CQRS, sync mechanisms, staleness windows, and write-heavy systems.

### INFERENCE

Read optimization is often data duplication/precomputation. That means every “faster read” mechanism creates a second question: how does the copy converge, and what happens while it is stale or unavailable?

### EXTERNAL KNOWLEDGE / CAVEAT

Not every additional structure is equally “a copy.” An index maintained transactionally by the DB has different consistency/failure properties from a cache, asynchronous materialized projection, read replica, or search index. Likewise, SquiFlow's Workstation local-first store is not merely a read cache: it can hold durable local user intent in `LocalCommitted/PendingRemote` states before central authority accepts it.

## C. Important concepts

- source of truth versus derived representation;
- transactional index versus asynchronously synchronized copy;
- cache freshness/invalidation;
- replica lag;
- materialized projection rebuild;
- denormalization update burden;
- purpose-built search/read store;
- fan-out on write versus fan-out on read;
- CQRS as responsibility separation, not mandatory separate stores;
- write amplification, WAL/storage/migration cost;
- read-after-write expectations;
- tenant/security scope on every derived copy.

## D. Diagram / visual explanation

The related Redis visual shows a Redis command interacting with main memory plus AOF/RDB persistence. It is not a direct map of the article's entire read/write path. The useful lesson is that even a “cache” product can have its own write/persistence path; choosing Redis therefore requires first knowing whether SquiFlow is using it as disposable cache, session/coordination state, or something more durable.

## E. How it works — step by step

1. define the authoritative write model and invariant;
2. measure the actual read that is too slow/expensive;
3. try the lowest-cost improvement that addresses the measured cause (query shape/index first where appropriate);
4. if a derived copy is introduced, declare source, key, freshness, sync mechanism, authorization, rebuild and outage semantics;
5. never let a stale derived read become current payment/stock/credit/authorization/tenant authority;
6. measure write amplification and recovery cost after the optimization;
7. remove/avoid the copy if its total system cost exceeds its measured read benefit.

## F. Why it matters

SquiFlow is expected to serve interactive Web/Admin reads while also accepting reconnect/import bursts and authoritative transactional writes on constrained hardware. A read optimization that improves one dashboard can still make backlog synchronization, WAL, storage, or recovery materially worse.

## G. Trade-offs / limitations

Faster reads can increase write latency, write amplification, disk, memory, propagation complexity, privacy/deletion paths, and recovery burden. Read replicas can be excellent for tolerant reporting but unsafe for a current authorization decision. Denormalized projections can simplify UI reads but require rebuild/freshness semantics. Caches can reduce load but create stampede and staleness modes.

## H. Alternatives / comparisons — fit, not winner/loser

```text
query/schema fix
    -> first when bad query/model is the actual cause

provider-native index
    -> targeted access path while remaining transactionally maintained

materialized/denormalized projection
    -> repeated complex read with acceptable explicit staleness

cache
    -> repeated read where disposable stale data is safe

read replica
    -> read-offload when replica lag is acceptable and primary is measured bottleneck

search/specialized read store
    -> when real query semantics exceed economical relational access

GraphQL / API composition
    -> changes client/read composition, not automatically storage
```

Several can coexist, but only when each has a named problem.

## I. Real implementation considerations

Every derived copy needs: authoritative source, update/refresh path, freshness evidence, version/sequence where needed, duplicate/out-of-order handling, tenant/authorization scoping, deletion/privacy propagation, rebuild/reconciliation, resource bounds, and outage behavior.

### Implications for the Current Implementation

- **KEEP:** normalized relational authoritative model first, with provider-native indexes justified by real queries/invariants.
- **KEEP:** derived read structures are reconstructable state, not a second independent business authority.
- **KEEP:** eventual consistency is per invariant; payment/stock/credit/tenant-sensitive authorization remain current-authority decisions.
- **KEEP:** Workstation local-first state is a distinct authority model, not a generic cache/read replica.
- **NEEDS MEASUREMENT:** no read replica, shared cache, search store, or materialized projection should be selected until an implemented query/load proves the need.
- **IMPROVE NOW (implementation gate):** when the Web/Admin UI exists, capture read/write mix, tenant skew, reconnect bursts, query plans and read-after-write expectations before adding copies.
- **AVOID:** “add cache/read replica because reads are slow” without proving the bottleneck and acceptable staleness.
- **AVOID:** using a stale derived copy for a protected authority decision.

**What are we actually doing and why?** We currently keep one authoritative transactional model because product correctness and small-team operations matter more than speculative read scale. We add read structures only when an actual SquiFlow read proves the benefit.

**What would falsify/change this?** Repeated production evidence that a particular read dominates DB/resource budget and cannot be economically fixed with query/model/index changes would justify a projection/cache/replica/specialized store chosen for that read's consistency contract.

### Critical interrogation — answers intentionally withheld

**Foundation**
1. Why does read optimization often mean duplication or precomputation?
2. How is a DB index different from an asynchronous projection?
3. What is read-after-write consistency from the user's perspective?

**Critical reasoning**
1. Which SquiFlow reads actually need fresh authority and which can be stale?
2. Why can adding a dashboard index hurt Workstation reconnect throughput?
3. Why is the local Workstation DB not simply a cache of central state?
4. Which tenant/privacy rules must a derived read store preserve?
5. What is the simplest fix before adding another datastore?

**Trade-off**
1. When is a cache better than a materialized projection?
2. When is a read replica useful but unsafe for a given operation?
3. When does denormalization move complexity from reads into writes/rebuilds?

**Failure / edge**
1. User writes then immediately reads a lagging replica. What UX/contract applies?
2. Projection update stalls for one tenant only. How is stale state visible and repaired?
3. Cache key omits tenant or permission revision. What can leak?
4. A derived copy retains data after authoritative deletion. What must happen?

**Implementation**
1. Which metrics prove a read path is the bottleneck?
2. How is projection freshness represented?
3. How is a derived store rebuilt without becoming authority during rebuild?
4. How are old and new projection versions rolled out compatibly?

**System design interview**
1. Design a fast tenant dashboard without weakening payment/stock/authorization freshness.
2. Compare index, cache, read replica and materialized projection by SquiFlow workload rather than ranking them globally.

**Challenge**
A dashboard is slow during a 500-Workstation reconnect burst. Decide whether to add an index, cache, materialized projection, read replica, or none, and identify the measurements that must distinguish read cost from write/WAL/pool contention first.
