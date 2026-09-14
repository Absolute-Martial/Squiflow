# SquiFlow Master Implementation Plan

**Baseline:** v0.0.20  
**Status:** Current high-level implementation direction. Detailed scope, evidence, and phase shape are earned from real responsibilities rather than pre-specified here.

## 1. Canonical governance

This file is an index and high-level implementation direction, not a second detailed roadmap.

Current governing owners are:

- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md` — scope/quality contract;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` — evidence permanence, regression, transition, and requalification contract;
- `docs/implementation/PHASES_AND_GATES.md` — high-level maturity direction;
- `docs/implementation/phases/phase-0/` — current detailed rebuild work;
- `docs/implementation/phases/phase-1/` — currently earned detailed trust/security direction;
- `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md` — non-authoritative future anticipation;
- focused architecture/domain/security/data/workstation/server owners for the exact responsibility being changed.

If this file ever conflicts with a focused canonical owner or the global phase-gate contracts, the focused/global owner governs and this file must be reconciled.

## 2. Phase-0A reset snapshot and handoff

The Phase-0A `v0.0.20` principles-first reset snapshot intentionally contains no production/test `.csproj` projects and an empty `SquiFlow.sln`.

That zero-project state is **qualification-time reset evidence**, not a permanent target. Subsequent active work is expected to introduce newly earned projects.

Folder names, architecture diagrams, old branches, old merge requests, historical source studies, and the retired pre-reset implementation plan do not prove that a runtime/project currently exists.

Phase 0A establishes the developer/repository baseline. Phase 0B must be derived from real current capability/application pressure rather than by rebuilding the old ApplicationKernel or old project tree.

## 3. Governing implementation sequence

For every active slice:

```text
real responsibility / workload
        ↓
focused owner + current decisions/open decisions
        ↓
production intent
        ↓
exact scope and non-scope
        ↓
NOT_INTRODUCED / PRODUCTION_HONEST / BLOCKED
        ↓
simplest production-honest implementation
        ↓
falsifiable evidence at the owning layer
        ↓
permanent / recurring regression guard
        ↓
requalification triggers
        ↓
qualify only with BLOCKED = none
```

A phase number is planning/maturity vocabulary. It is not authority to create a project, provider, process, interface, protocol, test matrix, or runtime behavior.

## 4. Engineering direction that remains accepted

Unless a focused owner is deliberately changed in the same work:

- C# / modern .NET 10 remains the application/toolchain direction.
- Modular monolith first; ordinary capability interaction stays in-process.
- One capability owns one business meaning.
- Foundation remains narrow and product-wide; it is introduced only when current consumers earn shared semantics.
- Physical project/process/provider/interface boundaries are earned by real compiler, provider, platform, security, fault, lifecycle, resource, packaging, compatibility, or workload pressure.
- Workstation remains the local-first/offline direction; Web remains online-only for business operations unless deliberately changed.
- PostgreSQL remains selected central transactional authority when central persistence is introduced.
- SQLite/WAL remains selected Workstation local/provisional persistence when local persistence is introduced.
- Guard remains an external supervision/recovery boundary, not business authority.
- ZITADEL, OpenFGA, and OpenBao/Vault-style key-management directions remain accepted where their owning scopes are activated.
- Same-process modules do not communicate through HTTP/gRPC merely to imitate microservices.
- No generic repository/unit-of-work/interface-per-class framework is baseline.
- No future host/process/provider is created merely because an old plan or diagram contained it.

Detailed semantics remain in their focused owners rather than being duplicated here.

## 5. Current phase relationship

```text
Phase 0  detailed / active-earned architectural development foundation
  0A     qualified repository/architecture reset baseline
  0B     next active implementation work — derive capability-first from current facts
Phase 1  detailed trust/security direction already concrete enough to govern
Phase 2  direction only — NOT_INTRODUCED
Phase 3  direction only — NOT_INTRODUCED
Phase 4  direction only — NOT_INTRODUCED
Phase 5  direction only — NOT_INTRODUCED unless a subset is pulled forward
Phase 6  direction only — NOT_INTRODUCED unless a subset is pulled forward
Phase 7  direction only — NOT_INTRODUCED unless a subset is pulled forward
Phase 8  direction only — NOT_INTRODUCED
Phase 9  direction only — NOT_INTRODUCED unless a capability is pulled forward
Phase 10 direction only — NOT_INTRODUCED
```

The exact future subphase decomposition, provider/runtime mechanism, evidence classes, cadences, failure inventories, transitional contracts, and exit criteria are **not** pre-authorized by this plan.

When a real responsibility arrives, activate or reshape the relevant phase from current facts. Old detailed future phase material remains Git history/anticipation, not success criteria.

## 6. Pull-forward rule

If current work needs a responsibility associated with a later roadmap area:

```text
real need appears
→ identify likely focused owner/direction
→ promote from NOT_INTRODUCED
→ declare current production intent and scope
→ implement to PRODUCTION_HONEST now
→ add evidence + permanent regression guard
→ continue
```

Do not implement a temporary unsafe substitute on the assumption that a later phase will repair it.

## 7. Product-development boundary

Technical phase completion is not automatically a customer/product promise.

`docs/product/PRODUCT_FOUNDATION.md` remains upstream for customer evidence, first customer ecosystem, first end-to-end product promise, and outcome validation. Technical slices may proceed before those questions are fully closed, but they must not be presented as validated customer scope merely because code exists.

Consequential domain terminology must follow `docs/domain/BUSINESS_TERMS.md`; do not freeze disputed words into durable/API/persistence meaning merely because an old implementation used them.

## 8. Verification rule

Evidence is derived from the claim, not from the phase label or implementation shortcut.

Use the strongest applicable layer for the property actually introduced: static/architecture, deterministic unit/property, real integration, process-failure, migration/restore, security-hostile, load/capacity, or operator evidence as appropriate.

Mocks do not prove behavior owned by a real provider/database/framework/process boundary.

A qualified material claim keeps a permanent or recurring regression guard and explicit requalification triggers. `Manual once` is not a regression strategy.

## 9. KISS, YAGNI, and SOLID

KISS means **smallest production-honest scope**: minimum accidental complexity while completely satisfying the current declared responsibility.

YAGNI prevents speculative breadth, not required correctness/security/durability/recovery/compatibility/resource/observability depth for a responsibility already introduced.

SOLID is pressure-testing guidance for real ownership/change/replacement/fault/security boundaries. It does not mandate one interface per class, generic repositories, forwarding layers, or project-count symmetry.

## 10. Historical master plan

The former v0.0.18 fixed Phase 0–10 implementation plan is superseded as current governance. It remains available in Git history for rationale and architectural questions.

Do not copy its old phase checklist, project inventory, host inventory, evidence list, or future subphase decomposition into new implementation merely because it once appeared here. Re-earn useful ideas from the current responsibility and focused owners.
