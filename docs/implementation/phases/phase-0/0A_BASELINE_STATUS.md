# Phase 0A — Principles-First Baseline Status

**Status:** **Complete / Qualified**  
**Date:** 2026-09-14  
**Baseline:** v0.0.20  
**Completion branch:** `phase0/0a-baseline-complete-v20`  
**Next gate:** 0B

This ledger records the evidence used to qualify 0A after the principles-first reset.

## 1. Authority order

```text
1. focused canonical owner
2. CURRENT_DECISIONS / accepted focused decision record
3. implementation phase package
4. master implementation-plan synthesis
5. historical review/source-study/old branch/MR material
```

Repository state proves implementation. Architecture docs may define future ownership without claiming a project exists.

## 2. Current implementation inventory

Current `.csproj` count: **0**.

```text
production/runtime projects: 0
foundation projects:         0
capability projects:         0
test/spec projects:          0
```

`SquiFlow.sln` is the empty rebuild container.

This is intentional. 0A qualifies the architecture/repository baseline; it does not manufacture implementation solely to obtain executable test evidence.

## 3. Current structural truth

The tracked source ownership categories remain the canonical structure documented by `REPOSITORY_STRUCTURE.md` and `REPOSITORY_FOLDER_STRUCTURE.md`, including:

```text
apps/
foundation/
modules/
services/
tests/
deploy/
docs/
```

Some folders exist because scoped `AGENTS.md` guidance reserves ownership. Folder existence does not prove a project/runtime exists.

No `eng/` top-level category is accepted. No `.github/` folder is introduced by 0A.

## 4. Rules and architecture owners confirmed

The baseline has focused owners for:

- engineering principles and complete KISS;
- explicit boundaries and pragmatic SOLID;
- repository/folder structure;
- application-kernel/module direction;
- capability ownership and authoritative execution;
- Web/Sync ingress and workload boundaries;
- schema/contract evolution;
- multi-tenancy;
- Workstation/Guard/local-first behavior;
- server/Worker direction;
- identity, authorization, encryption/key-management direction;
- observability and verification strategy;
- current accepted and intentionally open decisions.

The root/scoped `AGENTS.md` hierarchy tells implementation work which local rules apply.

## 5. Reset/history reconciliation

The previous implementation remains available in Git history but is not current implementation authority.

Relevant review lineage:

- earlier implementation/reconciliation work remains historical evidence;
- the principles-first reset established the v0.0.20 zero-project baseline;
- MR !59 introduced an unapproved `eng/`/CI structure and was closed as superseded;
- MR !60 introduced a premature `tests/architecture` executable merely to qualify 0A and is superseded by this documentation-only 0A completion approach;
- the final 0A review path is the MR sourced from `phase0/0a-baseline-complete-v20`.

Those superseded experiments are not architecture authority.

## 6. Why no executable test belongs in 0A

An architecture test is valuable when a concrete boundary exists to enforce. Before Foundation/capability/host projects exist, creating a test project only to assert an empty project set adds implementation whose only consumer is the gate itself.

That violates YAGNI and the project-creation rule.

The correct sequence is:

```text
0A: reconcile architecture/repository truth
        ↓
0B+: introduce first real implementation boundary
        ↓
introduce tests/specs that prove that real boundary
```

If 0B introduces Foundation/shared primitives, architecture specs can prove Foundation dependency rules in the same slice. If shared Foundation is not yet earned and the first implementation is capability-local, tests begin with that capability instead.

## 7. 0A evidence actually performed

Performed repository/static reconciliation includes:

- audited current v0.0.20 repository state after the principles-first reset;
- verified there are no current product/test `.csproj` projects on the clean baseline;
- verified the solution is an empty rebuild container;
- reviewed root and scoped `AGENTS.md` guidance;
- reconciled repository/file-structure owners with current physical folders and growth maps;
- established canonical source/decision precedence;
- separated accepted decisions from intentionally open decisions;
- preserved architecture, requirements, review evidence, and Git history while rejecting deleted implementation as current truth;
- corrected the attempted `eng/` structure and the attempted premature architecture-test project;
- confirmed 0A does not need runtime/build evidence because it intentionally introduces no executable implementation.

## 8. 0A completion criteria

| Criterion | Result |
| --- | --- |
| Canonical owner precedence is explicit | PASS |
| Accepted vs open vs historical decisions are distinguishable | PASS |
| Current project/runtime inventory is truthful | PASS — zero projects |
| Canonical repository/folder structure is explicit | PASS |
| AGENTS development-rule hierarchy is present | PASS |
| Principles/SOLID/KISS rules are owned | PASS |
| Previous implementation remains recoverable but non-authoritative | PASS |
| No speculative Foundation/test/runtime project is required for 0A | PASS |
| Next implementation gate and verification rule are explicit | PASS — 0B |

**0A is therefore Qualified.**

## 9. Handoff constraint for 0B

0B must not recreate the old ApplicationKernel by default.

It must start from a real declared scope and introduce only the shared primitives/boundaries that current consumers earn. The same change must add the narrowest executable verification capable of falsifying the claims that real code now makes.

From 0B onward, apply `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

No later phase may use “we will harden it later” to justify a `BLOCKED` introduced responsibility, while no earlier phase may manufacture unused abstractions/tests merely to look complete.
