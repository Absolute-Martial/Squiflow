# Decision — Production-Honest Phase Gates

**Date:** 2026-09-14  
**Status:** Accepted clarification of the implementation phase/gate model

## Decision

SquiFlow separates implementation **breadth** from implementation **depth**.

A phase may deliberately keep breadth small, but every responsibility introduced on a claimed product/developer/operator path must be **production-honest within an explicit declared scope** before the gate can pass.

The canonical owner is `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`.

A later clarification also applies this rule to governance itself: detailed phase/subphase/evidence structure is written only when real responsibilities have earned that specificity. See `docs/decisions/EARNED_PHASE_GOVERNANCE_2026-09-14.md`.

## Why this clarification is required

Earlier wording such as:

- `complete for current responsibility`;
- `complete enough`;
- `minimum implementation`;

could be interpreted circularly: define a narrow responsibility around the shortcut already built, write tests around that shortcut, and call the gate complete.

That interpretation is rejected.

A second circularity is also rejected:

```text
future phase label
→ speculative detailed governance
→ later implementation forced to fit that speculation
```

## Accepted state model

Each material active responsibility is one of:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

`BLOCKED` means the responsibility is introduced but not yet trustworthy for the guarantee it claims. It is a gate failure and cannot be carried forward as later hardening.

A blocked responsibility must either become production-honest before sign-off or be explicitly un-introduced/isolated from claimed product paths.

## Minimalism interpretation

KISS/YAGNI may reduce breadth. They do not lower the quality floor of a claimed behavior.

The preferred phase formulation is:

> smallest production-honest scope

rather than `minimum implementation` or `complete enough`.

## Gate evidence

Every active phase/integration gate must identify:

- a falsifiable production intent;
- production-honest scope;
- explicit non-scope;
- zero blocked responsibilities;
- evidence for material claims;
- known non-claims/limits;
- material carry-forward items that are genuinely not introduced.

Future direction-only phases do not invent these artifacts before a real responsibility becomes active.

## Experiments

POCs remain allowed. They do not count as introduced responsibilities only when clearly experimental, isolated from claimed runtime paths, carrying no real customer/tenant authority or durable production data, and not used as evidence that the production gate passed.

## Relationship to earlier engineering rules

This does not reverse KISS, YAGNI, incremental phases, earned boundaries, or the principles-first reset. It makes their intended meaning harder to misread:

- scope may be narrow;
- future-only architecture remains deferred;
- current claimed behavior may not be prototype-grade;
- security/durability/recovery/compatibility/observability obligations follow when the responsibility that needs them is introduced;
- future phase governance remains direction-only until real work makes its detailed claims knowable.