# OPS-004 — Bound fair local execution with the selected actor runtime

Task ID: OPS-004
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: OPS-003
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Qualify bounded execution and fair admission for the first workload using the accepted local Proto.Actor direction where serialization or supervision provides concrete value.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [WORKER_RUNTIME_AND_SCHEDULING](../../server/WORKER_RUNTIME_AND_SCHEDULING.md), [RESOURCE_CONSUMPTION_AND_LIMITS](../../requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md), [APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: Worker runtime/composition, admission policy, first handler instrumentation and regression tests. Exclude Proto.Cluster/Remote/Persistence, generic Channel queue layering, permanent per-entity actor forests and hard CPU-isolation claims. Keep neutral `Application.*` identities and product version `v0.0.1`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Inspect primary upstream documentation/source and pin released packages at implementation. Select capacities from measurements; distinguish actor supervision from durable semantic retry and isolate CPU/blocking work only when earned.

## Acceptance and edge cases

- Mailbox/dispatch rejection leaves the durable item recoverable and never falsely completed.
- Claimed/executing/mailbox capacity has enforced ceilings under a producer burst.
- A noisy tenant cannot permanently starve another admitted tenant in the declared test profile.
- Actor restart does not multiply provider or durable retry budgets.
- Cancellation, dead letters and shutdown release admission resources without losing accepted work.
- Report queue age, memory, concurrency and measured fairness limits; no OS-level isolation promise.

## Security/static review

Review dependency admission/license, bounded message shape, partition cardinality and tenant authority loaded from durable state rather than actor messages. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Use real durable workload bursts with multiple tenants; force mailbox rejection/restarts and compare backlog age/resource ceilings. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-004: Bound fair local execution with the selected actor runtime.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Qualify a small local actor execution topology; do not invent distributed runtime needs.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
