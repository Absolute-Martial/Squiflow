# Phase 5D — Workstation Snapshots and Cross-Version Execution

## Purpose

Local-capable workflows/rules/forms need compatible published snapshots, not live server lookups for every action.

This subphase applies only to configurable behavior that actually exists. It does not require every capability to publish local rule/form snapshots.

## Snapshot requirements

As applicable carry:

- stable definition ID/version;
- compatibility/minimum client evidence;
- immutable content/hash;
- publication/retirement metadata;
- fact-authority classification;
- permission/feature dependencies;
- safe display metadata.

## Workstation

The client may evaluate only behavior explicitly allowed for local execution. `ServerRequired` facts/transitions remain server-authoritative even if the rule definition itself is cached locally.

## Version skew

A Workstation must reject/upgrade/resnapshot when it cannot safely interpret a published definition. Do not silently ignore unknown required fields/transitions.

## Tests

Old Workstation + new published definition, active old instance after new publication, revoked permission, stale fact, definition removed/retired, interrupted snapshot refresh.

## Exit gate

Local configurable behavior is versioned and compatibility-aware rather than depending on the latest server definition implicitly.