# ByteByteGo Exhaustive Sequential Study — URL Entry 018

# URL 018 — Top Service-to-Service Communication Patterns

## A. Identification

- **URL entry:** `018`
- **PDF page:** `262`
- **Source URL:** `https://blog.bytebytego.com/p/top-service-to-service-communication`
- **Public source access:** paid post; public preview inspected.
- **Related visual:** archive page `118`, synchronous/asynchronous service communication visual.
- **Visual inspection:** PDF page `262` rendered and inspected in full.

## B. Core concept

### SOURCE

The source says independently deployed services must still coordinate business work and that communication style directly affects latency, scalability, failure recovery and consistency. It contrasts synchronous/blocking communication, which waits for an immediate response, with asynchronous/non-blocking communication, which allows senders to continue but adds consistency/ordering/debugging complexity.

### INFERENCE

The first SquiFlow question is **why is this a service boundary at all?** Only after a real process/service boundary exists should synchronous vs asynchronous transport be selected from the business semantics.

### EXTERNAL KNOWLEDGE / CAVEAT

“Synchronous is simple” can become false across long chains because timeout/retry/version/failure interactions compound. “Asynchronous is decoupled” does not mean easier correctness: durable delivery, idempotency, ordering, poison handling, authorization semantics and reconciliation become explicit obligations. gRPC/HTTP are transports for synchronous calls; queues/brokers are not automatically needed for all async work because a DB-backed outbox/job mechanism may fit.

## C. Important concepts

- in-process module call;
- real service/process boundary;
- synchronous request/response;
- gRPC vs HTTP fit;
- durable command/job;
- committed event/fan-out;
- timeout/deadline;
- retry ownership/idempotency;
- failure propagation;
- ordering/consistency;
- tracing/causation;
- long synchronous chain avoidance.

## D. Diagram / visual explanation

The related visual presents multiple communication styles. For SquiFlow it is a decision tree, not a mandate to create services:

```text
same host/module
    -> in-process

real process boundary + immediate authoritative answer
    -> synchronous HTTP/gRPC candidate

after-commit/long-running consequence
    -> durable job/outbox/Worker

many independent consumers of committed fact
    -> fan-out/pub-sub only when real
```

## E. How it works — step by step

1. Prove the capability needs an independent process/service boundary.
2. Identify whether caller needs an immediate authoritative answer.
3. If yes, compare HTTP/gRPC from payload/streaming/contracts/edge evidence.
4. Define deadlines, cancellation and one retry owner.
5. Preserve semantic idempotency for ambiguous outcomes.
6. If work is after-commit/long-running, persist durable job/outbox before returning.
7. Define authorization semantics for queued work.
8. Add duplicate/out-of-order/reconciliation behavior where messages are used.
9. Trace correlation/causation across the boundary.
10. Avoid adding another synchronous hop unless its independence value exceeds failure cost.

## F. Why it matters

This source is directly relevant to SquiFlow’s modular-monolith-first rule and gRPC selection policy. The decision is not synchronous vs asynchronous globally; different business interactions need different semantics.

## G. Trade-offs / limitations

Synchronous calls provide simple request/response but couple latency/availability and can amplify retries. Async work isolates latency and absorbs bursts but adds queue/job state, eventual outcomes, duplicate delivery, ordering/reconciliation and operator tooling. In-process calls avoid network failures but do not provide process isolation.

## H. Alternatives / comparisons — fit, not winner/loser

The options are complementary, not competing architecture brands. A future document service might receive a synchronous capability query and still perform rendering asynchronously; Workstation sync might use gRPC while notifications use durable Worker jobs.

## I. Real implementation considerations / SquiFlow implications

- **KEEP:** in-process calls inside modular-monolith hosts.
- **KEEP:** durable async for long-running/after-commit consequences.
- **KEEP:** gRPC as preferred candidate at real synchronous boundaries, not universal transport.
- **NEEDS MEASUREMENT:** HTTP vs gRPC POC for Workstation/real service workloads.
- **AVOID:** HTTP/gRPC between ordinary modules to imitate microservices.
- **AVOID:** long synchronous chains and nested retries.
- **LATER / SCALE TRIGGER:** broker/pub-sub only when independent consumer/replay/throughput needs exceed simpler outbox/job mechanisms.

**What are we doing and why?** We keep ordinary module communication in-process because current business capabilities share one runtime/transactional core. We use durable Worker/outbox for work that does not need an immediate interactive result. We evaluate HTTP/gRPC only for real synchronous process boundaries because only there do network transport properties justify their failure/version/operations cost.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. What two broad communication styles does the preview distinguish?
2. Which system properties does the source say communication style affects?
3. Why must independent services communicate despite independent deployment?

**Critical reasoning questions**
1. Which current SquiFlow interactions are not service-to-service at all?
2. What invariant requires an immediate answer versus durable async outcome?
3. What property would make gRPC materially better than HTTP on a real boundary?
4. What retry owner prevents multiplication across client/API/service/provider layers?
5. What evidence would justify extracting a service before choosing its communication pattern?

**Trade-off questions**
1. When is synchronous communication preferable?
2. When does async work improve resilience enough to justify eventual outcomes?
3. When is a DB-backed job/outbox simpler than a broker?

**Failure / edge-case questions**
1. Caller times out after callee commits. How is OutcomeUnknown/idempotency handled?
2. Async consumer crashes after external effect before acknowledgement. What prevents duplicate effect?
3. Service B slows down and A/C retries. How is overload contained?
4. Version skew changes message/schema semantics. What compatibility policy applies?

**Implementation questions**
1. How are deadlines/cancellation propagated?
2. How are CorrelationId/CausationId propagated?
3. What per-boundary retry budget and idempotency key exists?
4. What queue age/poison/reconciliation metrics are required for async work?

**System design interview questions**
1. Design communication for invoice issuance, PDF generation and notification delivery.
2. Decide HTTP vs gRPC vs durable async for a future independently deployed search service.

**Challenge**
A proposed architecture turns every module call into gRPC “to prepare for microservices.” Challenge the service boundary first, then identify which calls should remain in-process, which might become RPC later and which belong to durable async.

---
