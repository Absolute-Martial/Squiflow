# SquiFlow principles-first implementation baseline

**Baseline:** v0.0.20  
**State:** Phase 0A architecture/repository reconciliation is complete; executable product rebuild begins with 0B.

The previous Phase-0 implementation was intentionally purged. Its history remains recoverable through Git. Architecture, decisions, requirements, reviews, phase packages, and file-structure samples were preserved because they explain what the system is meant to become and why.

## Current implementation truth

There are currently no `*.csproj` production/test projects in the active baseline. `SquiFlow.sln` is an empty solution container. `Directory.Packages.props` contains no package versions until an actual project earns those dependencies.

This is now a **qualified 0A state**, not an unfinished test setup. 0A was a reconciliation gate and intentionally created no Foundation/test project merely to prove an empty architecture.

The next implementation must start from:

- `docs/architecture/ENGINEERING_PRINCIPLES.md`;
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`;
- `docs/architecture/REPOSITORY_STRUCTURE.md`;
- `docs/architecture/REPOSITORY_FOLDER_STRUCTURE.md`;
- `docs/decisions/CURRENT_DECISIONS.md`;
- the focused owner for the responsibility being implemented;
- Phase 0B as the next minimum maturity gate.

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

## 0A versus 0B verification

```text
0A
architecture / decisions / repository truth
→ static reconciliation evidence
→ no executable project required

0B+
real implementation boundary exists
→ compile/test/spec evidence begins
→ architecture tests enforce boundaries that actually exist
```

If 0B introduces shared Foundation primitives, test their contracts/dependency rules in the same slice. If the first real responsibility does not yet earn shared Foundation, do not create Foundation just so a test can reference it.

## File-structure guidance

The architecture docs contain sample/current-target trees. Preserve those as placement guidance. A sample path becomes real only when its responsibility is implemented; do not create empty projects merely to make the repository resemble the diagram.

## CI/CD

Repository CI/CD remains accepted. Local verification and thin GitHub/GitLab orchestration should be introduced with executable implementation, with self-hosted/self-managed runners preferred to avoid hosted build quotas by default.
