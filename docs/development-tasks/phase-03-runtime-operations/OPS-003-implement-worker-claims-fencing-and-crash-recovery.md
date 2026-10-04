# OPS-003 — Implement Worker claims fencing and crash recovery

Task ID: OPS-003
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: OPS-002
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Execute the first durable workload under a separate Worker identity with competing claims, stale-owner protection and recoverable completion semantics.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [CORE_API_AND_WORKER](../../server/CORE_API_AND_WORKER.md), [WORKER_RUNTIME_AND_SCHEDULING](../../server/WORKER_RUNTIME_AND_SCHEDULING.md), [DEPLOYMENT_CAPACITY_AND_RECOVERY](../../operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: earned Worker host/composition, selected capability handler/provider, claim schema/migrations and process/provider tests. Exclude HTTP administration, CoreApi credentials, distributed actors and business rules in the host. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Define lease duration/renewal, claim generation, effect boundary, bounded reconciliation and shutdown budget. Grants must be separately qualified by OPS-016 before production deployment.

## Acceptance and edge cases

- Two processes racing for one item retain one current claim and fenced completion.
- Expired ownership cannot overwrite a newer claim’s result; ambiguous provider effects enter OutcomeUnknown.
- Crash before dispatch, during work and after effect before completion preserve recoverable durable truth.
- Cancellation propagates; drain stops claims and ends within its declared budget.
- Separate credentials allow the workload only; DB/provider outages fail closed without hot polling.
- Poison/no-progress work reaches bounded retry or quarantine with stable explainable status.

## Security/static review

Check least-privilege Worker grants, trusted durable tenant context, payload/reference validation and no reusable platform credentials in job data. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run real PostgreSQL competing-process/lease tests and kill/restart at each effect window; verify elapsed drain and no duplicate semantic result. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-003: Implement Worker claims fencing and crash recovery.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Build the one earned Worker host and recoverable handler with separate credentials.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
