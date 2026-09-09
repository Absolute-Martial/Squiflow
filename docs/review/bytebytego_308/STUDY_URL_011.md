# ByteByteGo Exhaustive Sequential Study — URL Entry 011

# URL 011 — Database Schema Design Simplified: Normalization vs Denormalization

## A. Identification

- **URL entry:** `011`
- **PDF page:** `255`
- **Source URL:** `https://blog.bytebytego.com/p/database-schema-design-simplified`
- **Public source access:** paid post; public preview inspected. The preview is explicit that normalization and denormalization are tools for different problems rather than rivals with one universal winner.
- **Related visual:** archive page `214`, normalization/denormalization visual.
- **Visual inspection:** PDF page `255` rendered and inspected in full.

## B. Core concept

### SOURCE

The accessible source says schema design affects query speed, feature evolution, scalability and long-term maintenance, and that schema choices should be revisited as scale, data shape and system goals change. It characterizes normalization as favoring integrity, lower redundancy and maintainability, while denormalization favors read efficiency and simpler access paths. It explicitly says the goal is not to crown a winner.

### INFERENCE

The useful decision is per data responsibility and query path: keep authoritative invariants in a structure that is easy to constrain and evolve, then introduce derived duplication only where a demonstrated access pattern earns it.

### EXTERNAL KNOWLEDGE / CAVEAT

Normalization is not the same as “maximum normal form everywhere,” and denormalization is not automatically “faster.” Join selectivity, indexes, data volume, write frequency, update fan-out, cache locality and provider behavior determine actual cost. A denormalized read model can be maintained synchronously or asynchronously; its freshness/authority contract must be explicit.

## C. Important concepts

- authoritative versus derived data;
- redundancy and update anomalies;
- write constraints and transactional invariants;
- read-model duplication;
- join/query cost;
- materialized views/projections;
- freshness and rebuild;
- tenant scoping;
- migration/backfill;
- workload-driven evolution.

## D. Diagram / visual explanation

The visual contrasts normalized tables with denormalized shapes. For SquiFlow the visual is not a binary migration plan. The correct interpretation is:

```text
transactional business authority
    -> normalized around real business identities/relationships

measured expensive read/report
    -> consider projection/materialized view/denormalized read shape
    -> declare source + freshness + rebuild + tenant authorization
```

## E. How it works — step by step

1. Identify the business invariant and authoritative owner.
2. Model current-state writes so constraints/transactions are understandable.
3. Implement representative reads and inspect real query plans.
4. Measure join/query cost at projected cardinality and tenant skew.
5. If a read path is materially expensive, first test query/index changes.
6. If duplication is still justified, define a derived read structure.
7. Define synchronous/asynchronous refresh, freshness, duplicate/out-of-order handling and rebuild.
8. Ensure stale/failed projection cannot become payment/stock/permission authority.
9. Revisit when workload changes.

## F. Why it matters

SquiFlow has a rule-heavy transactional core plus dashboards/reports that may later have different read needs. This article reinforces using different representations for different responsibilities without turning “normalized vs denormalized” into a project-wide decision.

## G. Trade-offs / limitations

Normalization can increase joins and read complexity; denormalization duplicates data, expands update/backfill work, can introduce staleness and creates another structure to secure, observe and rebuild. Premature denormalization can hide weak queries/indexes. Over-normalization can also produce needless join complexity if entities are split without domain meaning.

## H. Alternatives / comparisons — fit, not winner/loser

```text
normalized authoritative schema
    -> current fit for payments/orders/inventory/membership/workflow truth

derived materialized/read projection
    -> fit for a proven dashboard/report/query shape

cache
    -> fit for reusable disposable responses where freshness permits
```
These can coexist. The decision is not which philosophy wins; it is which representation owns which responsibility.

## I. Real implementation considerations / SquiFlow implications

- **KEEP:** normalized authoritative business model as the starting point.
- **KEEP:** denormalized/materialized structures are reconstructable derived state with explicit source/freshness/rebuild.
- **NEEDS MEASUREMENT:** real joins/query plans/cardinality on Phase-3 workload before adding projections.
- **LATER / SCALE TRIGGER:** dashboard/report-specific read models when actual UI/query shapes justify them.
- **AVOID:** denormalized authoritative copies that can disagree about payment, stock, permissions or other protected state.
- **AVOID:** interpreting normalization as a dogmatic maximum-normal-form requirement.

**What are we doing and why?** We keep central business authority normalized around real identities and relationships because constraints, write correctness and evolution matter for SquiFlow’s transactional core. We would add denormalized read structures only for a concrete read path whose measured benefit exceeds freshness/rebuild/operational cost.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. What does the source say normalization optimizes for?
2. What does it say denormalization optimizes for?
3. Why does the source explicitly reject a winner/loser framing?

**Critical reasoning questions**
1. Which SquiFlow entities require one unambiguous authoritative representation?
2. Which future Web/Admin screens may justify a separate read projection?
3. Could the slow query be fixed by query/index shape before duplicating data?
4. What freshness can each candidate projection tolerate?
5. What evidence would prove the current normalized representation is inadequate for a specific read path?

**Trade-off questions**
1. When is an extra join cheaper than duplicated update logic?
2. When is synchronous denormalization preferable to asynchronous projection?
3. When is a cache better than a stored denormalized table?

**Failure / edge-case questions**
1. Projection update is lost after authoritative commit. What rebuild/reconciliation restores it?
2. A stale projection shows old stock. Which operations may rely on it?
3. Backfill runs while live writes continue. How is cutover made correct?

**Implementation questions**
1. What source version/effect identity does a projection persist?
2. How are tenant scope and OpenFGA/resource filters preserved in read models?
3. How is a denormalized structure rebuilt and compared to authority?
4. Which benchmark establishes that duplication actually helps?

**System design interview questions**
1. Design an order dashboard read model without making it a second source of truth.
2. Explain why normalization and denormalization can coexist in one application.

**Challenge**
A dashboard query becomes slow at 20M rows. Compare query/index tuning, a materialized view, a denormalized projection and a cache. Choose only after stating authority, freshness, rebuild and measured cost for each.

---
