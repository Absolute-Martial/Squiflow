# Product-to-Quality Traceability

**Status:** Accepted lightweight traceability bridge introduced from Product & Requirements Foundation Batch 3.  
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

## 5. Outcome evidence semantics

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

This is not a requirement for a metrics platform, analytics warehouse or universal dashboard.

Keep separate:

```text
product-outcome evidence
= evidence used to judge whether the promised business/customer result occurred

operational telemetry
= evidence about runtime health, failures and performance

authoritative consumption accounting
= durable usage facts used for limits/cost/reconciliation/support
```

A single data source may contribute to more than one category only when the ownership and semantics are explicit.

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

## 7. Batch 3 consistency review

The Batch 3 review checked the current Product Foundation, tenant permissions, Workstation local-first, sync/authority, NFR and verification contracts.

The existing downstream contracts were already consistent with the new upstream rules in these important respects:

- `LOCAL_FIRST_DESKTOP.md` already limits offline behavior to operations explicitly classified as local-capable/provisional and keeps central authority for shared financial/security facts;
- `SYNC_AND_AUTHORITY.md` already rechecks current authentication, TenantContext, authorization, domain/rule state and concurrency on reconnect;
- `TENANT_PERMISSIONS.md` already makes the Workstation permission snapshot non-authoritative and rechecks OpenFGA/current server state;
- `NON_FUNCTIONAL_REQUIREMENTS.md` already separates hard invariants, operational targets and degraded modes and selects freshness/consistency per invariant;
- `VERIFICATION_STRATEGY.md` already requires observable outcome, failure input, authoritative component, recovery and evidence layer for a testable requirement.

Those owners therefore did not need duplicate wording in this patch. The new traceability bridge supplies the missing upstream business reason and trade-off link without changing their accepted technical semantics.

Future decisions that alter one of those contracts must propagate to the affected owner documents rather than relying on this bridge alone.