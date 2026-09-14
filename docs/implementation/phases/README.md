# SquiFlow Detailed Implementation Phase Packages

**Status:** cumulative implementation maturity structure  
**High-level roadmap:** `docs/implementation/PHASES_AND_GATES.md`  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Development rules:** `docs/architecture/ENGINEERING_PRINCIPLES.md` and `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`

## Purpose

Phases are minimum maturity/verification envelopes, not permit lists, sprint walls, or implementation-quality ceilings.

```text
phase
= minimum foundation/maturity that must be true
+ existing responsibilities continuing to evolve
+ applicable failure/security/recovery/compatibility obligations
+ verification/integration evidence appropriate to responsibilities that actually exist
```

A phase is not:

```text
only these folders may change
every named future component must exist
minimal happy-path code is acceptable until a later phase
create implementation/tests solely so the phase can say it ran something
```

## Breadth versus depth

Phase scope has two independent axes:

```text
BREADTH
= how much product/platform scope is introduced
= may be intentionally narrow

DEPTH / PRODUCTION HONESTY
= whether the introduced scope is trustworthy for the guarantee it claims
= non-negotiable
```

The preferred development target is the **smallest production-honest scope**.

Deferring an unneeded boundary is healthy. Deferring correctness/security/durability/recovery/compatibility/resource/observability obligations after introducing the responsibility that needs them is not.

KISS/YAGNI reduce speculative breadth and accidental complexity; they do not reduce the quality floor of claimed behavior.

## Mandatory responsibility states

Every material responsibility at gate sign-off is:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

`BLOCKED` means introduced but not yet trustworthy for its claimed scope. It is a gate failure and cannot be carried forward as later hardening.

Before the gate can pass, a blocked responsibility must either become `PRODUCTION_HONEST` or be explicitly un-introduced/isolated so it truthfully becomes `NOT_INTRODUCED`.

Detailed rules, production-honesty categories, experiment isolation, evidence requirements, and the gate template are owned by `PHASE_GATE_PRODUCTION_HONESTY.md`.

## Mandatory intent and scope contract

Every integration gate must record, when it is actually qualified:

```text
PRODUCTION INTENT
WITHIN SCOPE — PRODUCTION_HONEST
NOT YET IN SCOPE — NOT_INTRODUCED
BLOCKED — must be empty
EVIDENCE
KNOWN LIMITS / NON-CLAIMS
CARRY-FORWARD ITEMS
```

The intent answers: **what real user/operator/developer scenario becomes safe and honest to depend on after this gate passes?**

A green test suite does not define the claim by itself. Evidence must trace to accepted requirements, owner contracts, failure/recovery promises, workload targets, or security/authority rules.

## Repository/file-structure rule

Architecture docs contain target/sample file structures. Preserve them as ownership/placement guidance. A sample path does not become a project/folder until real implementation earns it.

## Current reset / Phase-0 status

Baseline v0.0.20 currently contains no production/test projects. Earlier implementation remains in Git history.

**0A is Complete / Qualified as a documentation/repository-reconciliation gate. 0B is the next implementation gate.**

0A intentionally required no executable test project. Executable verification begins with the first real implementation boundary and grows with it.

Detailed Phase-0 status is owned by `phase-0/0A_BASELINE_STATUS.md`.

## Detailed package index

```text
Phase 0   Architectural development foundation
Phase 1   Identity, tenant authorization, sessions
Phase 2   Local-first Workstation durability and Guard recovery
Phase 3   Authoritative persistence and synchronization
Phase 4   Conflict, long-offline recovery, rebase
Phase 5   Versioned rules, workflow, dynamic forms
Phase 6   Independent Platform Admin and durable Worker
Phase 7   Files, documents, printing, backup/restore
Phase 8   Cross-system security/performance/network/observability qualification
Phase 9   Payments, credit, inventory, protected authority
Phase 10  Paying-customer production qualification
```

Each phase directory contains its detailed subphases and integration gate. The package structure is expandable when a responsibility becomes too broad.

## Pull-forward rule

If a real requirement needs a later foundation earlier:

```text
real requirement
→ identify owning architecture responsibility
→ pull the required subphase/gate forward explicitly
→ declare the current scope
→ satisfy the production-honesty bar now
→ update roadmap/decision evidence
→ use it
```

Do not create a temporary unsafe workaround just because the roadmap originally placed the foundation later.

## Carry-forward rule

Only responsibilities genuinely in `NOT_INTRODUCED` state may be carried forward as future scope.

Every material deferral records owner, preservation constraint, trigger, latest closing gate, and the current check preventing accidental introduction/violation. This is stronger than an unowned `TODO`.

`BLOCKED` cannot be carried forward.
