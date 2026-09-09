# ByteByteGo Exhaustive Sequential Study — URL Entry 012

# URL 012 — Database Indexing Demystified: Index Types and Use-Cases

## A. Identification

- **URL entry:** `012`
- **PDF page:** `256`
- **Source URL:** `https://blog.bytebytego.com/p/database-indexing-demystified-index`
- **Public source access:** paid post; public preview inspected.
- **Related visual:** archive page `226`, database index-type/use-case visual.
- **Visual inspection:** PDF page `256` rendered and inspected in full.

## B. Core concept

### SOURCE

The source explains indexes as precomputed structures that narrow the search space so a database does not inspect as many rows for filtering/sorting. It states that different index types serve different patterns such as key lookups and range scans, and that every index adds maintenance/storage cost.

### INFERENCE

Index selection should begin from a real query or invariant, not from column availability. The value of an index is the measured change in the target access path minus write/storage/rebuild/maintenance cost under SquiFlow’s actual workload.

### EXTERNAL KNOWLEDGE / CAVEAT

The optimizer may choose not to use an available index. Composite-key order, selectivity, correlation, cardinality estimates, covering behavior, partial predicates and provider-specific index types matter. Extra indexes can increase WAL, checkpoint pressure and reconnect/import duration on SquiFlow’s constrained rack.

## C. Important concepts

- search-space reduction;
- B-tree/hash/inverted/other provider-specific structures;
- equality vs range vs sort access;
- composite index order;
- tenant-aware keys;
- uniqueness constraints;
- write amplification;
- WAL/storage/cache pressure;
- query planner/statistics;
- index migration/rebuild.

## D. Diagram / visual explanation

The related visual groups several index structures and access patterns. It should be read as a vocabulary map, not a prescription to create one of each. PostgreSQL/other provider support and actual query plans determine what exists and is useful.

## E. How it works — step by step

1. Capture the exact query/invariant and expected cardinality.
2. Record tenant scope and ordering/filter predicates.
3. Run the real provider plan without the proposed index.
4. Add the smallest candidate index/constraint.
5. Re-run at normal and large-tenant cardinality.
6. Measure inserts/updates/deletes, reconnect/import bursts, WAL/storage and migration time.
7. Observe planner choice and statistics sensitivity.
8. Keep only if the whole workload improves acceptably.
9. Periodically review redundant/unused indexes after real production evidence exists.

## F. Why it matters

SquiFlow’s offline reconnect and tenant-scoped query patterns mean indexes can be both essential and expensive. This article supports the existing workload-first policy rather than a blanket “add indexes for speed” rule.

## G. Trade-offs / limitations

More indexes can speed selected reads while slowing writes, enlarging storage/backups/WAL and making schema migrations/rebuilds longer. Very selective small tables may not benefit. Bad composite order can be ineffective. Indexes can also mask poor access patterns or create assumptions that fail under tenant skew.

## H. Alternatives / comparisons — fit, not winner/loser

An index is not an alternative to good schema/query design, caching or projections globally. It is one tool. A tenant-scoped composite B-tree may fit one order list, an inverted/full-text index may fit search later, and no extra index may be best for a tiny reference table.

## I. Real implementation considerations / SquiFlow implications

- **KEEP:** every important index must protect a real query or invariant.
- **KEEP:** tenant-aware uniqueness/indexing where query/invariant requires it.
- **NEEDS MEASUREMENT:** actual plans, write cost, WAL/checkpoint impact and reconnect/import behavior.
- **AVOID:** index every filterable field.
- **AVOID:** inferring an index type from a generic diagram without provider/query evidence.
- **LATER / SCALE TRIGGER:** specialized search/index structures only for concrete search/query workloads.

**What are we doing and why?** We use indexes only where a concrete query or database invariant justifies them because SquiFlow must balance interactive reads against offline reconnect/import writes and finite rack resources. We would change an index when query-plan and whole-workload evidence shows a better shape or that its maintenance cost exceeds its value.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. What problem does the source say indexing solves?
2. Why do different index types exist?
3. What cost does the source explicitly attach to indexes?

**Critical reasoning questions**
1. Which Phase-3 SquiFlow queries are likely first index candidates and why?
2. Which indexes protect correctness rather than only performance?
3. How does tenant skew affect selectivity and plan choice?
4. What would make a dashboard index unacceptable despite excellent read latency?
5. What evidence would falsify an “obvious” composite index design?

**Trade-off questions**
1. When is a sequential scan cheaper than an index scan?
2. When should uniqueness be enforced by an index/constraint?
3. When is a specialized search index better than a relational B-tree?

**Failure / edge-case questions**
1. Index build fills disk/WAL on the rack. What recovery/maintenance path exists?
2. Statistics go stale after bulk import and planner regresses. How is this detected?
3. A pooled tenant query uses the wrong leading key and scans many tenants. What evidence catches it?

**Implementation questions**
1. What query-plan snapshots are captured in performance POCs?
2. How are index migration/rollback and lock impact tested?
3. What per-tenant data sizes are included?
4. How are unused/redundant indexes reviewed later?

**System design interview questions**
1. Design indexes for a tenant-scoped order list with status/date sorting and explain write cost.
2. Explain why more indexes can make a database slower overall.

**Challenge**
A composite index reduces p95 order-list latency by 10x but increases reconnect batch duration by 60%. Decide what to measure/change before accepting or rejecting it.

---
