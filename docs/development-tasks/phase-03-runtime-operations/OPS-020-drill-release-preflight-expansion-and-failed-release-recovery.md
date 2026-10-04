# OPS-020 — Drill release preflight expansion and failed-release recovery

Task ID: OPS-020
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: OPS-017, OPS-018, OPS-019, OPS-021
Release requirement: REQUIRED
Qualification consumers: GATE-005 adds both real Web hosts to the accepted backend release drill and reruns materially changed cases.

## Outcome

Promote one immutable verified release artifact through the first profile and prove the declared failed-release recovery path without corrupting authoritative or pending work.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [DEPLOYMENT_CAPACITY_AND_RECOVERY](../../operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md), [PERSISTENCE_SELECTION](../../data/PERSISTENCE_SELECTION.md), [PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS](../../implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: release packaging/provenance, deployment preflight/smoke scripts, actual schema/payload compatibility migrations and operator runbooks. Exclude zero-downtime/HA claims unsupported by topology and generic release orchestration infrastructure. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Accept maintenance/rollback window, operator stop authority, compatible expand/migrate/switch/contract ordering and binaries/schema/job versions supported together. Product stays v0.1.0; build revision/schema IDs remain separate traceability identifiers.

## Acceptance and edge cases

- Same checksummed/provenanced bytes are promoted; secrets/configuration remain outside artifacts.
- Preflight validates configuration, capacity/free space, dependency access, migration readiness and old/new compatibility.
- In-flight requests/jobs drain or stop within the accepted bounds before unsafe transitions.
- Deployment smoke verifies authorized tenant/commercial/Admin behavior beyond process liveness.
- A deliberately failed release recovers through tested rollback, roll-forward or maintenance restore without discarded facts.
- Destructive contraction waits for obsolete readers/writers and retained rollback requirements; Down is never assumed safe.
- Previous artifacts and runbooks remain usable throughout the accepted recovery window.

## Security/static review

Review artifact integrity, signing/checksum verification, operator permissions, migration privileges and private break-glass access evidence. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Execute release and failed-release drills in the first-profile environment with real migrations/old-new artifacts; inspect recovery data and measured downtime. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-020: Drill release preflight expansion and failed-release recovery.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Deliver one tested immutable-artifact release and failed-release recovery procedure.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
