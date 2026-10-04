# OPS-016 — Qualify separate database roles and migration registration

Task ID: OPS-016
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: GATE-001, OPS-003, COM-021, ADM-025, ADM-032
Conditional dependencies: OPS-005 when persistent scheduling is selected
Release requirement: REQUIRED
Cross-track prerequisites: OPS-003, COM-021, ADM-025, ADM-032, OPS-005

## Outcome

Qualify effective PostgreSQL privileges for provisioner, migrator and each introduced runtime identity, with module-owned migrations and no host-startup DDL.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [PERSISTENCE_SELECTION](../../data/PERSISTENCE_SELECTION.md), [DEPLOYMENT_CAPACITY_AND_RECOVERY](../../operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md), [MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES](../../architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: deployment role/grant scripts, existing DatabaseMigrator registration/lock behavior, actual capability migrations and real privilege tests. Exclude generic database abstractions and Worker/scheduler grants before those workloads exist. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Inspect existing CoreApi role provisioning before extending it. Accept provisioner ownership, migrator creation rights and runtime grant matrices for the initial profile; add separate Worker/Quartz rights when OPS-003/005 earn them.

## Acceptance and edge cases

- Provisioner/migrator/runtime identities are distinct; no runtime role owns protected objects or inherits elevation.
- CoreApi/AdminApi and actual Worker can perform only their declared operations with tenant isolation intact.
- Runtime DDL, role creation and cross-capability/tenant access fail under effective privileges.
- New module migrations are registered exactly once with bounded advisory-lock behavior and reviewed ordering.
- Concurrent migrators and pool-limited execution preserve lock safety and recover after failure.
- Startup never migrates schemas; incompatible schema fails safely with an explicit release preflight path.

## Security/static review

Inspect effective memberships, ownership, FORCE RLS, security-definer/search-path risks and script rollback on excess privileges; no passwords in scripts. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run real PostgreSQL grant/denial tests with each actual login, competing migrators and a one-connection module pool; inspect effective privileges after schema changes. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-016: Qualify separate database roles and migration registration.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Qualify the initial profile’s separate role matrix and module migration path only.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
