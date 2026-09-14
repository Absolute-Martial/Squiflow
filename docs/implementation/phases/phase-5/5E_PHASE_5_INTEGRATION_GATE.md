# Phase 5E — Integrated Phase-5 Production-Honesty Gate

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## Production intent

After Phase 5 passes, the declared tenant-configurable rule/workflow/form behavior can be authored, published, versioned, executed, recovered, and explained without arbitrary tenant code, without stale local facts becoming protected authority, and without making active old instances unknowable after a new publication.

## Scope contract

Before sign-off, classify each introduced rule/workflow/form/publication/snapshot responsibility as `NOT_INTRODUCED`, `PRODUCTION_HONEST`, or `BLOCKED`.

Other capabilities may remain strongly typed and unconfigured. A published dynamic behavior path with ambiguous fact authority, non-versioned active instances, or failed publication that destroys the prior accepted state is `BLOCKED`.

## Gate conditions

Phase 5 passes for its declared scope when:

- one real rule is deterministic/versioned and uses explicit fact authority;
- one workflow has continuation/recovery/version ownership;
- one bounded dynamic form can be drafted/published/versioned safely;
- old active instances remain explainable after new publication;
- Workstation receives only compatible snapshots and cannot convert stale server-required facts into authority;
- permission checks and workflow/domain validity remain separate;
- publication failure leaves the previous accepted version usable;
- arbitrary tenant code is still not part of the baseline.

## Evidence requirement

Exercise applicable invalid/conflicting rule definitions, stale/local versus server-owned facts, publication failure, concurrent workflow transition, permission revocation, version change with active instances, incompatible snapshot/version, and continuation/recovery behavior.

Tests must assert the declared rule/workflow semantics and fact authority, not merely that a generic interpreter returned some value.

## Completion meaning

Passing Phase 5 qualifies the declared configurable-behavior foundation as production-honest. New rules/workflows/forms or capability breadth re-enter the same scope/honesty model when introduced.
