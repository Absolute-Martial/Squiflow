# Product-to-Quality Traceability

**Status:** Accepted lightweight traceability bridge introduced from Product & Requirements Foundation Batch 3 and refined by Batch 4.  
**Authority boundary:** This document connects accepted product/business consequences to quality requirements, architecture consequences and verification evidence. It does not replace `docs/product/PRODUCT_FOUNDATION.md`, `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md`, focused domain/security/sync owner documents, or `docs/testing/VERIFICATION_STRATEGY.md`.

## 1. Why this bridge exists

SquiFlow already has a strong NFR model and detailed downstream failure/authority/recovery semantics. Those requirements still need an explicit upstream reason when a quality characteristic materially affects cost, architecture, product scope or customer expectations.

Use this chain:

```text
business/product outcome
→ consequence if quality is poor
→ quality characteristic
→ relative priority/trade-off
→ requirement class/owner
→ architecture consequence if any
→ verification evidence
```

A quality attribute is not justified merely because architecture literature recommends it or because a technology can provide it.

## 2. Lightweight trace record

For a consequential quality characteristic, record the smallest useful subset of:

| Field | Meaning |
|---|---|
| Product promise / journey | The customer/business outcome being protected. |
| Actor/context | Who is affected and under what operating conditions. |
| Failure consequence | What actually goes wrong for the business if the quality is poor. |
| Quality characteristic | Accuracy, freshness, latency, availability, offline continuity, recoverability, explainability, security, capacity, etc. |
| Relative priority | What this quality may trade against and why. |
| Requirement status | Accepted, MeasuredProvisional, OpenBeforeProduction, Deferred, or NotBaseline where the NFR model applies. |
| Detailed owner | Domain/security/workstation/sync/NFR/etc. document that owns the exact semantics. |
| Architecture consequence | Only the mechanism/boundary that the requirement actually earns. |
| Evidence / target | Observation, measurement, scenario, artifact or target used to judge adequacy. |
| Verification | Test, failure injection, actual-hardware qualification or other evidence proving the requirement. |
| Revisit trigger | What new evidence would change the trade-off or mechanism. |

Do not create entries for trivial qualities that do not influence a real decision.

## 3. Trade-offs must be explicit where material

Common SquiFlow examples include:

```text
freshness vs accuracy
latency vs current authority
availability vs fail-closed security
local continuity vs current shared stock/credit truth
maximum configurability vs simple operation
immediate synchronization vs bounded reliable recovery
```

There is no universal winner.

For example, `near real time` is not automatically superior to a slower but more accurate/explainable result. Likewise, an offline-capable action is not automatically better than a server-required action when current shared authority is necessary for correctness.

## 4. Offline/local-first traceability

The Workstation architecture remains local-first, but the product/domain decision determines which operation belongs in each authority class.

For a material operation being considered for offline use, answer:

```text
What business work must continue?
Who performs it?
What happens if it cannot continue?
How long can interruption be tolerated?
What information must be available locally?
How fresh/accurate must that information be?
Which facts require current central authority?
What harm occurs if stale data is accepted?
Is the action Local-capable, Local-provisional, or Server-required?
```

Then the existing owner documents define the mechanics:

- `docs/workstation/LOCAL_FIRST_DESKTOP.md` — local durability, authority classes and Workstation behavior;
- `docs/sync/SYNC_AND_AUTHORITY.md` — server revalidation, reconciliation and conflict behavior;
- `docs/security/TENANT_PERMISSIONS.md` — current authorization versus local permission snapshots;
- `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md` — degraded mode, durability, freshness and recovery requirements.

Do not classify an operation as offline-capable merely because the desktop implementation can technically run it without a network call.

## 5. Measurement semantics and proxy discipline

Product-outcome measures need stable meaning before they are used for scope or strategy decisions.

Where a measure is consequential, define:

```text
measure / evidence name
→ decision or outcome it informs
→ meaning
→ population / scope
→ authoritative or observational source
→ calculation / interpretation
→ time window
→ freshness
→ known uncertainty / error
→ who interprets it
→ what decision it may change
```

Before collecting or elevating a measure, ask:

```text
What do we mean by this measure?
Why do we care?
Which decision can change because of it?
What behavior could this metric accidentally incentivize?
What important reality could improve or worsen while the metric moves in the desired direction?
```

Keep these categories distinct unless a documented relationship exists:

```text
product-outcome evidence
= evidence used to judge whether the promised business/customer result occurred

operational / NFR evidence
= evidence about runtime quality, failures, capacity, latency, recovery, security or similar characteristics

delivery-progress metric
= evidence about completed/in-progress delivery work

engineering diagnostic / code-quality proxy
= evidence such as coverage, test count, complexity, defect counts or similar engineering signals

authoritative consumption accounting
= durable usage facts used for limits/cost/reconciliation/support
```

A metric is not promoted from one category to another merely because it correlates conveniently. Test count, code coverage, story count, story points, tasks completed, hours remaining, incident count or deployment frequency may be useful for a specific decision; none automatically proves customer value or overall product quality.

This is not a requirement for a metrics platform, analytics warehouse or universal dashboard. A single data source may contribute to more than one category only when the ownership and semantics are explicit.

## 6. Cross-owner consistency check

When a product or quality decision changes materially, inspect every affected owner rather than editing one document in isolation.

A dependent owner must be updated when leaving it unchanged would:

- contradict the accepted decision;
- imply a different authority/freshness model;
- retain a stale product or failure assumption;
- omit a required implementation/recovery obligation;
- make verification unable to prove the new requirement;
- create different meanings for the same term or measure.

Do not change a document merely for symmetry when its existing contract is already consistent.

## 7. Bridge restraint

This file must remain a traceability bridge, not a second NFR catalogue or a metrics governance platform.

Use the smallest record that explains a consequential quality decision. Detailed semantics stay in the focused owner. If this file starts duplicating retry rules, authorization behavior, sync semantics, exact SLOs, provider contracts or test procedures, move that detail back to the correct owner and keep only the link and business reason here.

## 8. Batch 3–4 consistency review

The Batch 3 review checked Product Foundation, tenant permissions, Workstation local-first, sync/authority, NFR and verification contracts. Those downstream contracts were already consistent with the new upstream business-to-quality rules, so they were not duplicated here.

Batch 4 further checked whether product-outcome measurement should replace engineering or delivery metrics. It should not. The categories above are complementary, and each must remain tied to the decision it actually informs.

Future decisions that alter downstream semantics must propagate to the affected owner documents rather than relying on this bridge alone.

## 9. Handoff to workload-driven architecture selection

When the trace above creates a **material architecture choice**, hand the accepted product/quality consequence to `docs/architecture/WORKLOAD_STRATEGY_SELECTION.md`.

The responsibilities remain separate:

```text
Product-to-Quality Traceability
    -> why a quality matters
    -> who is affected
    -> consequence if poor
    -> relative priority / requirement class
    -> evidence or target

Workload Strategy Selection
    -> which plausible mechanism best satisfies those requirements
    -> which candidates violate HardInvariants
    -> complexity / total-cost obligations
    -> primary + degraded/recovery behavior
    -> rejected alternatives
    -> verification + falsification/revisit trigger
```

Do not let the architecture-selection comparison manufacture a product requirement that has no upstream consequence/evidence. Conversely, do not force this traceability bridge to name a technology before the workload and owner-specific constraints have been evaluated.

For a consequential selection, it can be useful to add only these links to the trace record rather than duplicating the architecture decision here:

- selected strategy / decision reference;
- why that mechanism is an architecture consequence of this quality need;
- verification evidence;
- revisit trigger.

This preserves the bridge as lightweight traceability while making the chain from product consequence to architecture choice auditable.
