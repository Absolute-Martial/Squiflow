# Phase 4 — Long-Offline Conflict and Recovery

**Status:** direction only — `NOT_INTRODUCED` as a qualified phase

## Direction

This phase is expected to address behavior that only becomes real after local pending intent and server authority coexist for long enough to encounter version drift, conflicts, stale authority data, resnapshot or rebase needs.

## Known dependencies

Real locally pending operations, real server state, real supported-version overlap and an actual long-offline product requirement must exist before detailed governance can be honest.

## Current preservation constraints

- do not design synchronization so the only recovery is deleting pending local intent;
- preserve capability-owned version/revision semantics where introduced;
- never make stale local security/payment/stock/credit/limit state authoritative merely to support offline operation.

## Activation trigger

A supported operation must survive a longer offline/version-drift condition than the currently qualified synchronization contract covers.

When activated, derive the conflict/resnapshot/rebase evidence from the actual capability semantics rather than restoring a prewritten 4A–4E map.

See `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md`.