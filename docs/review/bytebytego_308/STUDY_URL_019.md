# ByteByteGo Exhaustive Sequential Study — URL Entry 019

# URL 019 — Top Strategies to Share Data Between Services

## A. Identification

- **URL entry:** `019`
- **PDF page:** `263`
- **Source URL:** `https://blog.bytebytego.com/p/top-strategies-to-share-data-between`
- **Public source access:** paid post; public preview inspected.
- **Related visual:** archive page `218`, service data-sharing/CQRS-related visual.
- **Visual inspection:** PDF page `263` rendered and inspected in full.

## B. Core concept

### SOURCE

The source contrasts a monolith’s shared database with service data ownership. It says a shared database makes coordination simple but increases coupling, while independent services should manage their own data and exchange selected information through mechanisms such as shared databases, APIs or messages. The challenge is preserving independence while maintaining consistency, performance and fault tolerance.

### INFERENCE

Data-sharing strategy follows the **actual ownership boundary**. A shared database is not universally wrong: it can be correct for multiple runtime hosts of one modular monolith with one business authority. It becomes dangerous when independently deployed services bypass each other’s ownership/invariants by editing shared private tables.

### EXTERNAL KNOWLEDGE / CAVEAT

“Database per service” is not the same as “one physical DB server/product per service.” Logical ownership and write authority matter more. API calls can create latency/availability coupling; replicated/event data can be stale and needs schema/version/rebuild semantics. Direct read access to another service’s tables may be acceptable for migration/analytics under controlled read-only contracts but should not become accidental write coupling.

## C. Important concepts

- logical authoritative data ownership;
- shared database within one modular monolith;
- service-private write authority;
- APIs for current data;
- events/messages for replicated facts;
- read models/materialization;
- consistency/freshness;
- schema evolution;
- transaction boundaries;
- cross-service query composition;
- reporting/analytics access;
- migration/anti-corruption boundaries.

## D. Diagram / visual explanation

The visual should be read as strategies with different coupling/consistency properties. For current SquiFlow, Core API, Admin API and Worker may share the central store because they are execution hosts of one modular-monolith business core, not three independent microservices.

## E. How it works — step by step

1. Define whether the boundary is a module or genuinely independent service.
2. Name the authoritative owner for each entity/invariant.
3. Inside the modular monolith, use shared reviewed application/domain/data-access rules rather than ad-hoc cross-module table writes.
4. If a capability is extracted, stop other services from mutating its private tables.
5. Choose synchronous API when current data is required immediately.
6. Choose durable event/replication/read model when temporary staleness is safe and decoupling is valuable.
7. Define schema/version, duplicate/out-of-order handling, freshness and rebuild for copied data.
8. Define degraded behavior if owner service/replica is unavailable.
9. Migrate shared-table access incrementally with compatibility evidence.
10. Keep analytics/reporting access read-only/derived where it should not become business authority.

## F. Why it matters

This article is a direct test of whether SquiFlow can explain *why* several executables share one DB today without turning that fact into either “shared DB is always good” or “database per service is always required.”

## G. Trade-offs / limitations

A shared DB offers transactions and simple joins but couples schemas and allows ownership bypass if not disciplined. APIs preserve ownership but add latency/availability/versioning. Event replication decouples consumers and supports local reads but introduces staleness/replay/rebuild. Separate physical databases increase isolation but also backup/restore/connection/operations burden.

## H. Alternatives / comparisons — fit, not winner/loser

```text
current modular-monolith hosts
    -> shared central DB + explicit module ownership

future independently deployed service
    -> private authoritative data ownership
    -> API/events/read models for access

report/search projection
    -> copied derived data allowed with freshness/rebuild contract
```

## I. Real implementation considerations / SquiFlow implications

- **KEEP:** Core API/Admin API/Worker may share central DB as hosts of one modular-monolith authority.
- **KEEP:** explicit module/data ownership even within shared DB; no ad-hoc bypass of invariants.
- **LATER / SCALE TRIGGER:** independent service data ownership only when a capability is genuinely extracted.
- **KEEP:** future services expose stable APIs/events/read models rather than other services mutating private tables.
- **AVOID:** interpreting “database per service” as mandatory one physical database server/product for every service.
- **AVOID:** shared-table writes between supposedly independent services.
- **NEEDS MEASUREMENT:** cross-boundary latency/freshness/operational cost when extraction is proposed.

**What are we doing and why?** We allow Core API, Admin API and Worker to share one central database because they are separate runtime hosts of the same modular-monolith business core and must enforce the same domain/transaction invariants. We would change to explicit service-owned persistence only when a capability becomes genuinely independently deployed/owned; then data-sharing must preserve that independence through stable contracts.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. What tension does the source describe between autonomy and data sharing?
2. Why is shared DB simple in a monolith?
3. What principle does the source call service data ownership?

**Critical reasoning questions**
1. Why is current SquiFlow shared central DB not automatically a microservice anti-pattern?
2. What exact capability would become authoritative owner if extracted?
3. Which direct table accesses would have to disappear after extraction?
4. When is an API preferable to replicated data?
5. What evidence proves service independence is worth cross-boundary consistency cost?

**Trade-off questions**
1. When is shared physical storage compatible with logical service ownership?
2. When is event replication better than synchronous lookup?
3. When is a local read model worth staleness/rebuild cost?

**Failure / edge-case questions**
1. Service B reads Service A replica that is hours stale and authorizes a payment. What boundary failed?
2. An event schema changes and old projection consumer cannot rebuild. What compatibility is required?
3. Two services both write the same customer balance table. What invariant ownership problem results?
4. Service API is unavailable during a transaction requiring its current data. How is business flow designed?

**Implementation questions**
1. How is data ownership documented/enforced in code/data access?
2. What migration sequence removes shared-table writes during service extraction?
3. How are replicated data freshness/reconciliation observed?
4. What integration tests prove no private-table mutation from another service?

**System design interview questions**
1. Design extraction of notification delivery without unnecessarily moving order/payment tables.
2. Compare shared DB, synchronous API and event replication for a future search service.

**Challenge**
A future “inventory service” is proposed but Core API still writes inventory tables directly for speed. Decide whether it is actually an independent service and design the migration needed before claiming service data ownership.

---
