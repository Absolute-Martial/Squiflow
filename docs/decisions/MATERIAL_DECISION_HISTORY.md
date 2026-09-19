# Material Decision History and Supersession

**Version:** v0.1.0

**Status:** Accepted convention for preserving the history of material SquiFlow decisions.  
**Authority boundary:** Current authoritative semantics remain in focused owner documents and `docs/decisions/CURRENT_DECISIONS.md`; unresolved choices remain in `docs/decisions/OPEN_DECISIONS.md`. This document owns how material decision rationale is preserved when accepted direction changes. It does not replace owner documents, the decision audit, Git history, or ordinary review records.

## 1. Why this document exists

SquiFlow intentionally changes decisions when stronger evidence shows that an earlier choice is incomplete, too broad, too narrow, or wrong.

Git history shows that text changed. It does not by itself explain which prior decision governed the project, why that decision was reasonable at the time, what evidence changed, or which later decision superseded it.

For material decisions, preserve this chain explicitly:

```text
Decision A accepted for context/evidence at time A
-> new evidence / changed constraint / discovered failure
-> Decision B accepted
-> Decision B supersedes Decision A
-> current owner reflects Decision B
-> Decision A remains readable as historical rationale
```

Do not rewrite history to make the current decision look inevitable.

## 2. What counts as a material decision

Use this convention when losing historical rationale would make future product/domain/architecture work materially harder, especially for decisions affecting:

- product promise or customer scope;
- domain identity, lifecycle, authority, or historical semantics;
- tenant/security/authorization/isolation boundaries;
- local/server authority and sync behavior;
- financial/stock/credit correctness;
- quality attributes driving architecture/cost;
- runtime/process/deployment boundaries;
- externally visible APIs/protocols/contracts;
- provider choices with migration/operational consequences;
- persistent data shape or irreversible migration;
- long-lived configuration/workflow/rule compatibility;
- production recovery/operability obligations.

Do not create a material record for every naming choice, local refactor, dependency version, trivial UI adjustment, or easily reversible choice with no lasting rationale value.

## 3. Minimal material decision record

A material record may live in the decision audit, a focused review/decision record, or another durable repository artifact. It does not require a separate ADR when the focused record already preserves the needed history.

Preserve the smallest useful subset of:

```text
Decision identity / short name
Status: Proposed / Accepted / Superseded / Rejected / Deferred
Context / problem
Decision
Why now
Evidence / assumptions
Alternatives considered
Consequences / trade-offs
Affected owners
Verification / proof obligation
Revisit trigger
Supersedes
Superseded by
Date / repository reference where useful
```

## 4. Supersession rule

When an accepted material decision changes materially:

1. update the current authoritative owner to the newly accepted semantics;
2. create or update a durable rationale record for the new decision;
3. mark the prior material decision **Superseded** rather than silently editing it into the new rationale;
4. link forward/backward where practical;
5. propagate the accepted change to materially dependent owner documents;
6. update implementation/verification obligations when affected.

A superseded record is historical evidence, not current authority.

## 5. Current owner versus historical rationale

Use this hierarchy:

```text
focused owner / CURRENT_DECISIONS
= what SquiFlow currently accepts

OPEN_DECISIONS
= unresolved choice that still needs closure

material decision history / audit / decision record
= why a consequential choice was made or changed

Git history
= exact textual/code changes
```

If these appear inconsistent, the focused current owner governs current semantics, and the inconsistency must be corrected rather than treating the historical record as alternate authority.

## 6. 2026-09-13 accepted supersessions/refinements

The following refinements are material and supersede older shorthand where it conflicts.

### 6.1 Generic shared-module wording -> Capability Core

**Prior shorthand:** business code was repeatedly described as `shared modules` or shared Workstation/server logic without one precise common point.

**Accepted replacement:** capability-owned business modules contain one business meaning; the **Capability Core** names the host-neutral/deterministic center when such a center exists. Foundation remains separate from capability business meaning.

Current owner: `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`.

### 6.2 Blind dual processing -> provisional execution + authoritative admission

**Prior shorthand:** Workstation/server `dual processing` could be read as storing/replaying the same complete transaction twice.

**Accepted replacement:** the Workstation records a local provisional projection plus semantic `OperationEnvelope`; the server performs current trust/authority checks, compares operation dependency revisions, uses a fast admission path when safe, and selectively re-evaluates invalidated decisions before authoritative commit.

Current owners:
- `docs/decisions/DUAL_PROCESSING_AND_IN_PROCESS_COORDINATION.md`;
- `docs/sync/SYNC_AND_AUTHORITY.md`.

### 6.3 One tenant backend ingress -> interactive Web and Sync workload separation

**Prior shorthand:** `services/core-api` represented all ordinary tenant Web and Workstation synchronization traffic indefinitely.

**Accepted replacement:** the current CoreApi remains the compact early host, but interactive Web/API and Workstation Sync are accepted independent future runtime hosts when the split is implemented. They share the same business modules and PostgreSQL authority; they are not separate business backends or sources of truth.

Current owner: `docs/architecture/WEB_AND_SYNC_INGRESS.md`.

### 6.4 Web-only tenant role/device administration -> server-authoritative multi-host tenant administration

**Prior rule:** tenant role/permission assignment and device management were described as Web-only and Desktop as never initiating permission changes.

**Accepted replacement:** Web remains the primary/broader tenant administration surface, but selected tenant-scoped operations such as staff invitation/creation, role/permission assignment and device management may also be surfaced on an **online authorized Workstation** when the capability supports it. The Workstation is only a host adapter: it never grants/revokes authority locally or offline and never receives OpenFGA administrative credentials.

Rule/workflow/form authoring and other administration can remain Web-only when no Workstation product requirement exists. Platform/super-admin control remains Admin Web -> Admin API only and is never exposed through Workstation.

Current owner: `docs/admin/ADMIN_SURFACES.md`.

### 6.5 Simple enable/disable feature flags -> release and experiment management

**Prior rule:** feature state focused primarily on tenant enable/disable and dependency closure.

**Accepted refinement:** feature availability is distinct from release channel, authorization, experiment assignment and domain validity. SquiFlow supports staged `Internal/Preview/Beta/Stable/Deprecated` release state, host/tenant/version targeting, stable A/B assignment for safe product/presentation experiments, Workstation offline feature snapshots, explicit offline policy, and kill-switch/rollback behavior.

Current owner: `docs/architecture/FEATURE_RELEASE_AND_EXPERIMENTS.md`.

### 6.6 "Ingress only" shorthand -> API hosts/adapters with commands and reads

**Problem discovered:** describing WebApi and SyncApi as `ingress only` could be misread as meaning they only receive data and must not participate in query/response paths.

**Accepted refinement:** WebApi and SyncApi are server hosts/API adapters. They own transport/runtime concerns and invoke module commands/queries. WebApi can return data through module-owned query paths; SyncApi supports both upload/admission and pull/download. Neither host owns duplicate Orders/Customers/Inventory business logic or ad-hoc table access.

Current owners:
- `docs/architecture/WEB_AND_SYNC_INGRESS.md`;
- `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`.

### 6.7 Mandatory Core/Server/Workstation projects -> conceptual responsibilities + earned compile-time splits

**Prior diagram risk:** examples such as `Orders.Core`, `Orders.Server`, and `Orders.Workstation` could be interpreted as mandatory projects for every capability.

**Accepted replacement:** `Core`, server-authoritative execution and Workstation-local/provisional execution are conceptual responsibility categories first. A small capability remains one compact project by default. A separate `.Core`/`.Server`/`.Workstation` project is created only when cross-host reuse, provider/platform isolation, compile-time dependency enforcement, packaging, or module complexity earns it.

A provider-specific persistence project can be introduced when concrete EF/Npgsql/SQLite/provider references would otherwise contaminate a host-neutral business project. Empty projects are prohibited.

Current owner: `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`.

### 6.8 Ambiguous "persistence" -> explicit state and data-access semantics

**Problem discovered:** `persistence adapter` could be misread either as a mandatory generic repository layer or as meaning WebApi writes directly to PostgreSQL for every operation.

**Accepted replacement:** persistence means durable state outside transient process memory. SquiFlow distinguishes ephemeral runtime state, durable processing state, authoritative PostgreSQL business state, object storage, and Workstation SQLite/WAL state. Module-owned query paths may read optimized projections directly; authoritative mutations run through the owning module's application/business transaction path. No generic `WebApiDB -> CoreDB` business-database chain is baseline.

Current owners:
- `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`;
- `docs/server/SERVER_STATE_AND_PROCESSING.md`;
- `docs/data/PERSISTENCE_SELECTION.md`.

### 6.9 Reference architecture copying -> reference-informed, SquiFlow-specific decomposition

**Accepted refinement:** Microsoft Clean Architecture/DDD guidance, ABP module layering, and Kamil Grzybek's modular-monolith reference are design evidence, not templates to copy mechanically. They collectively support thin transport hosts, contained persistence concerns and optional compile-time layering. Kamil's reference explicitly notes that Application/Domain/Infrastructure assemblies may be merged when pragmatism warrants it.

SquiFlow therefore adopts the principle:

> modular ownership is mandatory; physical project decomposition is earned.

Reference rationale/current owner: `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`.

## 7. Decision changes must name what changed

A later decision should distinguish the reason for change where useful:

- new customer/product evidence;
- discovered domain invariant or semantic distinction;
- failed implementation/POC/measurement;
- changed operational/provider constraint;
- changed security/privacy/legal obligation;
- changed product promise;
- correction of an earlier mistaken assumption;
- deliberate scope reduction or expansion.

## 8. Rejected and deferred are not erased

A rejected or deferred mechanism can remain useful historical information when it records why SquiFlow did not adopt it.

If later evidence reopens it, create a new decision that explains what materially changed. Do not simply delete the old rejection and replace it with acceptance.

## 9. Relationship to existing decision artifacts

- `docs/decisions/CURRENT_DECISIONS.md` remains the accepted-direction catalogue.
- `docs/decisions/OPEN_DECISIONS.md` remains the unresolved-decision catalogue.
- `docs/review/DECISION_AUDIT.md` remains an important reasoning/correction record and may contain material decision history.
- focused source-review records may preserve evidence that led to a decision but do not override the current owner.
- `docs/product/PRODUCT_FOUNDATION.md` owns evidence and product-decision discipline upstream of material product decisions.

Existing historical corrections do not need to be rewritten retroactively into one-record-per-decision ADRs. Apply this convention prospectively and when a future material change would otherwise make rationale ambiguous.

## 10. Restraint

This convention does not require:

- an ADR for every change;
- a decision ID bureaucracy for trivial choices;
- architecture review boards;
- immutable files that can never receive status/link corrections;
- copying the same current rule into history files and owner documents;
- retaining obsolete rationale as current behavior.

The target is simple:

> Current semantics stay easy to find, while consequential changes remain explainable.
