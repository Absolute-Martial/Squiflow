# SquiFlow principles-first implementation baseline

**Baseline:** v0.0.20  
**State:** implementation reset; rebuild not yet started.

The previous Phase-0 implementation was intentionally purged on `rewrite/principles-first-reset`. Its history remains recoverable through Git. Architecture, decisions, requirements, reviews, phase packages, and file-structure samples were preserved because they explain what the system is meant to become and why.

## Current implementation truth

There are currently no `*.csproj` production/test projects in the reset baseline. `SquiFlow.sln` is an empty solution container. `Directory.Packages.props` contains no package versions until an actual project earns those dependencies.

The next implementation must start from:

- `docs/architecture/ENGINEERING_PRINCIPLES.md`;
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`;
- `docs/architecture/REPOSITORY_STRUCTURE.md`;
- `docs/decisions/CURRENT_DECISIONS.md`;
- the focused owner for the responsibility being implemented;
- the relevant phase gate as a minimum maturity floor.

## Rebuild rule

Do not restore the old tree by memory or by symmetry. For each responsibility:

```text
requirement/accepted decision
        ↓
explicit owner + authority/state boundary
        ↓
material edge/failure/security/compatibility cases
        ↓
simplest complete design
        ↓
project/folder/interface/process only if earned
        ↓
verification developed with the implementation
```

KISS is complete simplicity, not omission. YAGNI prevents speculative infrastructure, not necessary failure/recovery/security behavior for a responsibility that already exists.

## File-structure guidance

The architecture docs contain sample/current-target trees. Preserve those as placement guidance. A sample path becomes real only when its responsibility is implemented; do not create empty projects merely to make the repository resemble the diagram.

## CI/CD

Repository verification will be designed to run locally and through GitHub/GitLab thin wrappers. Self-hosted/self-managed runners are preferred to avoid consuming hosted build quotas by default.
