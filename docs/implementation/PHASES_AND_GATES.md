# Sequential Implementation Phases and Gates

**Status:** high-level cumulative roadmap  
**Detailed phase index:** `docs/implementation/phases/README.md`  
**Production-honesty owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/regression owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`  
**Future anticipation ledger:** `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md`

## 1. Roadmap rule

This file defines **maturity direction**, not pre-written implementation.

The roadmap is allowed to say:

- which class of responsibility is likely to come next;
- which earlier foundations it depends on;
- what current architecture must preserve so that future work remains possible;
- what real trigger would activate the phase.

The roadmap must not claim future knowledge by preassigning:

- exact subphase decomposition;
- exact evidence classes/cadences;
- exact hostile/failure test inventories;
- exact transitional state contracts;
- exact provider/runtime mechanisms;
- exact exit criteria for responsibilities that do not yet exist.

The governing sequence is:

```text
real responsibility/workload arrives
        ↓
understand the actual claim
        ↓
activate or reshape the relevant phase
        ↓
derive production intent and scope
        ↓
derive falsifiable evidence
        ↓
derive permanent/recurring regression protection
        ↓
qualify
```

A future phase name is a planning label, not an implementation contract. The labels and ordering may be reshaped if real work proves a different grouping or sequence is clearer.

## 2. Current earned-detail boundary

At the current principles-first rebuild baseline:

```text
Phase 0  detailed / active-earned
Phase 1  detailed trust/security contract
Phase 2  direction only — NOT_INTRODUCED
Phase 3  direction only — NOT_INTRODUCED
Phase 4  direction only — NOT_INTRODUCED
Phase 5  direction only — NOT_INTRODUCED
Phase 6  direction only — NOT_INTRODUCED
Phase 7  direction only — NOT_INTRODUCED
Phase 8  direction only — NOT_INTRODUCED
Phase 9  direction only — NOT_INTRODUCED
Phase 10 direction only — NOT_INTRODUCED
```

Phase 0/1 detailed documents may evolve further as current real work sharpens their claims.

Future phase detail is created only when real implementation/workload earns it.

## 3. Work-in-progress rule

Default WIP remains intentionally small. Prefer one coherent production-honest slice at a time rather than broad speculative implementation.

The WIP rule is not a reason to prohibit adjacent work that is required to make the active slice correct. If a current slice needs a later responsibility, pull that responsibility forward and qualify it now under the global gate contracts.

## 4. Source-first implementation rule

Production-honest does not require writing every mechanism from scratch.

Before custom infrastructure is created for an active responsibility, inspect applicable proven implementations and record whether SquiFlow will use a focused dependency, adapt bounded source when its license permits the intended use and distribution, reuse tests/algorithms, or retain the source only as reference with a concrete rejection reason. License does not exclude a source from internal research. Record the upstream revision, exact source, license, retained authority, framework assumptions, gaps, exit path, and SquiFlow-owned evidence.

The current source map is `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md`. It is routing evidence, not automatic authorization. Custom code remains appropriate for SquiFlow-specific business/authority behavior or a demonstrated candidate gap.

## 5. Cross-phase responsibilities

Some rules apply whenever their responsibility first appears, regardless of nominal phase number.

Examples:

- resource consumption/limits when a real finite/enforced resource appears;
- authentication/authorization when a protected surface appears;
- durability/recovery when durable state appears;
- compatibility/versioning when an independently versioned or durable contract appears;
- idempotency/concurrency when retryable/shared mutation appears;
- observability/audit when a material operational/security state transition appears;
- secret/key handling when sensitive credentials/key material appear;
- process lifecycle/fault isolation when a new executable appears.

Do not wait for a later roadmap label to implement correctness that the current responsibility already requires.

## 6. Phase 0 — architectural development foundation

Phase 0 remains the active detailed rebuild foundation.

Its detailed owner package is `docs/implementation/phases/phase-0/`.

Current focus includes only the architecture/kernel/module/host/engineering-safety responsibilities real implementation actually reintroduces. The detailed Phase-0 package determines exact current evidence; this file does not duplicate it.

Phase 0 may also host real capability work. A phase is a maturity floor, not a whitelist of folders/features.

## 7. Phase 1 — identity, tenant authorization, session and trust foundation

Phase 1 remains detailed because the accepted user/tenant trust architecture already establishes concrete identity/authorization/session boundaries that must be specified carefully.

Its detailed owner package is `docs/implementation/phases/phase-1/`.

Detailed security evidence, transitional restrictions and authorization-change semantics live there rather than in this parent roadmap.

## 8. Phase 2 — anticipated local-first Workstation durability/recovery direction

**State:** `NOT_INTRODUCED` as a qualified phase.

Likely responsibility class: trustworthy local/provisional business durability and recovery for the first real offline-capable Workstation operation.

Activation trigger: a real operation must be durably accepted locally before server confirmation, or another real local-state requirement makes the same durability contract necessary.

Current preservation constraints:

- local/provisional and central authoritative state remain distinct;
- transient UI/process memory is never described as durable;
- Guard remains supervision/recovery rather than business authority;
- no ad-hoc local persistence path bypasses accepted encryption/migration/recovery ownership.

Exact persistence/encryption/outbox/Guard/update evidence is written when the real operation exists.

## 9. Phase 3 — anticipated central authority/synchronization direction

**State:** `NOT_INTRODUCED` as a qualified phase.

Likely responsibility class: authoritative central persistence/admission and real synchronization where a current workload requires it.

Activation trigger: a real capability needs central authoritative commit and/or a local-capable operation needs server admission/sync.

Current preservation constraints:

- host-neutral capability meaning;
- local success is not central acceptance;
- semantic operation/version contracts stay transport-independent where possible;
- no artificial network boundary between ordinary modules in one process.

Exact PostgreSQL, idempotency/concurrency, sync, migration/version, transport and workload evidence is derived then.

## 10. Phase 4 — anticipated long-offline/conflict/recovery direction

**State:** `NOT_INTRODUCED`.

Likely responsibility class: long-offline compatibility, conflict handling, resnapshot/rebase and stale-authority refresh after real local/server coexistence exists.

Activation trigger: supported offline duration/version drift exceeds the currently qualified sync contract.

Current preservation constraints:

- pending local intent must remain recoverable;
- stale security/payment/stock/credit/limit state cannot become authoritative;
- capability-owned version/conflict semantics remain explicit where introduced.

Exact conflict/resnapshot behavior is capability-derived when the need appears.

## 11. Phase 5 — anticipated configurable behavior direction

**State:** `NOT_INTRODUCED` unless a subset is pulled forward.

Likely responsibility class: bounded versioned rules/workflow/forms/configuration where real product variability earns them.

Activation trigger: a real capability needs variability that simpler strongly typed behavior/settings cannot represent honestly.

Current preservation constraints:

- strongly typed behavior remains the default;
- no arbitrary tenant code execution;
- historical/issued truth is not silently recomputed from mutable configuration.

Exact rule/workflow/form mechanisms are not selected by this roadmap.

## 12. Phase 6 — anticipated independent Admin/Worker direction

**State:** independent Admin/Worker runtime boundaries `NOT_INTRODUCED`.

Likely responsibility class: independently secured platform control and/or independent durable background execution when a real workload earns those process boundaries.

Activation trigger: first real control-plane operation needing independent security/availability, or first durable background workload needing an independent execution lifecycle.

Current preservation constraints:

- platform permissions/audit may exist without Admin executables;
- transactional consequence/outbox semantics may exist without Worker;
- once durable work exists, process memory cannot be its authority;
- process boundaries require real lifecycle/fault/security/resource justification.

Admin and Worker may activate at different times; the roadmap does not require them to arrive together.

## 13. Phase 7 — anticipated files/documents/backup direction

**State:** `NOT_INTRODUCED` as a qualified provider-bound package.

Likely responsibility class: provider-backed objects/attachments, document/printing effects and recovery assets.

Activation trigger: the first real capability needs one of those responsibilities.

Current preservation constraints:

- provider SDKs stay behind adapters;
- file providers are not hidden business authority;
- semantic sync and large-file transfer remain separable;
- backup claims require real restore evidence once introduced.

Exact provider, integrity, capacity, printing and restore evidence is derived from the real workload/provider topology.

## 14. Phase 8 — anticipated cross-system qualification direction

**State:** `NOT_INTRODUCED` as a standalone qualification package.

Likely responsibility class: combined attack/failure/load/observability qualification after enough real topology exists for cross-boundary effects to matter.

Activation trigger: real integrated components/providers/workloads make cross-system behavior materially different from individual component evidence.

Current preservation constraints:

- no current security/recovery/observability/resource responsibility may be postponed here;
- no broad SLO/load/security claim is invented before representative topology/workload exists.

Exact qualification scenarios are intentionally unknown until then.

## 15. Phase 9 — anticipated protected financial/shared authority direction

**State:** `NOT_INTRODUCED` as a qualified package; individual capabilities may be pulled forward.

Likely responsibility class: external financial effects and shared current-authority facts such as payment/refund, stock, credit or historical pricing.

Activation trigger: real protected business scope requires one of these semantics.

Current preservation constraints:

- external effects require semantic identity/ambiguity/reconciliation once introduced;
- stale local state cannot silently become final shared authority;
- historical financial truth follows explicit correction/reversal semantics.

Exact provider/business/concurrency evidence is derived from the actual capability.

## 16. Phase 10 — anticipated paying-customer qualification direction

**State:** `NOT_INTRODUCED` as a final production qualification envelope.

Likely responsibility class: qualification of the actual first-paying-customer promise against the actual deployment/provider/support profile.

Activation trigger: paying-customer scope, supported clients, providers, rack/topology, operators and workload are concrete enough to qualify.

Current preservation constraints:

- earlier gates keep their permanent regression guards;
- recovery cannot depend on undocumented machine knowledge;
- material provider/topology/workload changes invalidate stale qualification evidence.

Only then are exact rack capacity, restore/recovery, release/rollback-or-roll-forward, provider migration, RPO/RTO and operator evidence specified.

## 17. Future anticipation belongs in the ledger

Detailed thinking that may inform Phases 2–10 is preserved in `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md`.

That document is deliberately non-authoritative. It records likely questions, current preservation constraints and activation triggers without pretending to know future evidence/cadence/subphase design.

When future work activates, its implementation owner may adopt, modify, merge, split, reorder, rename or reject those anticipations.

## 18. Deferred architecture remains genuinely deferred

Do not introduce technologies/mechanisms merely because they are common architecture patterns or once appeared in a future plan. Examples include generic repositories/unit-of-work, browser-offline/PWA sync, mandatory Redis/Kafka, event-driven-everything, full CQRS/event sourcing/Saga, GraphQL/Federation, service mesh, Kubernetes, database-per-service, HTTP/gRPC between ordinary same-process modules, global CRDTs, arbitrary helper processes, broad polyglot persistence, generic metering/analytics infrastructure, or unearned protocol/provider abstractions.

If a real workload earns one later, evaluate it under the current workload-selection and production-honesty owners rather than treating this roadmap as permission.
