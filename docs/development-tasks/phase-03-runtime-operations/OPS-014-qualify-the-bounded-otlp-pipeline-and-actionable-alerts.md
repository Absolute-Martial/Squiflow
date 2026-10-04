# OPS-014 — Qualify the bounded OTLP pipeline and actionable alerts

Task ID: OPS-014
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6 Luna (high)
Dependencies: GATE-001
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Qualify one deployment’s logs, traces and metrics through the accepted OTLP boundary with privacy, bounded exporter behavior and alerts tied to useful operational questions.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [OBSERVABILITY_IMPLEMENTATION_CONTRACT](../../observability/OBSERVABILITY_IMPLEMENTATION_CONTRACT.md), [SERILOG_OTLP_PIPELINE](../../observability/SERILOG_OTLP_PIPELINE.md), [OBSERVABILITY_VERIFICATION_ACCEPTANCE](../../observability/OBSERVABILITY_VERIFICATION_ACCEPTANCE.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: existing host instrumentation/composition, minimal shared consumer-earned observability code, collector configuration, alert/runbook fixtures and tests. Exclude new Diagnostics executables, per-method instrumentation and a monitoring product selected without workload/budget. Keep neutral `Application.*` identities and product version `v0.0.1`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Choose initial collector/backend, retention and operator routing through the accepted owner. Specify API/DB/provider health measures now; add job-age/scheduler measures only when their tasks introduce them. Keep durable audit outside lossy export.

## Acceptance and edge cases

- End-to-end correlation follows one authorized request and introduced durable consequence without carrying secrets.
- Tenant/account/job identifiers cannot create unbounded metric label cardinality.
- Unavailable exporter uses bounded buffer/drop/backpressure policy and cannot corrupt business success.
- Redaction tests cover tokens, cookies, SQL/provider exception detail and unrestricted customer content.
- Alert fixtures distinguish sustained backlog/dependency failure from a living process and identify an operator response.
- Resource/export loss counters and retention/deletion settings are measurable for the selected profile.

## Security/static review

Inspect enrichers, Activity baggage, sink/export configuration and public runtime names; review collector credentials and network exposure. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run a local OTLP receiver and exporter outage/cardinality/privacy fixtures; trigger representative alert conditions and inspect received records. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-014: Qualify the bounded OTLP pipeline and actionable alerts.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Qualify the existing-consumer OTLP pipeline and one profile’s actionable alerts only.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
