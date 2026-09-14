# Principles-First Implementation Reset — 2026-09-14

**Status:** Accepted reset decision  
**Baseline introduced:** v0.0.20

## Decision

Purge the current Phase-0 implementation/test projects and restart implementation from the accepted architecture, explicit-boundary rules, and canonical engineering principles.

Do **not** purge architecture/decision/requirements/review/phase knowledge. Git history remains available for implementation archaeology, and documented file-structure samples remain design guidance.

## Why

The previous implementation contained useful work, but development had become vulnerable to three recurring mistakes:

1. treating phase descriptions as an implementation ceiling and stopping at demo-shaped code;
2. introducing generic/shared abstractions before stable responsibility boundaries were proven;
3. confusing “simple” with “minimal happy path,” allowing edge/failure/recovery/security behavior to be postponed merely because a later phase mentioned deeper maturity.

The `HostKind`/`SupportedHosts` topology coupling was a concrete example: executable/deployment names leaked into host-neutral application-kernel metadata instead of remaining in composition roots/adapters.

Rather than continue accumulating compatibility with premature code, the implementation was reset while the architectural reasoning was preserved.

## What was purged

The reset removes current projects under:

```text
apps/
services/
foundation/
modules/
tests/
```

The solution remains as an empty rebuild container. Central package management remains enabled but predeclares no unused packages.

## What was intentionally preserved

- `docs/architecture/` including repository/file-structure samples;
- `docs/decisions/` and material decision history;
- product/domain/requirements/security/data/sync/workstation/server/operations owners;
- review/source-study evidence;
- implementation phase packages;
- `global.json` and shared compiler/analyzer defaults;
- Git history of the deleted implementation.

This satisfies Chesterton's Fence: implementation was removed only after its purpose/history was preserved and reviewed.

## Mandatory development rules after reset

Canonical owners:

- `docs/architecture/ENGINEERING_PRINCIPLES.md`;
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`;
- `docs/architecture/REPOSITORY_STRUCTURE.md`.

A phase is a **minimum maturity/verification floor**, not a maximum quality/scope boundary.

KISS means:

> choose the simplest design that completely covers the current responsibility, including material edge cases, failure, recovery, security, compatibility, concurrency, resource bounds, observability, and operability.

KISS does not mean fewest files/classes, happy-path-only code, or collapsing a real authority/security/fault boundary.

YAGNI forbids future-only scaffolding; it does not justify omission of necessary behavior for a responsibility that exists now.

SOLID is applied pragmatically to real ownership/change/replacement boundaries. It does not imply one interface per class, generic repositories, manager/helper layers, deep inheritance, or plugin systems without a current requirement.

## File-structure rule

Sample/target trees in architecture docs remain valid guidance for ownership and placement. They are **growth maps**, not scaffolding instructions.

A project/folder/process is created only when its responsibility exists and its boundary is earned by real compiler/provider/platform/lifecycle/fault/security/resource/deployment/compatibility/packaging pressure.

## Foundation/kernel rule

Do not rebuild the old application kernel simply because it existed.

Start with real capability/application work. Introduce or extract Foundation/kernel primitives only when current consumers prove that the semantics are genuinely product-wide. Preserve inward dependency direction and host/provider neutrality when a shared boundary is earned.

## Verification rule

Verification is developed with the responsibility being introduced. Architecture tests protect real boundaries after those boundaries exist; they must not force speculative projects.

Repository CI/CD is allowed again. Local verification and GitHub/GitLab workflows should share repository-owned commands/scripts; self-hosted/self-managed runners are preferred under hosted-minute constraints.

## Historical relationship

- MR !54 remains the historical documentation-first rewrite merged into `main`.
- MR !55 captured the subsequent v0.0.19 baseline reconciliation and boundary corrections but is superseded by this reset before merge.
- `rewrite/principles-first-reset` starts from the useful architectural corrections in !55, then removes the implementation and establishes v0.0.20.

The deleted code is evidence and history, not a template that must be restored.
