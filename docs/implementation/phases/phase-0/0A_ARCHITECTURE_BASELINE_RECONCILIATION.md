# Phase 0A — Architecture Baseline Reconciliation

**Status:** **Complete / Qualified**  
**Gate type:** architecture, decision, repository-truth, and handoff reconciliation  
**Executable product/test projects required by this gate:** none  
**Current evidence ledger:** `0A_BASELINE_STATUS.md`

## Purpose

0A establishes a trustworthy starting point before implementation resumes. It reconciles accepted architecture, development rules, open decisions, Git history, repository structure, and current implementation truth.

0A is intentionally **not** a Foundation/application-kernel implementation phase and it is **not** an architecture-test implementation phase. Creating code merely so 0A can test that code would reverse the dependency:

```text
wrong
0A needs a test
→ invent code/boundary
→ test invented boundary

correct
0A reconciles architecture/repository truth
→ 0B+ introduces a real implementation boundary
→ that same implementation slice introduces the narrowest useful executable verification
```

## Source precedence

```text
focused canonical owner
        ↓
CURRENT_DECISIONS / accepted focused decision record
        ↓
implementation phase package
        ↓
master planning synthesis
        ↓
historical review / source-study / old branch or MR material
```

Git history preserves why earlier code/boundaries existed. It does not make deleted implementation current.

## Qualified 0A baseline

The following are now explicit and mutually consistent:

- baseline version is `v0.0.20`;
- root and scoped `AGENTS.md` files define development rules for the existing ownership areas;
- `ENGINEERING_PRINCIPLES.md` owns the principles-first development rules, including complete KISS;
- `EXPLICIT_BOUNDARIES_AND_SOLID.md` owns dependency/boundary/SOLID interpretation;
- `REPOSITORY_STRUCTURE.md` and `REPOSITORY_FOLDER_STRUCTURE.md` own placement/growth structure;
- `CURRENT_DECISIONS.md` records accepted direction and `OPEN_DECISIONS.md` records intentionally unresolved choices;
- architecture/file-structure samples are growth maps, not scaffolding instructions;
- deleted pre-reset implementation remains history only;
- no product/runtime/Foundation/capability/test project is currently implemented;
- `SquiFlow.sln` is an empty rebuild container;
- CI/CD is an accepted later engineering responsibility but is not used to justify an extra folder or fake executable in 0A.

## What 0A does not create

0A deliberately does **not** create:

- `foundation/application-kernel` implementation;
- another Foundation library merely to establish a layer;
- a capability module;
- Workstation, Guard, Web, API, Worker, Admin, persistence, identity, authorization, or observability runtime code;
- `tests/architecture/` or another executable test project merely to test an empty architecture;
- `eng/`, `.github/`, or another unapproved top-level source/tooling category.

A folder shown in a growth map remains only a reserved ownership location until a real responsibility earns implementation.

## Verification model for 0A

Because 0A intentionally contains no executable implementation, its evidence is **repository reconciliation**, not `dotnet test`/`dotnet run` evidence.

Required 0A evidence is:

1. repository tree and project inventory checked against the canonical structure;
2. version markers and SDK/build baseline checked for internal consistency;
3. current/accepted/open/historical documentation roles identified;
4. stale implementation claims reconciled with the reset state;
5. previous experimental branches/MRs that introduced unapproved structures explicitly superseded;
6. no unresolved contradiction remains that prevents the next implementation slice from knowing its owner, dependency direction, and current architecture constraints.

A successful compile is not an 0A exit requirement because there is no implementation project to compile.

## Handoff to 0B

0B is where executable implementation begins.

The first real slice must follow:

```text
accepted requirement/decision
→ identify business/technical owner
→ identify authority/state/dependency boundary
→ identify material edge/failure/security/compatibility cases
→ implement the simplest complete responsibility
→ introduce shared Foundation/kernel primitive only if current consumers prove it is shared
→ introduce the narrowest useful tests/specs for the boundary that now exists
```

This means executable architecture tests are added **with real boundaries**, not before them. If Foundation exists in that slice, test its dependency rules. If only a capability exists, test that capability first. Do not create Foundation merely so an architecture test has something to inspect.

## 0A exit questions

0A is complete when a developer can answer without guessing:

1. Which document owns the architecture topic being changed?
2. What is accepted, what is open, and what is only historical?
3. What projects/runtimes actually exist now?
4. Which repository locations are current versus growth-only?
5. What dependency/authority direction must new implementation preserve?
6. Which engineering principles apply during implementation?
7. Which decisions are intentionally deferred and must not be silently resolved in code?
8. What must the first real implementation slice prove and test?

Those questions are now answerable from the v0.0.20 baseline, so **0A is Qualified and the next implementation gate is 0B**.
