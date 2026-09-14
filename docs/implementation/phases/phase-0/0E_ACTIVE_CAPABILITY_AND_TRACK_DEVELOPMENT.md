# Phase 0E — Active Capability and Track Development

## Purpose

Phase 0 is not a permit list that restricts development to only Foundation/host work. Real capability, Workstation, Web, server/API, testing, observability and deployment work may advance in parallel when current requirements need them.

The constraint is architectural maturity, not component exclusivity.

## Development rule

A component/capability introduced during Phase 0 may be narrow, but its declared current behavior must be production-honest. It may continue gaining breadth in later work.

Examples of work that may legitimately occur during Phase 0 when needed:

- real capability domain/application/contracts;
- Workstation/Web presentation for real current behavior;
- API/host adapters for current operations;
- additional architecture/verification rules;
- observability required by current runtime paths;
- provider adapter work required by current boundaries;
- deployment/reproducibility work for components that actually exist;
- another capability proving that a shared Foundation primitive is genuinely cross-capability.

## Future responsibility rule

Do not interpret `may advance in parallel` as permission to prebuild future-phase runtimes, stores, providers, protocols or governance.

A future responsibility remains `NOT_INTRODUCED` until a real current requirement needs it.

When it is needed:

```text
real need
→ promote responsibility
→ derive active scope from current facts
→ satisfy production-honesty/evidence contracts now
```

Do not restore retired future `2A…10E` files or conform implementation to their historical planning decomposition merely because those files once existed.

## Allowed growth without phase renumbering

Capability work can start before a later roadmap label. For example, an Orders capability may exist during Phase 0/1 with only the real semantics currently needed. Later it can gain local persistence, central authority, sync, protected stock/payment interactions, etc. when those responsibilities are earned.

This means:

```text
Phase N
≠ first moment a business capability may exist

Phase N
= likely maturity direction for a class of responsibility
```

## Sustainability

Earlier components are never frozen by phase completion. They remain expandable, but later breadth must preserve all already-reached production-honest claims and regression guards.

## Verification expectation

Verification follows real claims. Do not create tests solely because a future phase diagram contains a component. When a concrete boundary/invariant appears, add the narrowest falsifiable evidence and permanent/recurring guard required by the global owners.

## Exit gate

0E is satisfied when current development proves that:

- more than one real track/capability can evolve without violating ownership/dependency rules where reuse pressure exists;
- another real capability can be introduced without changing fundamental dependency direction;
- later foundations can be pulled forward deliberately when truly required;
- `NOT_INTRODUCED`, `PRODUCTION_HONEST`, and `BLOCKED` are used explicitly rather than `complete enough` wording;
- `BLOCKED = none` for scope claimed by the gate;
- no component is called qualified merely because a phase checklist was minimally satisfied;
- future phase governance remains direction-only until real work earns detail.