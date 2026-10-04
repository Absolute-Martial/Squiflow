# ADM-031 — Conditional first resource-permission enforcement

Task ID: ADM-031
Phase: 01-admin-platform
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: ADM-013, ADM-011
Release requirement: CONDITIONAL

## Outcome

Implement the resource scope actually accepted by ADM-013, such as one supported Own/Assigned relation, throughout its selected commands and bounded reads.

## Current basis and canonical inputs

Read ADM-013's decision, [tenant permissions](../../security/TENANT_PERMISSIONS.md), [current truth](../../../README.IMPLEMENTATION.md), [rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Resource enforcement is not implemented by a model experiment or UI visibility.

## Scope and exclusions

Allowed areas: selected capability's ownership/assignment fact contract, pinned relation/check adapter, durable assignment mutation and affected CoreApi read/command tests. Coordinate shared model rollout. Exclude every-resource ACL frameworks, tenant-defined permission code and invented branch/ownership meanings.

## Decisions/prerequisites

ADM-013 may explicitly decide that the release requires only tenant scope; then this task remains unselected. Otherwise name the exact resources, assign/reassign authority, unassigned semantics, list-filter strategy and delegation ceiling. Do not implement ambiguous ownership by guessing the creator.

## Acceptance and edge cases

- Read detail/list/action guidance and mutation consistently enforce accepted resource scope.
- A tenant-wide grant bypasses resource scope only where the accepted model says so.
- Cross-tenant resource/assignment input never reveals or authorizes another tenant.
- Reassignment/revocation races deny stale admissions and preserve completed history.
- Pagination cannot leak inaccessible records or claim an unsupported global count.
- Assignment retry advances effect/revision once and rechecks current authority.

## Security/static review

Review enumeration, userset expansion, assignment authority, stale snapshots and the difference between business assignment and permission relationships.

## Dynamic verification and unavailable-environment handling

Run `./eng/verify.sh` with actual PostgreSQL/OpenFGA/host fixtures covering detail, list, mutation, changed assignment and forbidden resources. Mock checks do not qualify the actual relation/filter property; list unrun cases exactly.

## Handoff

Return bounded source ZIP/hash, model rollout/compatibility plan, exact permitted surfaces, meaningful regression evidence and blockers. Selected resource scopes join backend and both relevant browser gates.

## Assignable prompt

```text
Implement ADM-031 only for ADM-013's accepted resource scope.
Inspect the selected capability and current relation model.
Name exact detail, list, action and command surfaces.
Implement current scope checks and deliberate assignment authority.
Preserve tenant isolation, pagination and historical outcomes.
Test actual relations, reassignment races and forbidden access.
Review usersets, stale scope and enumeration behavior.
Return source ZIP, hashes and model rollout evidence.
Do not commit or create a universal ACL/branch framework.
```
