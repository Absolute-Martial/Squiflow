# OPS-019 — Measure representative capacity resource bounds and faults

Task ID: OPS-019
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: OPS-014, OPS-015, OPS-016, COM-032, OPS-009, OPS-012
Release requirement: REQUIRED
Cross-track prerequisites: COM-032, OPS-009, OPS-012
Qualification consumers: WEB-014 and UIA-009 add actual browser/circuit workloads; GATE-005 repeats target capacity only where these new workloads change the measured envelope.

## Outcome

Publish a measured capacity envelope for one deployed commercial workload and prove its failure/resource behavior on the intended hardware class.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [DEPLOYMENT_CAPACITY_AND_RECOVERY](../../operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md), [RESOURCE_CONSUMPTION_AND_LIMITS](../../requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md), [PERSISTENCE_SELECTION](../../data/PERSISTENCE_SELECTION.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: workload fixtures/load harness, disposable deployment/fault scripts, query/admission tuning tied to measurements and focused regressions. Exclude synthetic CPU loops as HTTP capacity evidence, invented SLA numbers and premature sharding/HA platforms. Keep neutral `Application.*` identities and product version `v0.0.1`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Accept tenant/data skew, read/write mix, burst sizes, payloads, authorization/provider topology and response objectives. Include only introduced Worker/document/upload paths; distinguish process-local admission from durable hard limits and OS isolation.

## Acceptance and edge cases

- Report achieved authorized HTTP operations/second, latency percentiles, error mix and saturation point with hardware/configuration.
- Measure DB pool wait/locks/WAL/checkpoints, CPU/RAM/temp/disk and provider latency under realistic skew/bursts.
- Noisy-tenant and background workloads respect declared admission/fairness bounds without unbounded queues.
- Dependency slow/unavailable, restart and disk-full faults preserve isolation, receipts and recoverable work.
- Recovery after burst/fault shows bounded backlog age/drain and no duplicate irreversible effect.
- Identify first bottleneck, tested warning/critical evidence and next scaling/recovery action without unsupported capacity claims.

## Security/static review

Use synthetic non-sensitive fixtures and scoped fault permissions; inspect responses/logs during overload for leakage or authorization bypass. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run actual deployed load/fault cases on representative hardware with real PostgreSQL/IdP/OpenFGA; preserve reproducible harness/config and raw sanitized measurements. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-019: Measure representative capacity resource bounds and faults.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Measure the first real end-to-end profile; separate microbenchmarks from service capacity.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
