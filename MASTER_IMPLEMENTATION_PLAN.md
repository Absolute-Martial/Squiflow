# SquiFlow Master Implementation Plan

**Product version:** v0.1.0, locked until the complete production-capable product gate
**Status:** Current high-level implementation direction. Detailed scope, evidence, and phase shape are earned from real responsibilities rather than pre-specified here.

## 1. Canonical governance

This file is an index and high-level implementation direction, not a second detailed roadmap.

Current governing owners are:

- `README.IMPLEMENTATION.md` — current repository implementation truth;
- `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md` — current purge decision;
- `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` — source-first routing;
- `docs/review/WORKSTATION_FRAMEWORK_ADMISSION_RESEARCH.md` — Workstation-specific source admissions and proof-gated implementation route;
- `reference-sources/` — curated source-only upstream snapshots and their admission manifest; never product/runtime code;
- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md` — scope/quality contract;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` — evidence permanence, regression, transition, and requalification contract;
- `docs/implementation/PHASES_AND_GATES.md` — high-level maturity direction;
- `docs/implementation/phases/phase-0/` — current detailed rebuild work;
- `docs/implementation/phases/phase-1/` — currently earned detailed trust/security direction;
- `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md` — non-authoritative future anticipation;
- focused architecture/domain/security/data/workstation/server owners for the exact responsibility being changed.

If this file ever conflicts with a focused canonical owner or the global phase-gate contracts, the focused/global owner governs and this file must be reconciled.

## 2. Current implementation truth

The current post-purge implementation contains independently earned Branding, IdentityAccess and Tenancy capabilities, capability-owned PostgreSQL adapters/migrations, CoreApi, the one-shot DbMigrator, unit/integration tests, `.slnx`, SDK pin and central build/package contract. CoreApi has narrow configured JWT validation plus active-account and active-membership queries; Tenancy can resolve immutable context from current membership. It contains no real ZITADEL login/session topology evidence, account/tenant provisioning, OpenFGA authorization, tenant-owned business data/RLS, business capability, Worker, Web UI, Workstation or general Foundation/ApplicationKernel project.

Phase 0A remains the qualified reset baseline. The former Phase 0B Parties implementation and its verification are retired historical evidence after the 2026-09-17 purge. The narrow current responsibilities listed in `README.IMPLEMENTATION.md` are `PRODUCTION_HONEST`; all broader runtime responsibilities remain `NOT_INTRODUCED`; `BLOCKED = none`.

Folder names, architecture diagrams, old branches, old merge requests, historical source studies, and the retired pre-reset implementation plan do not prove that another runtime/project currently exists.

## 3. Governing implementation sequence

For every active slice:

```text
real responsibility / workload
        ↓
focused owner + current decisions/open decisions
        ↓
inspect applicable proven implementations
        ↓
record source-admission decision
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

A phase number is planning/maturity vocabulary. It is not authority to create a project, provider, process, interface, protocol, test matrix, or runtime behavior. Production-honest also does not mean every mechanism is written from scratch: use the source review to choose a focused package, bounded permissive adaptation, reusable tests/algorithms, or a documented reference-only outcome.

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
  0A     QUALIFIED — repository/architecture reset baseline
  0B     RETIRED HISTORY — former capability-first foundation discovery
  0C     NOT ACTIVE — host/process composition only when a real executable earns it
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

The deleted Party-kind slice did not close the discovery-sensitive Customer/Party/Account relationship group. A future useful capability journey must resolve only the semantics it actually needs.

## 8. Verification rule

Evidence is derived from the claim, not from the phase label or implementation shortcut.

Use the strongest applicable layer for the property actually introduced: static/architecture, deterministic unit/property, real integration, process-failure, migration/restore, security-hostile, load/capacity, or operator evidence as appropriate.

Mocks do not prove behavior owned by a real provider/database/framework/process boundary.

The current repository verification contract is `dotnet restore SquiFlow.slnx`, `dotnet build SquiFlow.slnx --no-restore`, and `dotnet test SquiFlow.slnx --no-build --no-restore`. Report only commands actually run. Former 0B tests and CI runs remain historical evidence only; no current CI execution claim exists.

The next executable slice must define repository-owned verification commands and introduce the smallest test/CI surface that proves its actual claims.

A qualified material claim keeps a permanent or recurring regression guard and explicit requalification triggers. `Manual once` is not a regression strategy.

## 9. KISS, YAGNI, and SOLID

KISS means **smallest production-honest scope**: minimum accidental complexity while completely satisfying the current declared responsibility.

YAGNI prevents speculative breadth, not required correctness/security/durability/recovery/compatibility/resource/observability depth for a responsibility already introduced.

SOLID is pressure-testing guidance for real ownership/change/replacement/fault/security boundaries. It does not mandate one interface per class, generic repositories, forwarding layers, or project-count symmetry.

## 10. Historical master plan

The former v0.0.18 fixed Phase 0–10 implementation plan is superseded as current governance. It remains available in Git history for rationale and architectural questions.

Do not copy its old phase checklist, project inventory, host inventory, evidence list, or future subphase decomposition into new implementation merely because it once appeared here. Re-earn useful ideas from the current responsibility and focused owners.
