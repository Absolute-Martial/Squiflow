# OPS-001 — Select the first durable workload and authority contract

Task ID: OPS-001
Phase: 03-runtime-operations
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: GATE-001, COM-001
Release requirement: REQUIRED
Cross-track prerequisites: COM-001

## Outcome

Choose one real capability consequence requiring restart-safe execution and publish its accepted authority, completion and failure contract before creating Worker infrastructure.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [CORE_API_AND_WORKER](../../server/CORE_API_AND_WORKER.md), [WORKER_RUNTIME_AND_SCHEDULING](../../server/WORKER_RUNTIME_AND_SCHEDULING.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: the selected capability’s focused owner, accepted/open decisions and workload contract tests. Inspect actual producer and consumer source. Exclude imaginary generic queues, a business lifecycle expansion and runtime scaffolding. Keep neutral `Application.*` identities and product version `v0.0.1`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Use COM-001's existing committed-Order facts as the first bounded document-consequence candidate, subject to explicit owner acceptance. Name the producer, irreversible effect, latency/resource budget and authorization class. This avoids making permission reconciliation wait for invoice or Web work. If another first producer is selected, update exact task dependencies and validate the graph before dispatch. Invoice rendering later waits COM-022 under OPS-009.

## Acceptance and edge cases

- A named producer and one handler have an end-to-end useful outcome and concrete payload/version bounds.
- Classify committed consequence, deferred actor action or platform command; define when current authorization is checked.
- Define semantic identity, retry owner/budget, retention and cancellation before versus after an external effect.
- Specify crash windows, poison input, unavailable dependencies and ambiguous outcomes without exactly-once claims.
- Record one initial workload’s required measurements and explicit excluded workload classes.

## Security/static review

Trace tenant scope, originating actor, provider privilege and untrusted payload boundaries; admit exact upstream mechanisms and license obligations before custom infrastructure. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Exercise the existing producer path and contract cases where executable; decision approval is evidence for this assignment, not proof of durable execution. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-001: Select the first durable workload and authority contract.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Publish one accepted workload contract; do not build a generic queue or Worker yet.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
