# SquiFlow Detailed Implementation Phase Packages

**Status:** cumulative implementation maturity structure  
**High-level roadmap:** `docs/implementation/PHASES_AND_GATES.md`  
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

## Complete-current-responsibility rule

Deferring an unneeded boundary is healthy. Deferring correctness after introducing a responsibility is not.

KISS means the simplest design that fully covers the current responsibility and its material edge/failure/recovery/security/concurrency/compatibility/resource/observability cases. YAGNI prevents speculative breadth, not necessary current behavior.

SOLID, DRY, CQS, Law of Demeter, immutability, defensive programming, idempotency, resilience, performance, security, database, API, CI/CD, and observability principles are applied according to `ENGINEERING_PRINCIPLES.md`; none is used mechanically to create ceremony or a technology shopping list.

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
→ implement it correctly and completely for current use
→ update roadmap/decision evidence
→ use it
```

Do not create a temporary unsafe workaround just because the roadmap originally placed the foundation later.

## Carry-forward rule

Every material deferral records owner, preservation constraint, trigger, latest closing gate, and the current check preventing accidental violation. This is stronger than an unowned `TODO`.
