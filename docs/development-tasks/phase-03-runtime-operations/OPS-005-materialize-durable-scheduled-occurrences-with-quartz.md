# OPS-005 — Materialize durable scheduled occurrences with Quartz

Task ID: OPS-005
Phase: 03-runtime-operations
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: OPS-003, OPS-004
Release requirement: CONDITIONAL
Cross-track prerequisites: none

## Outcome

Implement one actually required timed workload with durable schedule meaning, duplicate-safe occurrence materialization and explicit recovery behavior.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [WORKER_RUNTIME_AND_SCHEDULING](../../server/WORKER_RUNTIME_AND_SCHEDULING.md), [PERSISTENCE_SELECTION](../../data/PERSISTENCE_SELECTION.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: one capability schedule contract/provider, Quartz adapter/persistent PostgreSQL migrations, Worker composition and schedule tests. Exclude schedules for imagined jobs, Quartz business handlers, direct dashboard/table administration and replacement schedulers. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Select the first timed occurrence in OPS-001's accepted workload contract, such as a document-reconciliation retry only when timed retry is actually needed. Do not make this task depend on a workflow implementation that waits for scheduling; bind another concrete producer before reassignment if selected.

Activate only when a named workload requires timing. Preserve accepted Quartz direction; verify primary released documentation and pin compatible packages. Decide timezone, misfire, overlap, revision publication and bounded catch-up for one trigger family.

## Acceptance and edge cases

- Duplicate firings and restart races materialize one occurrence and semantic job.
- Schedule revision changes/disable reconcile derived triggers and cannot resurrect obsolete work.
- DST gaps/folds and clock changes match the published business-time contract.
- Misfires skip, run once or boundedly catch up as selected without an outage storm.
- NoOverlap/coalescing semantics survive running work and duplicate trigger delivery.
- Lost Quartz state can be restored/rebuilt from owned schedules; firing success is distinct from effect success.

## Security/static review

Review schedule scope, revision authorization, bounded payloads and separate scheduler grants; keep secrets out of Quartz data. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Use real Quartz persistent PostgreSQL restart/misfire tests, controlled clocks and duplicate materialization races for the selected schedule. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-005: Materialize durable scheduled occurrences with Quartz.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Implement exactly one earned schedule and its recovery semantics; no generic scheduler product.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
