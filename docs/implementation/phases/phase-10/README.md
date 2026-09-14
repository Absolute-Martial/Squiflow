# Phase 10 — Paying-Customer Production Qualification and Provider Migration Readiness

**All detailed packages:** `docs/implementation/phases/README.md`  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

Phase 10 is the production qualification envelope. It should add as little new application architecture as possible; instead it proves that the accumulated architecture works on the real deployment/support profile.

It is the final parent phase in the current Phase 0–10 roadmap, but it does not mean product development stops. New capabilities, providers, process boundaries, deployment profiles or materially different workloads re-enter the same cumulative maturity/gate model when introduced.

## Subphases

```text
10A  Rack inventory, capacity, SPOF and resource envelope
10B  Replacement-environment restore and security recovery
10C  Immutable release, migration, rollback/roll-forward drill
10D  Provider migration, production limits and operator ownership
10E  Paying-customer production-honesty gate
```

## Continuing development

Feature work may continue while qualification proceeds, but changes intended for the production scope must satisfy all already-reached gates and must not invalidate completed restore/capacity/release evidence without requalification.

## Phase-specific evidence and regression map

- **10A — capacity/SPOF/resource envelope:** representative load/resource tests on the actual deployment class establish measured limits and next-move triggers. Evidence is `RELEASE_CANDIDATE` and reruns after material workload/topology/provider changes. A prior benchmark does not survive a materially different production profile automatically.
- **10B — replacement restore/security recovery:** full restore, key/recovery ceremony, identity/authorization/config/deployment recovery and authorized smoke journey are `OPERATOR_DRILL` evidence with explicit freshness. Material schema/key/provider/recovery-set changes invalidate stale restore evidence.
- **10C — release/migration/rollback-roll-forward:** immutable artifact/provenance/preflight, migration, failed deployment, rollback-or-roll-forward, old-client/durable-work compatibility and authorized smoke checks run for release candidates according to release risk. A release path with no tested failure recovery is `BLOCKED`.
- **10D — provider migration/limits/operator ownership:** migration rehearsal/verification, hard-limit measurement/admission/reconciliation, operator ownership and break-glass/provider-recovery paths receive named owners and recurring/pre-release drills. New provider adapters re-enter qualification rather than inheriting old-provider status.
- **10E — paying-customer gate:** sign-off records the exact product/support/availability/recovery promise, current evidence references, evidence freshness, `BLOCKED = none`, and all active regression guards.

## Evidence invalidation rule

Phase 10 is especially sensitive to stale proof.

Any material change to:

```text
schema/recovery set
key-management/recovery policy
identity/authorization topology
provider
release/migration mechanism
rack/node/storage/network topology
supported Workstation/server version window
major workload/capacity profile
```

must identify which Phase-10 evidence becomes stale and rerun it before the affected production promise is made again.

## Production SLO/error-budget policy

Before the first paying customer, operational targets that are actually promised should have measurable SLIs/SLOs and an owner. Error budgets may govern availability/latency/backlog/recovery objectives and influence release/change policy.

Hard security/correctness/durability promises do not receive an error budget.

## Production-scope freeze during qualification

A release candidate may keep receiving fixes, but adding unrelated new production scope during final qualification invalidates affected evidence and expands the gate. Prefer a narrow production promise with current proof over continuously moving the target.

Passing Phase 10 qualifies only the declared production scope/profile. New capabilities/providers/topologies later re-enter the relevant phase evidence and regression model.