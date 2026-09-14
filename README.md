# SquiFlow

**Current architecture/development baseline: `v0.0.20`**  
**Implementation state:** principles-first reset; **Phase 0A is Complete / Qualified**; executable product implementation begins with Phase 0B.

SquiFlow is rebuilding from the accepted architecture rather than carrying premature implementation forward. The reset is intentional and auditable: earlier code remains in Git history, while the current implementation surface is empty so new code can be introduced under explicit boundaries, SOLID, complete KISS, YAGNI, defensive programming, security, verification, and observability rules from the beginning.

Reset decision: `docs/decisions/PRINCIPLES_FIRST_RESET_2026-09-14.md`.

## Read in this order

1. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` — qualified 0A repository/baseline truth and handoff to 0B.
2. `docs/architecture/ENGINEERING_PRINCIPLES.md` — mandatory development principles, including complete KISS.
3. `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md` — explicit dependency/ownership boundary rules.
4. `docs/decisions/CURRENT_DECISIONS.md` — accepted product/technology direction summary.
5. `docs/architecture/REPOSITORY_STRUCTURE.md` and `docs/architecture/REPOSITORY_FOLDER_STRUCTURE.md` — current/growth repository vocabulary and file-structure samples.
6. Focused architecture/security/data/sync/workstation/server owners.
7. `docs/implementation/PHASES_AND_GATES.md` and detailed phase packages — cumulative maturity gates, not implementation ceilings.

## Source precedence

```text
focused canonical owner
        ↓
CURRENT_DECISIONS / accepted focused decision record
        ↓
implementation phase package
        ↓
master/review/source-study/history material
```

Repository state proves what is implemented. Architecture documents may define future ownership and sample structures without implying that those projects currently exist.

## Current repository implementation boundary

There are currently **no application, service, foundation-library, capability-module, or test projects** in the active baseline.

Retained repository-level assets include:

```text
docs/                         architecture, decisions, requirements, reviews, phase packages
deploy/README.md              deployment/reproducibility planning record
global.json                   .NET SDK baseline
Directory.Build.props         shared compiler/analyzer defaults
Directory.Packages.props      central package management enabled; no unused package versions predeclared
SquiFlow.sln                  empty solution container for the rebuild
VERSION / CURRENT_VERSION.txt v0.0.20
```

This zero-project state is intentional and is now the **qualified 0A baseline**. 0A did not create Foundation or test projects merely to prove an empty architecture.

## Architecture direction retained through the reset

- C# / .NET 10 LTS.
- Avalonia Workstation and Blazor tenant Web when those surfaces are implemented.
- ASP.NET Core server hosts when implemented.
- Modular monolith first; ordinary capability communication stays in-process.
- `Foundation → capability-owned business meaning → host/provider adapter → executable composition root` dependency direction.
- One source implementation of business meaning per capability.
- Workstation is the local-first/offline host direction; PostgreSQL is selected central authority; SQLite/WAL is selected Workstation local/provisional persistence when those slices are implemented.
- ZITADEL identity, OpenFGA application authorization, and OpenBao/Vault-style external key-management directions remain accepted but are not implemented by the reset itself.
- Guard remains an external supervision/recovery boundary when rebuilt; it is not business logic, database authority, Worker, scheduler, or key vault.
- Process/project splits are earned by real compiler, provider, lifecycle, fault, security, resource, deployment, compatibility, or packaging boundaries.

## Development rule

A phase is a **minimum maturity and verification gate, not a maximum implementation scope or quality limit**.

KISS means:

> the simplest design that completely covers the current responsibility and its material edge cases, failures, recovery, security, compatibility, concurrency, resource bounds, observability, and operability.

It never means “happy path only” or “fewest files/classes regardless of correctness.”

File-structure samples in the architecture docs are preserved and should guide placement. They are not commands to create empty symmetry projects.

## 0A → 0B handoff

```text
0A COMPLETE
architecture / decisions / repository truth reconciled
        ↓
0B NEXT
introduce first real capability/shared primitive only when earned
        ↓
add executable tests/specs with the real boundary they prove
```

Architecture testing must not be used as a reason to create Foundation prematurely. If 0B introduces Foundation/shared primitives, verify them in that same slice. If the first real responsibility remains capability-local, test the capability first and extract Foundation only when current consumers prove the shared boundary.

## CI/CD direction

Repository CI/CD is allowed. GitHub and GitLab should be thin orchestration layers over repository-owned verification commands, with self-hosted/self-managed runners preferred under hosted-minute constraints. CI is introduced with executable implementation rather than used to manufacture a Phase-0A tooling project.

## Versioning

`v0.0.20` marks the principles-first baseline and qualified Phase-0A handoff. Older document version labels may remain as provenance when their decisions are still valid; focused owners and current repository status govern conflicts.
