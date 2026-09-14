# Critical Architecture Interrogation Rule

**Status:** Authoritative review-method refinement for the remaining ByteByteGo exhaustive study.  
**User instruction:** do not merely record what a source suggests; **critically ask what SquiFlow is actually doing and why**.

This rule complements `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md`.

## Purpose

Every architecture/pattern/technology exposure must be challenged before it becomes an implementation implication. The review must distinguish:

```text
source says a mechanism exists
    != SquiFlow needs the mechanism

SquiFlow currently uses a mechanism
    != it is globally best

mechanism improves one metric
    != total system/recovery/security cost improves
```

## Required interrogation

For every material implication, ask:

1. **What exactly are we doing?** Name the concrete SquiFlow boundary, operation, data flow, runtime, state or mechanism.
2. **What real problem are we solving?** Name the business invariant, failure mode, security requirement, operational burden, performance bottleneck or customer need.
3. **Why does this mechanism solve that problem?** State the specific property that creates value.
4. **Is the problem real now or speculative?** Do not create infrastructure from future possibility alone.
5. **What is the simplest credible alternative?** Include keeping the current in-process/store/host mechanism when appropriate.
6. **What new failure/correctness/operations cost does the proposal introduce?** Include recovery, versioning, security, observability, capacity and debugging.
7. **What authority does it own?** State whether the mechanism is authoritative, derived, cached, replayable, advisory or disposable.
8. **What remains unchanged?** A new transport/cache/broker/orchestrator/token/read model must not silently replace unrelated authorization, idempotency, domain or durability requirements.
9. **What evidence would justify adoption?** Measurement, POC, failure history, customer requirement, scale trigger, operational burden or protocol need.
10. **What evidence would make us change/reject it?** Record the falsification condition rather than only evidence in favor.
11. **How do we recover when it fails?** A proposal without degraded/recovery behavior is incomplete.
12. **Who has to operate and understand it?** Complexity must fit SquiFlow's small-team environment.

## Mandatory review sentence

For a current mechanism:

> **We are doing `<mechanism>` at `<boundary>` because `<specific property>` protects/solves `<specific requirement>`. We would change it if `<evidence/trigger>` shows `<failure/cost/need>`.**

For a candidate:

> **We are not adding `<mechanism>` yet because `<problem>` is absent or unproven. The simplest current mechanism is `<current approach>`. Adoption requires `<specific evidence>` and must own `<new failure/recovery obligations>`.**

## Critical questions must target SquiFlow, not only the article

Every detailed study should include questions that force review of the current project, such as:

- Which invariant are we protecting?
- What is the actual transaction/authority boundary?
- Why is this a process/service rather than an in-process module?
- Why is this data duplicated/cached/indexed, and how is it deleted/rebuilt?
- Why is this retry/concurrency level safe under real DB/provider capacity?
- What happens after response loss, restart, partial effect or version skew?
- What simpler design was considered?
- Which metric or incident would prove the current design is inadequate?

## Decision discipline

This rule does **not** authorize silent owner-architecture changes. If the interrogation reveals a material change to accepted runtime, storage, identity/authorization, deployment, recovery or major phase scope:

1. record the proposed change and evidence in the study;
2. surface it to the user;
3. obtain approval before changing owner architecture documents.

Review artifacts and coverage ledgers may continue to be updated without that approval.

## Starting point

The rule is applied explicitly from archive batch `071-080` onward. Earlier entries remain governed by the existing fit/use retrospective audit and may be challenged again when later sources expose a contradiction or stronger question.

## Workload-strategy promotion

For material choices, the interrogation now hands off to `docs/architecture/WORKLOAD_STRATEGY_SELECTION.md` rather than stopping at `technology X is a candidate`.

Before recommending a consequential mechanism, also make explicit:

1. **Which requirements are non-tradable HardInvariants?** Eliminate candidates that violate them before comparing latency, cost or convenience.
2. **What workload evidence actually changes the choice?** Include only relevant dimensions such as latency/freshness, completeness, size/volume/burst, ordering/event time/late data, availability/degraded mode, offline, consistency, replay/recovery, compatibility, client diversity, security/privacy and resource/operator constraints.
3. **What is the total complexity/TCO obligation?** Include infrastructure, implementation, operations, verification, failure/recovery, security, compatibility/migration/exit and business cost of latency/staleness/unavailability where material.
4. **What is the primary strategy and what happens when it cannot run?** Name degraded/fail-closed/fallback behavior without silently weakening authority.
5. **How does recovery/reconciliation return the system to a trustworthy state?** A fallback that merely hides divergence is not recovery.
6. **Which plausible alternatives were rejected for this workload and why?** Preserve the reason so later evidence can reopen the decision.
7. **What evidence closes the decision and what evidence reopens it?** Adoption and falsification/revisit triggers are both required for material decisions.

`Complexity budget` in this context is qualitative. Do not invent a universal numeric score or let weighted optimization trade away a HardInvariant.

Likewise, hybrid architecture is allowed only when each mechanism has a distinct job. `REST + GraphQL + gRPC + broker + stream + cache` is not automatically more adaptable; each added surface must earn its own failure, compatibility, recovery and operator cost.
