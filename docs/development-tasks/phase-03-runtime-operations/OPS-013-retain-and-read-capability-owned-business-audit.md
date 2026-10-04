# OPS-013 — Retain and read capability-owned business audit

Task ID: OPS-013
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: GATE-001, COM-001
Release requirement: REQUIRED
Cross-track prerequisites: COM-001

## Outcome

Protect one material commercial transition with durable append-only audit evidence and bounded authorized history reads separate from operational telemetry.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [OBSERVABILITY_IMPLEMENTATION_CONTRACT](../../observability/OBSERVABILITY_IMPLEMENTATION_CONTRACT.md), [APPLICATION_SECURITY_BASELINE](../../security/APPLICATION_SECURITY_BASELINE.md), [PERSISTENCE_SELECTION](../../data/PERSISTENCE_SELECTION.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: selected commercial capability/provider transaction, audit schema/retention, CoreApi read and regression tests. Reuse valid retained business receipts where sufficient. Exclude a universal event store and administrative audit reads already owned by ADM-023. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Select one transition and its minimum actor, action, fact/revision, outcome, correlation and timestamp evidence. Accept retention, access/privacy and maintenance authority. Append-only means enforced runtime privileges; tamper-evident ledger claims need a separate decision.

## Acceptance and edge cases

- Authoritative mutation and required audit evidence commit together; failed audit cannot report successful unaudited mutation.
- Concurrent/idempotent replay does not create conflicting audit fact identities.
- Runtime application identities cannot update/delete retained records outside accepted maintenance policy.
- Tenant/history reads are scoped, stable-order paginated and do not mutate durable state.
- Suspension/provider outage or telemetry loss cannot erase already retained evidence.
- Retention/legal hold and correction append semantics preserve original facts without claiming unrestricted forensic integrity.

## Security/static review

Review data minimization, effective DB grants, tenant isolation and authorized history exposure; exclude raw credentials/request bodies. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run real PostgreSQL atomicity, privilege denial and scoped history tests; disable OTLP export and show retained audit remains available. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-013: Retain and read capability-owned business audit.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Qualify one capability’s durable business audit; reuse sufficient receipts and avoid ADM-023 duplication.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
