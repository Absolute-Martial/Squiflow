# ByteByteGo Exhaustive Sequential Study — Archive Entries 111-120

**Source:** `ByteByteGo_Selected_Archive_plus_Web_Content_Images_URLs(2).pdf`  
**Coverage in this file:** archive entry `119`  
**Review method:** source first; diagrams visually inspected; `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE` separated; SquiFlow implications follow both `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`.

The standing rule for this batch is stronger than “compare technologies.” For every material exposure the review asks:

```text
What is SquiFlow actually doing at this boundary?
What real user/business/operational problem does that solve?
Why does the current mechanism fit that problem?
What alternative could be better for a different surface?
Could several mechanisms coexist?
What authority does the mechanism own — and what does it NOT own?
What failure/recovery burden does it introduce?
What evidence would justify adoption?
What evidence would falsify/change the current choice?
What is documented versus actually implemented today?
```

A source comparison, popularity claim, or product catalog is never sufficient by itself to choose SquiFlow architecture.

---

# 119 — System Performance Metrics Every Engineer Should Know

## A. Identification

- **Archive entry:** `119`
- **PDF pages:** `233-234`
- **Original archive pages:** `429-430`
- **Multi-page:** yes
- **Visual inspected:** PDF page `233`.

## B. Core concept

### SOURCE

The article highlights four metrics:

- QPS — incoming requests/queries per second;
- TPS — completed transactions per second;
- Concurrency — simultaneous active requests;
- Response Time — elapsed request-to-response time.

It gives the relationship:

```text
QPS = Concurrency / Average Response Time
```

### INFERENCE

The article is applying a Little's-Law-style relationship to request throughput: under stable conditions, average number in system ≈ throughput × average time in system.

### EXTERNAL KNOWLEDGE / CAVEAT

The terminology is workload-dependent:

- QPS can mean application requests, database queries, or another counted operation; the unit must be defined.
- TPS does not universally mean “request + database + response.” A transaction is a domain/system-defined unit.
- The formula is meaningful only with a clearly defined stable system boundary and consistent units; queueing, bursts, failures, and changing arrival rates can make a simplistic reading misleading.
- Average response time alone hides tail latency. p50/p95/p99/max, errors, saturation, queue age, resource utilization, and dependency breakdown are often more actionable.
- In modern async .NET services, high I/O concurrency does not necessarily require one thread per request.

## C. Important concepts

- offered load vs completed throughput;
- QPS/RPS/TPS unit definition;
- concurrency/in-flight work;
- latency distribution;
- queue wait vs service time;
- saturation;
- error/timeout rate;
- utilization (CPU/RAM/disk/network);
- DB connection-pool wait;
- lock/WAL/storage pressure;
- Worker queue depth **and oldest age**;
- sync backlog count/bytes/oldest age/drain throughput;
- dependency/provider latency;
- tenant fairness/noisy neighbor;
- coordinated omission and realistic load tests;
- actual hardware qualification.

## D. Diagram / visual explanation

Page `233` has four colored columns for QPS, TPS, Concurrency, and Response Time, each showing a client/server path. The bottom formula states `QPS = Concurrency ÷ Average RT` and notes that more concurrency or lower response time increases throughput.

That visual is useful for first-order relationships but can be dangerous if interpreted as “increase concurrency to make the system faster” without considering saturation.

## E. How it works — step by step

A useful SquiFlow performance investigation should:

1. Define the exact journey/operation and workload unit.
2. Measure offered load and successful completed throughput.
3. Measure end-to-end latency distribution, not only averages.
4. Measure in-flight concurrency and queue wait.
5. Find first constrained resource: CPU, memory, DB pool, locks, disk/WAL, network, provider quota, Worker slots, etc.
6. Apply one targeted change.
7. Re-run the same workload and verify correctness did not regress.

## F. Why it matters

This article reinforces one of the strongest existing SquiFlow principles: **measure before scaling/optimizing**. It is especially relevant because SquiFlow targets lower-spec owned hardware and offline reconnect bursts, where peak behavior may differ radically from steady Web demo traffic.

## G. Trade-offs / limitations

- high throughput can come with unacceptable latency;
- low average latency can hide severe tail latency;
- increasing concurrency can improve utilization until it causes queueing/contention collapse;
- batching can improve throughput while increasing individual latency;
- caches can improve average latency but introduce freshness/correctness risk;
- more DB connections can reduce wait until DB memory/lock contention worsens;
- sampling/telemetry itself consumes resources;
- performance targets without representative workload become vanity numbers.

## H. Alternatives / comparisons — fit, not winner/loser

Metrics are complementary rather than competitors:

```text
latency distribution
  user experience / dependency delay

throughput
  completed capacity

concurrency
  work in flight

queue/backlog age
  ability to recover from bursts

utilization/saturation
  constrained resource

error/timeout rate
  correctness/availability under load
```

A single QPS number is never sufficient for SquiFlow qualification.

## I. Real implementation considerations

### SquiFlow-specific measurement families

```text
Workstation
  local action latency, startup/restart, local DB contention, disk pressure

Sync
  pending count/bytes, oldest pending age, reconnect drain throughput,
  conflict/retry/upgrade counts

Core/Admin API
  latency percentiles, throughput, error classes, DB pool wait,
  OpenFGA/ZITADEL/provider dependency latency

Worker
  queue depth, oldest-item age, run duration, retry/quarantine/no-progress,
  concurrency by work class

Central DB
  query plans, lock wait, transaction duration, pool saturation,
  WAL/checkpoint/temp/disk behavior

Objects/backup
  transfer throughput, retry, restore time

Observability
  exporter drops, spool pressure, CPU/RAM/network overhead
```

### Implications for the Current Implementation

- **KEEP:** numeric SLO/capacity targets remain measurement-driven rather than guessed.
- **IMPROVE NOW (implementation discipline):** every vertical slice should define what performance evidence it collects before optimization decisions.
- **KEEP:** actual-hardware qualification is required.
- **KEEP:** sync/Worker age metrics matter more than queue depth alone.
- **NEEDS MEASUREMENT:** final pool sizes, concurrency budgets, protocol choices, and latency targets.
- **AVOID:** maximizing QPS as a goal when it harms tail latency, fairness, or correctness.

**Bottleneck question:** what is the first constrained resource under the representative workload? If we cannot answer that, adding cache, more threads, a broker, or another server is speculation.

**Failure cases:** reconnect storm causes DB pool wait but API CPU remains low; telemetry overhead distorts low-end Workstation performance; one tenant dominates expensive jobs; average response time looks fine while p99 times out; external provider throttling makes retries amplify load.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. Define QPS, TPS, concurrency, and response time for one explicit system boundary.
2. What relationship is the source approximating with `QPS = Concurrency / Avg RT`?
3. Why must units/boundary be defined before using that formula?

**Critical reasoning questions**
1. Why can raising concurrency reduce throughput after saturation?
2. Why is average response time inadequate for SquiFlow API qualification?
3. Which metric best reveals a Worker that is slowly falling behind even if depth fluctuates?
4. Why can API CPU be low while latency is high?
5. Which performance numbers should be measured on actual rack hardware rather than developer laptops?

**Trade-off questions**
1. When does batching improve throughput but worsen UX latency?
2. When can more DB connections hurt?
3. When is a cache a correctness regression despite lower latency?

**Failure / edge-case questions**
1. 95% of requests are 50 ms but 5% are 10 s. What does the average hide?
2. Reconnect storm arrives after 100 Workstations were offline. Which metrics predict collapse?
3. Provider returns 429 and every layer retries. What happens to QPS and throughput?
4. One noisy tenant consumes all Worker concurrency. Which metric reveals unfairness?

**Implementation questions**
1. What should be measured before choosing DB pool size?
2. How should latency be decomposed across auth, DB, provider, and queue wait?
3. Which metrics are unsafe as high-cardinality dimensions?
4. How will benchmark workload definitions be versioned/repeated?

**System design interview questions**
1. Diagnose a system with low CPU, high p99 latency, and saturated DB pool.
2. Explain Little's Law in the context of API concurrency and response time.

**Challenge**
During a 100-device reconnect test, Core API CPU is 35%, DB CPU 60%, DB pool wait is high, WAL disk latency spikes, p50 is 120 ms, p99 is 14 s, and sync backlog age keeps rising. Decide what to investigate before adding cache, extra API instances, or a broker.

---
