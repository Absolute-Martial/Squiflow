# Workload Strategy Selection Decision — 2026-09-14

**Status:** Accepted refinement  
**Version:** v0.0.21  
**Current owner:** `docs/architecture/WORKLOAD_STRATEGY_SELECTION.md`

## 1. Audit result

The proposed strategy-selection model is directionally correct, but most of its raw ingredients already existed across SquiFlow as review and requirement discipline rather than one canonical architecture-governance owner.

Existing rules already required:

- technology comparisons to be converted into boundary/workload fit analysis rather than winner/loser selection;
- current mechanism, exact reason, alternative fit, adoption evidence and falsification evidence to be recorded;
- every material proposal to name the real problem/invariant, simplest credible alternative, new failure/operations cost, authority, recovery and small-team operator burden;
- architecture to remove accidental complexity rather than required capability;
- `HardInvariant`, `OperationalTarget`, and `DegradedMode` requirement classes;
- numerical quality targets to be measured on representative hardware/workloads rather than invented;
- quality decisions to trace back to product/business consequences;
- fallback/degraded/recovery behavior to be explicit where material;
- caches, brokers, gRPC, GraphQL, Kubernetes, service extraction, streams and similar mechanisms to be requirement/measurement-triggered rather than maturity steps;
- batch versus streaming to be decided from latency/completeness/replay/order/late-data/operations needs rather than technology preference.

The gap was **integration**: these rules lived across review methodology, NFRs, traceability, implementation gates and focused owners. This decision promotes them into one cross-cutting architecture-selection model.

## 2. Accepted refinements

### 2.1 Workload profile becomes first-class architecture evidence

Material choices now start from an explicit workload/boundary profile where relevant, including:

- business consequence;
- authority/freshness;
- latency/completeness;
- size/volume/burstiness;
- ordering/event-time/late-data needs;
- availability/degraded mode;
- offline requirement;
- consistency;
- replay/recovery;
- compatibility/version overlap;
- client diversity;
- security/privacy;
- hardware/provider resource constraints;
- operator capacity;
- total cost.

Unknowns are measurement obligations rather than guessed values.

### 2.2 Hard invariants filter before weighted trade-offs

SquiFlow does not use a universal score in which enough performance/cost benefit can offset tenant isolation, security, financial correctness, durable intent, or another accepted HardInvariant.

Candidate mechanisms that violate a hard invariant are eliminated before optimization among the remaining candidates.

### 2.3 Complexity budget is accepted as a qualitative review concept

Every additional process, protocol, store, compatibility surface, provider, distributed state mechanism, credential lifecycle, recovery path or specialist tool must be justified by a named requirement.

`Complexity budget` is **not** a numeric architecture score or arbitrary project quota.

### 2.4 Total cost is broader than engineering simplicity

Material alternatives may need comparison across infrastructure/provider cost, engineering effort, operator/support burden, verification, failure/recovery, security/privacy, compatibility/migration, exit/lock-in, resource cost, and the business cost of latency/staleness/unavailability.

The comparison can remain qualitative where exact monetary conversion would create false precision.

### 2.5 Primary/degraded/recovery/reconciliation behavior is part of the decision

Where meaningful, a selected strategy records:

- normal primary path;
- safe degraded behavior;
- fallback only when semantic guarantees remain acceptable;
- recovery to normal operation;
- reconciliation for ambiguous/divergent durable state.

Some operations correctly fail closed rather than use a weaker fallback.

### 2.6 Rejected alternatives are preserved for material decisions

Plausible alternatives should record why they did not fit the exact workload now, instead of being globally labeled inferior.

This makes future re-evaluation possible when constraints change.

### 2.7 Processing choices are not a maturity ladder

Synchronous work, durable jobs/batch, incremental processing, micro-batch and stream/event-log processing are different fits.

Streaming earns its complexity only when a real workload needs properties such as sustained continuous low latency, replayable independently consumed history, event-time/windows, late/out-of-order handling, or stateful continuous processing.

A stream is not selected simply because events occur over time.

### 2.8 Hybrid architecture is explicitly allowed but must remain explainable

Different workloads may correctly use different API/protocol/processing/storage mechanisms. One workload may itself combine local reads, sync, an authoritative command, Worker consequences and optional live signals.

Each mechanism must own a distinct justified responsibility; hybrid architecture is not automatically superior to a simpler path.

### 2.9 Material decision records gain a reusable minimum shape

When the choice is consequential enough to preserve, record the smallest useful subset of:

```text
workload / boundary
business consequence
authority
hard invariants
operational/degraded requirements
workload evidence and unknowns
current mechanism
plausible candidates
selected strategy
rejected alternatives
complexity/TCO obligations
degraded/fallback/recovery/reconciliation
compatibility/migration/exit
verification evidence
falsification/revisit trigger
owner/status/supersession
```

This complements, rather than replaces, the material-decision-history convention.

### 2.10 “Architecture Decision Engine” remains methodology, not runtime infrastructure

The uploaded proposal's `Architecture Decision Engine` concept is accepted only as an informal description of the governed method.

Current baseline is documentation + evidence + review + tests/POCs. SquiFlow does not add:

- an AI architecture selector;
- a runtime strategy-selection service;
- one weighted scoring algorithm;
- one framework that dynamically switches protocols/stores/processing modes.

Future tooling may check decision completeness or link evidence, but it cannot silently select architecture or override authority/invariants.

## 3. Relationship to existing owners

This refinement **promotes** existing rules rather than superseding them.

- `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` still governs how external comparisons are interpreted.
- `CRITICAL_INTERROGATION_RULE.md` still governs skeptical review questions.
- `NON_FUNCTIONAL_REQUIREMENTS.md` remains the owner of HardInvariant/OperationalTarget/DegradedMode semantics.
- `PRODUCT_TO_QUALITY_TRACEABILITY.md` remains the upstream product/business-to-quality bridge.
- `MATERIAL_DECISION_HISTORY.md` remains the historical/supersession convention.
- focused owners still decide the concrete mechanism for API, sync, persistence, Worker, security, deployment, Workstation, etc.

`WORKLOAD_STRATEGY_SELECTION.md` now owns the cross-cutting method by which those concrete mechanisms are selected/revisited.

## 4. Corrections to generic examples in the proposal

Some generic examples are useful only as candidates, not automatic mappings.

The following are **not** adopted as universal SquiFlow rules:

```text
high-frequency internal call -> gRPC
asynchronous event -> broker
client diversity -> BFF / GraphQL
unbounded data -> streaming
versioned contract -> runtime schema registry
high availability -> distributed architecture
```

Each remains a positive candidate only when the exact workload requires its property strongly enough to justify the added failure/operations/compatibility surface.

Likewise, the example `Inventory dashboard -> micro-batch/stream + GraphQL + gRPC + distributed cache` is not accepted as an architecture decision without measured inventory-dashboard requirements. It remains an illustration of how a decision record might compare alternatives.

## 5. Batch/stream clarification from existing review material

The repository already contains a useful direction that this decision makes canonical:

- bounded imports, backups, reports, rebuilds and many Worker tasks fit batch/incremental mechanisms;
- not every sync admission belongs behind a queue;
- live signals do not imply stream processing;
- stream/event-log infrastructure becomes relevant only for a genuine continuous low-latency/replay/order/event-time workload;
- periodic reconciliation can be a repair strategy but is not universally mandatory.

Future processing decisions should use the workload profile rather than treating batch -> micro-batch -> streaming as increasing architectural maturity.

## 6. What remains open

This decision does not select:

- one exact scoring/ranking formula;
- exact numeric complexity budgets;
- a universal architecture questionnaire for trivial changes;
- streaming/Kafka/RabbitMQ;
- GraphQL/BFF;
- gRPC as final Sync transport;
- distributed cache;
- Kubernetes/service mesh;
- a runtime schema registry;
- a new data-processing platform;
- AI-driven architecture decisions.

Those stay governed by their real workload/adoption gates.

## 7. Architectural invariant

> **SquiFlow chooses architecture from the actual workload and protected requirements. Hard invariants constrain the candidate set first; the remaining strategies are compared by quality fit, failure/recovery behavior, compatibility, total cost and small-team complexity. The smallest correct mechanism wins unless evidence justifies paying for more, and every material choice retains recovery and falsification/revisit evidence.**
