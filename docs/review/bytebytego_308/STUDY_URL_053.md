# URL 053 — Non-Functional Requirements: The Backbone of Great Software - Part 1

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `053`
- **PDF page:** `297`
- **Source URL:** `https://blog.bytebytego.com/p/non-functional-requirements-the-backbone`
- **Source access:** paid article with a public preview; no subscription controls bypassed.
- **Related supplied visual:** archive page 429, system performance metrics visual.
- **Visual inspected:** PDF page `297` at full size.

## B. Core concept

### SOURCE

The public preview distinguishes functional requirements—what the software does—from non-functional requirements—how well it must perform under real conditions. It calls out response time, availability, usability, reliability, fault tolerance, recoverability, security/compliance, throughput, capacity/resource utilization, maintainability, testability, and modularity, and stresses that ignoring these qualities early can lead to instability or expensive redesign.

### INFERENCE

The strongest SquiFlow use of this article is not to add another NFR checklist but to ensure each important quality is connected to a concrete capability, failure mode, measurable evidence, recovery behavior, and release gate.

### EXTERNAL KNOWLEDGE / CAVEAT

The functional/NFR boundary is useful but not absolute; some security, audit, compatibility, or recovery requirements are observable behaviors and can be written functionally. Numeric targets should not be invented before representative workload/hardware evidence exists. The related QPS/concurrency/response-time formula is a steady-state relationship, not a license to increase concurrency indefinitely or a complete latency model.

## C. Important concepts

- functional behavior versus quality/operational constraint;
- hard invariants versus measurable targets versus degraded modes;
- latency distributions rather than averages alone;
- availability, fault tolerance and recoverability as separate properties;
- capacity/resource utilization and backpressure;
- security/compliance and privacy;
- maintainability/testability/modularity;
- actual-hardware measurement;
- failure/recovery/compatibility acceptance evidence;

## D. Diagram / visual explanation

The visual separates queries per second, transactions per second, concurrency, and response time, and relates QPS to concurrency and average response time. For SquiFlow the important addition is queueing and bottleneck location: API latency can be dominated by DB pool wait, locks, disk/WAL, provider latency, Worker backlog, network, or client work. Average response time can also hide p95/p99 tail behavior and tenant unfairness.

## E. How it works — step by step

1. Name the concrete capability or user journey.
2. State the normal functional behavior.
3. Identify hard invariants that may not be traded away for speed/availability.
4. Define degraded behavior when network/provider/storage/process capacity fails.
5. Name measurable target families and the exact measurement method.
6. Run representative workload on the real deployment class before setting promises.
7. Define recovery, compatibility, and hostile/edge tests.
8. Record whether each numeric target is accepted, provisional, or still open.
9. Revisit architecture only when evidence shows the current mechanism cannot meet the requirement.

## F. Why it matters

SquiFlow runs on constrained owned hardware, supports offline Workstations, depends on identity/authorization/storage providers, and will handle money, stock, documents, and multi-tenancy. Those characteristics make failure, recovery, capacity, compatibility, and operability first-class design inputs rather than later optimization work.

## G. Trade-offs / limitations

Stricter NFRs can conflict: stronger durability may add latency; more isolation costs resources; higher availability costs topology and operations; broader telemetry costs storage/privacy/cardinality. Treating every possible quality as a hard requirement would make the product expensive and slow to build.

## H. Alternatives / comparisons — fit, not winner/loser

```text
HardInvariant
    -> correctness/security property that cannot be traded away

OperationalTarget
    -> measured latency/capacity/recovery objective

DegradedMode
    -> defined behavior when a dependency/resource fails

OpenBeforeProduction
    -> target/policy that must be closed before a promise

Deferred / NotBaseline
    -> intentionally absent requirement until a real trigger
```

These are complementary requirement states, not competing architectures.

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** the current SquiFlow NFR model of HardInvariant, OperationalTarget, and DegradedMode with explicit authority/failure/recovery/compatibility/evidence.
- **KEEP:** measurement-driven numeric targets on actual rack/Workstation hardware instead of invented p95/p99, memory, or throughput numbers.
- **KEEP:** resource bounds/backpressure, restore qualification, tenant isolation, sync idempotency, and provider-failure degraded modes as release concerns.
- **IMPROVE NOW:** for each implemented vertical slice, translate relevant NFRs into executable acceptance tests and observable evidence rather than leaving them only in architecture prose.
- **NEEDS MEASUREMENT:** actual first-customer latency, capacity, recovery, RPO/RTO, Workstation resource and provider-budget targets.
- **AVOID:** treating ‘scalable’, ‘reliable’, or ‘secure’ as complete requirements without conditions and proof.

**What are we actually doing and why?** SquiFlow uses a structured NFR model because the product’s main risks include ambiguous failure, constrained hardware, offline recovery, tenant isolation, provider outages, and long-lived compatibility. The architecture should change only when those measurable requirements show a current mechanism is insufficient—not because an NFR list suggests a fashionable scaling pattern.

**What would falsify/change this?** If an NFR has no meaningful user/business/operational consequence for the implemented capability, forcing a heavyweight mechanism or arbitrary target is waste. Conversely, a recurring incident or failed acceptance test is evidence that the current design or target needs strengthening.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. How do functional requirements differ from NFRs in the source?
2. What is the difference among availability, reliability, fault tolerance, and recoverability?
3. Why is average response time insufficient for a user-facing performance promise?

**Critical reasoning**

1. Which SquiFlow requirements are HardInvariants and which are OperationalTargets?
2. Why should the lower-spec rack change how capacity requirements are written?
3. Which NFRs apply differently to Web, Workstation, Guard, Core API, Admin API, and Worker?
4. What does ‘Web online-only honesty’ require during network loss?
5. How do compatibility and restore become NFR concerns rather than just deployment details?

**Trade-off**

1. When is lower latency worth higher resource cost, and who decides?
2. What availability promise is realistic with one active rack node?
3. How much observability can be added before telemetry becomes a resource/privacy problem?

**Failure / edge**

1. OpenFGA is slow but not fully down. Which latency/degraded requirement applies?
2. Disk is nearly full while Workstation has unsynced local work. Which invariant wins?
3. p50 is excellent but p99 reconnect latency is terrible for one tenant. Is the NFR met?
4. A backup exists but restore cannot re-establish idempotency/job state. Is recoverability met?

**Implementation**

1. Which metrics and acceptance tests close the first production NFRs?
2. How are provisional numerical targets recorded without becoming accidental customer promises?
3. What workload profile is required before tuning DB or Worker concurrency?
4. Which NFR failures should block release versus create an operational warning?

**System design interview**

1. Define NFRs for SquiFlow Workstation sync on a lower-spec rack.
2. Design an acceptance plan for latency, durability, tenant isolation, and recovery without inventing arbitrary target values.

**Challenge**

1. A change cuts median API latency in half but doubles WAL growth, causes p99 stalls under reconnect bursts, and worsens restore time. Decide which evidence matters and how you would judge the trade-off.
