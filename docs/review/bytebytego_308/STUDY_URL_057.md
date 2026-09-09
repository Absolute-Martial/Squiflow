# URL 057 — Software Architect Knowledge Map

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `057`
- **PDF page:** `301`
- **Source URL:** `https://blog.bytebytego.com/p/ep156-software-architect-knowledge`
- **Source access:** public newsletter section accessible.
- **Related supplied visual:** archive page 162, system design topic map.
- **Visual inspected:** PDF page `301` at full size.

## B. Core concept

### SOURCE

The newsletter presents software architecture as a broad learning discipline. It recommends depth in one or two programming languages, proficiency with development/operations tools, design principles, multiple architectural patterns, platform/cloud/container/distributed-system concepts, data/analytics, networking/security, and supporting skills such as decision-making, stakeholder management, communication, estimation, and leadership.

### INFERENCE

For SquiFlow, a knowledge map is valuable as a review-question inventory: it helps us remember to test data, network, security, operations, delivery, and organizational consequences. It is not a stack-selection matrix and does not mean SquiFlow should implement one technology from every branch.

### EXTERNAL KNOWLEDGE / CAVEAT

Named tools and patterns in a learning roadmap are examples, not architecture requirements. Architecture quality depends on connecting decisions to product constraints, evidence, failure/recovery, ownership, operability and change cost. Important SquiFlow topics such as semantic idempotency, offline authority, tenancy isolation, compatibility, backpressure, restore, privacy and small-team operational burden may not be prominent in a generic map.

## C. Important concepts

- depth plus breadth;
- architecture patterns as vocabulary;
- data/network/security/platform literacy;
- delivery/operations knowledge;
- decision-making and stakeholder communication;
- evidence and trade-off reasoning;
- failure/recovery awareness;
- architecture as continuous learning rather than tool collection;

## D. Diagram / visual explanation

The related system-design map branches into application architecture, scalability/reliability, network/communication, security/observability, data, and infrastructure/deployment. In SquiFlow the useful move is to attach each branch to a concrete product boundary and ask whether it matters now. The map is a completeness aid; it is not a backlog that says ‘add caching, sharding, Kafka, Kubernetes, NoSQL, event sourcing, microservices, and a service mesh.’

## E. How it works — step by step

1. Use the map to identify which architecture domain a decision touches.
2. Translate a generic concept into the exact SquiFlow boundary/problem.
3. Check current accepted mechanism and why it exists.
4. Compare alternatives only against that requirement and operating context.
5. Identify evidence/POC needed before changing a material technology choice.
6. Record what remains unchanged even if another technology is adopted elsewhere.
7. Communicate the trade-off and recovery/operational impact clearly enough for implementation and review.

## F. Why it matters

The SquiFlow project spans offline desktop, Web, security providers, relational/local storage, file/object storage, constrained rack deployment, observability, business workflow, and future integrations. Breadth matters because failure frequently crosses those boundaries, while depth is needed to implement and debug them credibly.

## G. Trade-offs / limitations

A broad knowledge map can encourage shallow ‘logo architecture’ if every concept becomes a component. Focusing only on one stack can create blind spots. The useful balance is enough breadth to ask the right questions and enough depth/evidence to avoid cargo-cult decisions.

## H. Alternatives / comparisons — fit, not winner/loser

```text
knowledge map
    -> vocabulary/checklist for what to understand

architecture decision record / owner doc
    -> accepted product-specific decision and rationale

POC / benchmark / hostile test / restore drill
    -> evidence

implementation source + CI
    -> proof that design exists in code

operations runbook / telemetry
    -> proof that it can be operated/recovered
```

Knowing a concept is not the same as adopting it.

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** the review discipline that uses broad architecture sources to generate questions, not automatic components.
- **KEEP:** current depth around C#/.NET, modular monolith, offline sync, ZITADEL/OpenFGA, persistence, deployment/recovery, and OTel boundaries because those are actual SquiFlow responsibilities.
- **LATER / SCALE TRIGGER:** Kafka, NoSQL, sharding, service mesh, Kubernetes, serverless, data-lake/stream-processing and other map items only when a concrete workload requires their property.
- **IMPROVE NOW:** continue tying every material design choice to what/where/why, failure/recovery, adoption evidence and falsification evidence.
- **AVOID:** architecture-by-logo or treating a learning roadmap as a required technology stack.

**What are we actually doing and why?** SquiFlow studies broad architecture material so we can interrogate decisions and avoid blind spots. We use only the mechanisms whose properties solve current product constraints, while keeping other technologies as positive candidates for different future boundaries.

**What would falsify/change this?** If the review map repeatedly misses a class of incidents or business constraints—such as privacy, accessibility, offline recovery, or operator burden—the map should be extended. If a concept has no concrete SquiFlow use case, it stays knowledge rather than architecture.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. Which knowledge categories does the source consider important for a software architect?
2. Why are decision-making and communication listed beside technical skills?
3. What is the difference between knowing a pattern and selecting it for a system?

**Critical reasoning**

1. Which map branches are genuinely active SquiFlow architecture concerns today?
2. Which listed technologies are currently only knowledge/candidates and why?
3. What SquiFlow-specific topics are underrepresented by a generic system-design map?
4. How does a small-team constraint change an architect’s choice among equally capable technologies?
5. What evidence turns a concept from knowledge into an accepted dependency?

**Trade-off**

1. How much breadth is useful before study becomes distraction from implementation?
2. When should the team deepen one technology versus compare alternatives?
3. What operational complexity should disqualify a theoretically elegant option?

**Failure / edge**

1. A team knows Kafka well and therefore wants to use it for one Worker queue. What question is missing?
2. A diagram recommends Kubernetes but the deployment has one node. What requirement must exist first?
3. A security map says OAuth/JWT but the actual need is current resource authorization. Which layer is missing?

**Implementation**

1. How should architecture questions be converted into POC gates?
2. How do owner docs distinguish accepted direction from candidate knowledge?
3. Which architecture tests can encode dependency boundaries?
4. How should implementation evidence update a prior design assumption?

**System design interview**

1. Use the knowledge map to review SquiFlow without adding unnecessary technologies.
2. Explain how you would choose among several known technologies when all are technically capable.

**Challenge**

1. Given a map with every fashionable architecture component available, construct the smallest SquiFlow production architecture that still covers all current invariants and recovery responsibilities—and explain every omitted component.
