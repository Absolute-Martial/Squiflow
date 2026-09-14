# Phase 4 — Conflict, Long-Offline Recovery, and Rebase

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

Phase 4 turns short-lived synchronization into a durable long-offline operating model. It assumes Phase 3 has real local pending intent, server authority, versioned sync and central persistence.

It does not mark the first time conflict/recovery concepts may exist. Earlier modules may already have expected versions and simple conflict outcomes; Phase 4 qualifies the broader long-offline/resnapshot/rebase model that later capabilities can reuse.

## Subphases

```text
4A  Long-offline detection and compatibility assessment
4B  Capability-specific conflict and reconciliation
4C  Resnapshot, rebase and pending-intent preservation
4D  Authority/configuration/limit refresh and degraded UX
4E  Integrated Phase-4 production-honesty gate
```

## Continuing development

All business modules, Web, Workstation, Sync/API, data, observability and deployment tracks continue. New capabilities may adopt conflict/recovery behavior as their state becomes locally editable, and they may define capability-specific conflict semantics without waiting for a separate future phase.

## Phase-specific evidence and regression map

- **4A — long-offline compatibility:** supported historical client/schema/protocol fixtures remain in CI and run `PER_MR`. Tests cover expired auth/session, old protocol/schema, compacted history, revoked device/permission, changed rule/config/limits and explicit upgrade/resnapshot outcomes.
- **4B — conflict/reconciliation:** capability-specific deterministic conflict scenarios and concurrent edits run `PER_MR`; generic last-write-wins is prohibited for protected invariants unless a capability explicitly proves that semantics.
- **4C — resnapshot/rebase:** real interruption/low-space/slow-transfer/restart exercises verify that pending local intent, unsynced attachments and review evidence survive. Fast state-machine checks run `PER_MR`; broader transfer/storage failure experiments are recurring and `PRE_RELEASE` for affected Workstation releases.
- **4D — authority/config/limit refresh:** hostile tests permanently verify stale local permission/device/config/limit snapshots never override current server authority before protected admission.
- **4E — integration:** requires evidence that a supported long-offline client follows the documented path and that an unsupported one fails/recoveries explicitly without silent pending-work deletion.

## Transitional contract

If a Workstation returns from offline state outside the currently qualified compatibility/history window, the allowed behavior is explicit recovery (`reauth`, `upgrade`, `resnapshot`, `rebase`, review) rather than best-effort interpretation.

A capability whose conflict semantics are not yet qualified may be read locally or collect clearly provisional intent only to the extent its earlier guarantees allow; it may not silently auto-resolve protected conflicts.

## Hard invariants

Pending local intent preservation, tenant/authorization authority refresh, and no silent unsupported-version reinterpretation are hard invariants. Recovery duration/backlog-clearance time may later use measured SLOs.