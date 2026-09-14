# Phase 4C — Resnapshot, Rebase, and Pending Intent Preservation

## Resnapshot rule

Resnapshot is a recovery mechanism for local authoritative mirrors/projections, not permission to erase user intent.

Before replacing local synchronized state, preserve as applicable:

- pending OperationEnvelopes;
- unsynchronized attachments/staging references;
- conflict/review evidence;
- local installation/device identity;
- local-only drafts whose product contract says they survive;
- migration/recovery journal evidence.

## Flow

```text
quiesce applicable local sync mutation
→ preserve pending intent/evidence
→ obtain authorized compatible snapshot
→ validate/integrity-check
→ replace/rebuild local synchronized state
→ rebase pending operations against new base
→ surface conflicts/rejections
→ resume sync
```

## Atomicity/restart

Each stage must have a crash/restart story. A failed resnapshot cannot leave the application claiming success while both old/new local bases are incomplete.

## Resource behavior

Preflight local disk space, bound snapshot size/concurrency and avoid deleting the last known usable checkpoint before the new state is verified.

## Reuse

The generic resnapshot mechanics can be reused by later capabilities, while the owning capability still decides how its pending semantic operations rebase/conflict.

## Exit gate

A client older than retained incremental history can recover without silently losing pending business intent.