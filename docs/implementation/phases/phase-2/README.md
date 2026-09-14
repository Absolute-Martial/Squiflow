# Phase 2 — Local-First Workstation Durability and Recovery

**Status:** direction only — `NOT_INTRODUCED` as a qualified phase  
**Global gate owners:** `PHASE_GATE_PRODUCTION_HONESTY.md` and `PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Direction

This phase is expected to mature the first real Workstation business operation from presentation/process state into trustworthy local durable/provisional state, with recovery semantics appropriate to the implementation that actually exists then.

The current architecture suggests likely ownership areas such as local persistence, encryption, atomic local intent/outbox semantics, process/restart behavior, and Guard/update/migration coordination. These are directions, not a pre-written implementation contract.

## Known dependencies

Before this phase can be specified in detail, there must be a real local-capable business slice and enough Phase-0/Phase-1 foundation to know its identity, authority, security, state and host boundaries.

## Current preservation constraints

Until activated:

- do not call transient presentation/process memory durable;
- do not confuse local/provisional success with central authoritative acceptance;
- do not introduce an ad-hoc local store that bypasses accepted encryption/migration/recovery ownership;
- Guard must remain supervision/recovery rather than business authority.

## Activation trigger

The first real operation must be accepted durably on the Workstation before server confirmation or another real local-state requirement makes the same durability contract necessary.

At that point, create the actual Phase-2 subphase/evidence structure from the real operation and current workload. Do **not** restore the retired 2A–2E files merely because they previously existed.

See `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md` for non-authoritative anticipated questions worth reconsidering when this phase activates.