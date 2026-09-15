# SquiFlow Master Implementation Plan

**Baseline:** v0.0.20  
**Status:** Current high-level implementation direction. Phase 0 is qualified for introduced responsibilities; future scope remains earned from real work rather than pre-specified here.

## 1. Canonical governance

This file is an index and high-level implementation direction, not a second detailed roadmap.

Current governing owners are:

- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md` — scope/quality contract;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` — evidence permanence/regression/requalification contract;
- `docs/implementation/PHASES_AND_GATES.md` — high-level maturity direction;
- `docs/implementation/IMPLEMENTATION_TODO.md` — operational follow-up and trigger-dependent carry-forward;
- `docs/implementation/phases/phase-0/0F_STATUS.md` — integrated qualified Phase-0 state;
- `docs/implementation/phases/phase-1/` — detailed trust/security governance for when that runtime responsibility activates;
- `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md` — non-authoritative future anticipation;
- focused architecture/domain/security/data/workstation/server owners for the exact responsibility being changed.

If this file conflicts with a focused canonical owner or global gate contract, the focused/global owner governs and this file must be reconciled.

## 2. Current implementation truth

The v0.0.20 Phase-0A reset snapshot contained no production/test projects. Qualified Phase 0 now contains two capability/test pairs:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
modules/payments/SquiFlow.Payments/SquiFlow.Payments.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
tests/unit/SquiFlow.Payments.Tests/SquiFlow.Payments.Tests.csproj
```

`SquiFlow.Parties` is qualified only for `PartyKind = Person | Organization` and its no-outward-dependency boundary.

`SquiFlow.Payments` is qualified only for the documented Payment status vocabulary and its no-outward-dependency boundary.

There is still no Foundation/ApplicationKernel project, application/service executable, persistence/provider project, or claim that a complete Party/Customer/Payment model exists.

Integrated Phase-0 evidence is `docs/implementation/phases/phase-0/0F_STATUS.md`; `BLOCKED = none`.

## 3. Governing implementation sequence

```text
real responsibility / workload
        ↓
focused owner + current/open decisions
        ↓
production intent
        ↓
exact scope and non-scope
        ↓
NOT_INTRODUCED / PRODUCTION_HONEST / BLOCKED
        ↓
simplest production-honest implementation
        ↓
falsifiable evidence
        ↓
permanent / recurring regression guard
        ↓
qualify only with BLOCKED = none
```

A phase number is planning/maturity vocabulary. It is not authority to create a project, provider, process, interface, protocol, test matrix, or runtime behavior.

## 4. Engineering direction that remains accepted

- C# / modern .NET 10 remains the application/toolchain direction.
- Modular monolith first; ordinary capability interaction stays in-process.
- One capability owns one business meaning.
- Foundation remains narrow/product-wide and is introduced only when current consumers earn shared semantics.
- Physical project/process/provider/interface boundaries are earned by real compiler/provider/platform/security/fault/lifecycle/resource/packaging/compatibility/workload pressure.
- Workstation remains the local-first/offline direction; Web remains online-only unless deliberately changed.
- PostgreSQL remains selected central transactional authority when central persistence is introduced.
- SQLite/WAL remains selected Workstation local/provisional persistence when local persistence is introduced.
- Guard remains external supervision/recovery, not business authority.
- ZITADEL, OpenFGA and OpenBao/Vault-style directions remain accepted where their owning scopes activate.
- Same-process modules do not communicate through HTTP/gRPC merely to imitate microservices.
- No generic repository/unit-of-work/interface-per-class framework is baseline.
- No future host/process/provider is created merely because an old plan or diagram contained it.

## 5. Current phase relationship

```text
Phase 0  QUALIFIED — introduced architectural/capability/engineering-safety foundation
  0A     QUALIFIED — repository/architecture reset baseline
  0B     QUALIFIED — capability-first Foundation discovery
  0C     NOT INTRODUCED — no executable responsibility exists yet
  0D     QUALIFIED — current build/verification/architecture-safety subset
  0E     QUALIFIED — second real capability / Payments slice
  0F     QUALIFIED — integrated Phase-0 gate
Phase 1  detailed trust/security governance; runtime implementation trigger-dependent
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

Phase 0 completed non-linearly because real capability/build work existed while no executable responsibility earned 0C. That is intentional under the earned-responsibility model.

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

`docs/product/PRODUCT_FOUNDATION.md` remains upstream for customer evidence, first customer ecosystem, first end-to-end product promise and outcome validation. Technical slices may proceed before those questions are fully closed, but must not be presented as validated customer scope merely because code exists.

Consequential domain terminology follows `docs/domain/BUSINESS_TERMS.md`; do not freeze disputed words into durable/API/persistence meaning merely because an old implementation used them.

The Party and Payment slices are intentionally narrower than complete business aggregates.

## 8. Verification rule

Evidence is derived from the claim, not the phase label or implementation shortcut.

Current qualified four-project evidence is successful GitHub Actions run `34922277237`, job `104232827231`, executing the repository-owned restore/build/test contract.

Earlier qualified 0B evidence is run `34916005915`, job `104213630182` for the then-current Parties-only solution.

GitLab hosted quota currently prevents its equivalent job from reaching a runner; this is operational follow-up, not a code/test failure or GitLab execution-success claim.

A qualified material claim keeps a permanent or recurring regression guard and explicit requalification triggers. `Manual once` is not a regression strategy.

## 9. KISS, YAGNI, and SOLID

KISS means smallest production-honest scope: minimum accidental complexity while completely satisfying the current declared responsibility.

YAGNI prevents speculative breadth, not required correctness/security/durability/recovery/compatibility/resource/observability depth for a responsibility already introduced.

SOLID is pressure-testing guidance for real ownership/change/replacement/fault/security boundaries. It does not mandate one interface per class, generic repositories, forwarding layers, or project-count symmetry.

## 10. Carry-forward / TODO discipline

`docs/implementation/IMPLEMENTATION_TODO.md` separates:

- active introduced blockers (currently none);
- operational follow-up that does not reopen a qualified gate;
- trigger-only future work that remains `NOT_INTRODUCED` until its trigger occurs.

Phase completion is not reopened merely because future responsibilities exist. An active blocker must never be hidden in TODO and carried forward as future hardening.

## 11. Historical master plan

The former v0.0.18 fixed Phase 0–10 implementation plan is superseded as current governance. It remains available in Git history for rationale and architectural questions.

Do not copy its old phase checklist, project inventory, host inventory, evidence list, or future subphase decomposition into new implementation merely because it once appeared here. Re-earn useful ideas from current responsibility and focused owners.
