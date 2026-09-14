# SquiFlow

**Current architecture/development baseline: `v0.0.20`**  
**Implementation state:** principles-first reset; architecture and decision history retained; production projects have not yet been reintroduced.

SquiFlow is rebuilding from the accepted architecture rather than carrying premature implementation forward. The reset is intentional and auditable: earlier code remains in Git history, while the current implementation surface is empty so new code can be introduced under explicit boundaries, SOLID, complete KISS, YAGNI, defensive programming, security, verification, and observability rules from the beginning.

Reset decision: `docs/decisions/PRINCIPLES_FIRST_RESET_2026-09-14.md`.

## Read in this order

1. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` — current repository/reset truth.
2. `docs/architecture/ENGINEERING_PRINCIPLES.md` — mandatory development principles, including complete KISS.
3. `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md` — explicit dependency/ownership boundary rules.
4. `docs/decisions/CURRENT_DECISIONS.md` — accepted product/technology direction summary.
5. `docs/architecture/REPOSITORY_STRUCTURE.md` — current/target repository vocabulary and file-structure samples.
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

There are currently **no application, service, foundation-library, capability-module, or test projects** in the active reset baseline.

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

Directories/projects are reintroduced only when a real responsibility is being implemented and its boundary is explicit.

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

## CI/CD direction

Repository CI/CD is again allowed. GitHub and GitLab should be thin orchestration layers over the same repository-owned verification commands/scripts, with self-hosted/self-managed runners preferred under hosted-minute constraints. No hosted pipeline is triggered merely by this reset.

## Versioning

`v0.0.20` marks the principles-first reset baseline. Older document version labels may remain as provenance when their decisions are still valid; focused owners and current repository status govern conflicts.
