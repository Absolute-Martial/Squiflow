# Phase 4 — Conflict, Long-Offline Recovery, and Rebase

## Purpose

Phase 4 turns short-lived synchronization into a durable long-offline operating model. It assumes Phase 3 has real local pending intent, server authority, versioned sync and central persistence.

It does not mark the first time conflict/recovery concepts may exist. Earlier modules may already have expected versions and simple conflict outcomes; Phase 4 qualifies the broader long-offline/resnapshot/rebase model that later capabilities can reuse.

## Subphases

```text
4A  Long-offline detection and compatibility assessment
4B  Capability-specific conflict and reconciliation
4C  Resnapshot, rebase and pending-intent preservation
4D  Authority/configuration/limit refresh and degraded UX
4E  Integrated Phase-4 gate
```

## Continuing development

All business modules, Web, Workstation, Sync/API, data, observability and deployment tracks continue. New capabilities may adopt conflict/recovery behavior as their state becomes locally editable, and they may define capability-specific conflict semantics without waiting for a separate future phase.