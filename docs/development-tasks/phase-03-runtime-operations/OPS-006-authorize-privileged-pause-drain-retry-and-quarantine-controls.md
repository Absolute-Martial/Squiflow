# OPS-006 — Authorize privileged pause drain retry and quarantine controls

Task ID: OPS-006
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: OPS-003, ADM-002, ADM-014
Release requirement: REQUIRED
Qualification consumers: UIA-007 presents these controls later; ADM-023 separately owns administrative receipt reads.

## Outcome

Expose narrow operational commands through private AdminApi so an authorized operator can safely pause/drain execution and review or retry eligible failed work.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [CORE_API_AND_WORKER](../../server/CORE_API_AND_WORKER.md), [WORKER_RUNTIME_AND_SCHEDULING](../../server/WORKER_RUNTIME_AND_SCHEDULING.md), [OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE](../../security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: accepted PlatformAdministration/control capability, its persistence, AdminApi routes and Worker reconciliation tests. Select the first workload’s useful controls. Exclude Worker public admin endpoints, shared operator credentials and direct Quartz mutation. Keep neutral `Application.*` identities and product version `v0.0.1`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Name permission/risk/step-up policy and command revision/idempotency. Pausing new admission is distinct from cancelling running effects; retries require known semantic safety, never a generic reset-to-pending command.

## Acceptance and edge cases

- Forged device, revoked principal and denied permission cannot enqueue or change a control.
- Idempotent command receipt and durable actor/device audit commit atomically.
- Pause prevents new claims and drain has a bounded visible outcome while accepted work remains retained.
- Concurrent controls use expected revision and cannot overwrite a newer operational decision.
- OutcomeUnknown requires reconciliation; quarantine release validates payload/version and retry budget.
- Worker executes the authorized parameters only; every administrative outcome remains explainable.

## Security/static review

Trace private identity/device/OpenFGA checks, contextual authorization and allowed command parameters; no tenant route or job payload bypass. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run real AdminApi/PostgreSQL/OpenFGA denial, replay and concurrent-control tests; kill/restart Worker during drain and retry. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-006: Authorize privileged pause drain retry and quarantine controls.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Implement the initial workload’s direct AdminApi-to-capability control path only.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
