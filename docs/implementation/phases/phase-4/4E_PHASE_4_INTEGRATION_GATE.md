# Phase 4E — Integrated Phase-4 Production-Honesty Gate

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## Production intent

After Phase 4 passes, a supported long-offline Workstation can return to service through an explicit compatibility/recovery path without silently deleting pending intent, bypassing current authority, or resolving real conflicts through generic last-write-wins.

## Scope contract

Before sign-off, classify each introduced long-offline/conflict/resnapshot/rebase/refresh responsibility as `NOT_INTRODUCED`, `PRODUCTION_HONEST`, or `BLOCKED`.

Capability-specific conflict policies and future retention breadth may remain unintroduced. A recovery path that loses pending intent, hides incompatibility, or trusts stale protected authority is `BLOCKED`.

## Gate conditions

Phase 4 passes for its declared scope when:

- a months-old supported Workstation follows an explicit compatibility path;
- unsupported protocol/schema states fail explicitly;
- at least one real aggregate conflict is resolved without generic last-write-wins;
- resnapshot preserves pending local intent and attachments/evidence;
- rebase has deterministic/reviewable outcomes;
- permission/device/config/limit authority is refreshed before protected admission;
- low disk/slow network/interrupted resnapshot are recoverable;
- the Workstation explains recovery states in business/operator terms.

## Evidence requirement

Exercise the actual old-client/old-schema/protocol path and interruption points being claimed. Include applicable cases such as long offline duration, revoked permission/device, lowered/exhausted limit, deleted/merged remote entity, compacted incremental history, large pending backlog, slow/interrupted resnapshot, low disk, and rebase conflicts.

Evidence must prove preservation of local pending intent rather than merely reconstructing a clean local database.

## Completion meaning

Passing Phase 4 qualifies the declared long-offline/conflict/recovery behavior as production-honest. Retention windows and conflict policies remain capability/profile-specific and may evolve through explicit compatible scope changes.
