# URL 054 — Mastering Data Consistency Across Microservices

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `054`
- **PDF page:** `298`
- **Source URL:** `https://blog.bytebytego.com/p/mastering-data-consistency-across`
- **Source access:** paid article with a public preview; no subscription controls bypassed.
- **Related supplied visual:** archive page 441, microservice best-practices visual.
- **Visual inspected:** PDF page `298` at full size.

## B. Core concept

### SOURCE

The preview describes microservices as independently operating services that communicate through APIs and often manage their own databases. It highlights the resulting consistency problem: business operations can span stores, while duplicate/lost data, network delays, and concurrency can make services temporarily or permanently disagree. It frames the article around understanding those inconsistency scenarios and strategies to handle them.

### INFERENCE

The most important SquiFlow question comes before selecting a consistency pattern: why create an independently owned service/data boundary at all? The current modular monolith intentionally avoids distributed consistency for ordinary business modules because many payment, stock, quotation, workflow, and tenant invariants are still strongly related.

### EXTERNAL KNOWLEDGE / CAVEAT

A monolith does not automatically mean one database or guaranteed consistency, and microservices do not require one physical database server per service. The stronger property is explicit authoritative ownership. Distributed transactions, sagas/compensation, outbox/event propagation, reconciliation, and derived projections are different tools with different failure semantics; the paid preview does not expose which strategies the article later recommends, so those patterns must be treated as external engineering options rather than source claims.

## C. Important concepts

- authoritative data ownership;
- local transaction boundary versus distributed business process;
- network delay and partial failure;
- duplicate/lost/out-of-order messages;
- concurrency and stale reads;
- eventual consistency and convergence;
- outbox/reconciliation/compensation;
- service extraction trigger;
- shared database inside one modular-monolith authority versus cross-service table mutation;

## D. Diagram / visual explanation

The related nine-practice microservices visual includes separate data stores, independent builds, single responsibility, containers, stateless servers, DDD, micro frontends, and orchestration. Those boxes are not a target architecture for SquiFlow. The consistency article specifically makes the cost of separation visible: once one business operation crosses independently owned data stores, local ACID transactions no longer cover the whole business outcome.

## E. How it works — step by step

1. Start from the business invariant and decide which capability owns authoritative state.
2. Keep ordinary modular-monolith modules in-process when one transaction/authority is still the simplest correct boundary.
3. If a capability earns independent deployment, define its private authoritative writes and external contract.
4. Classify each cross-boundary interaction as immediate synchronous need or after-commit/eventual consequence.
5. Use semantic idempotency, durable outbox, retries and reconciliation for propagated effects.
6. Use compensation only where the business can meaningfully undo/correct a prior committed step.
7. Expose user/operator states for pending, failed, ambiguous, or compensating outcomes.
8. Measure latency, operational burden, recovery and data drift before claiming the extraction improved the system.

## F. Why it matters

SquiFlow’s current small-team, lower-spec-rack, transaction-heavy product has strong reasons to avoid unnecessary distributed consistency. The same article becomes valuable later if a capability gains independent scaling, fault, security, deployment, residency, or team-ownership requirements strong enough to justify extraction.

## G. Trade-offs / limitations

Independent services can isolate deployment/fault/scaling concerns and clarify ownership, but they introduce network failures, versioned contracts, duplicated data/projections, retry/idempotency, operational tooling, reconciliation, and more difficult end-to-end transactions. Keeping everything in one process forever can also become a constraint if a real boundary needs independence.

## H. Alternatives / comparisons — fit, not winner/loser

```text
modular monolith + one central transaction
    -> current strongly related business authority

separate process sharing the same business authority
    -> Admin API / Worker style runtime separation where appropriate

independent service + private authoritative data
    -> when scaling/fault/security/team/residency boundary is real

durable outbox + event/projection
    -> after-commit propagation

saga/compensation or explicit orchestration
    -> multi-step distributed business process when required

reconciliation
    -> ambiguity/drift that cannot be atomically resolved
```

No line is a universal maturity step.

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** the modular-monolith business core and in-process module communication for ordinary business modules.
- **KEEP:** Core API, Admin API, and Worker may share central persistence because they are runtime hosts of one business authority, while preserving module/data ownership.
- **KEEP:** strong/current authority for payment, stock, tenant authorization, credit and other protected invariants; derived projections may be eventual only with explicit source/freshness/rebuild.
- **LATER / SCALE TRIGGER:** extract a true service only for measured independent scaling, fault/security isolation, deployment cadence, team ownership, specialized runtime, residency or similar concrete need.
- **LATER / SCALE TRIGGER:** saga/compensation/eventual cross-service workflow only after a real independently owned service boundary exists.
- **AVOID:** service-per-module or database-per-service rules applied mechanically to the current modular monolith.
- **AVOID:** direct cross-service private-table writes after a capability genuinely becomes independently owned.

**What are we actually doing and why?** SquiFlow keeps ordinary business modules in one modular-monolith authority because current invariants benefit from local transactions, simpler debugging, lower operating cost, and one small team. Microservices remain a positive option for a boundary that later needs real independence; the comparison itself is not the decision.

**What would falsify/change this?** If a module develops sustained independent scaling, security/process isolation, team/deployment independence, or residency requirements that the monolith cannot satisfy economically, the current boundary should be re-evaluated. If an extracted service still requires shared-table writes and synchronized releases, the extraction has likely produced a distributed monolith and should be reconsidered.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. Why does separate data ownership make consistency harder?
2. What is the difference between local ACID consistency and end-to-end distributed business consistency?
3. Why can duplicate/lost messages occur even when each service database is healthy?

**Critical reasoning**

1. Which SquiFlow invariants currently benefit from one central transaction?
2. What real requirement would justify extracting a payment, inventory, or document capability?
3. Why can Core API, Admin API, and Worker share a database without automatically being ‘bad microservices’?
4. When does a derived projection tolerate stale data and when would that violate authority?
5. What does explicit authoritative ownership mean if multiple schemas/databases run on one physical server?

**Trade-off**

1. What independence benefit must outweigh distributed consistency cost before service extraction?
2. When is compensation a legitimate business correction and when is it a fake rollback?
3. When would synchronous RPC be safer/simpler than event propagation, and when the opposite?

**Failure / edge**

1. Service A commits but its event is not delivered. How is the consequence recovered?
2. An event is delivered twice and out of order. Which state/version rules prevent corruption?
3. A compensation fails after the original step committed. What user/operator state is exposed?
4. Two services each think they own the same field/table. What architectural defect exists?

**Implementation**

1. How is the transactional outbox tied to the authoritative transaction?
2. Which identifiers make propagated commands/events idempotent?
3. How is stale derived state versioned and rebuilt?
4. What recovery drill proves an extracted service can be restored without hidden shared-state coupling?

**System design interview**

1. Design a future independently deployed notification/payment-adjacent service while preserving SquiFlow business authority.
2. Explain why database-per-service is about ownership more than one physical database instance.

**Challenge**

1. A proposed ‘inventory microservice’ still needs synchronous access to order tables, shared DB transactions, coordinated deployments, and direct updates from Core API. Decide whether extraction is justified and explain what must change before calling it independent.
