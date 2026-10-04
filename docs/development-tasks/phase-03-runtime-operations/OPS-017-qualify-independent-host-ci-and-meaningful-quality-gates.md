# OPS-017 — Qualify independent host CI and meaningful quality gates

Task ID: OPS-017
Phase: 03-runtime-operations
Status: VERIFY_EXISTING
Model: GPT-6.1 Sol
Dependencies: GATE-001
Release requirement: REQUIRED
Qualification consumers: WEB-014 and UIA-009 extend independent build and browser gates when those hosts exist.

## Outcome

Inspect and extend the existing repository-owned verification contract so each real host and consumer has independent, meaningful regression and security evidence.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [0D_ENGINEERING_SAFETY_OBSERVABILITY_AND_REPRODUCIBILITY](../../implementation/phases/phase-0/0D_ENGINEERING_SAFETY_OBSERVABILITY_AND_REPRODUCIBILITY.md), [APPLICATION_SECURITY_BASELINE](../../security/APPLICATION_SECURITY_BASELINE.md), [PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS](../../implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: eng scripts, GitHub workflows, existing test/tool configuration, architecture checks and focused gaps. Reuse current coverage, secret scanning and host gates. Exclude GitLab migration, unused tool installation and desktop headless testing before a desktop task exists. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Inventory actual tooling first. Preserve xUnit/NSubstitute/Testcontainers; qualify Playwright for earned Web consumers, Coverlet/ReportGenerator reports, format/analyzers, NetArch-style boundaries, Gitleaks, dependency/security review, scoped Stryker and BenchmarkDotNet where measurements matter.

## Acceptance and edge cases

- Normal repository gate preserves parallel integration tests and reports complete counts/failures/skips.
- Every introduced host restores/builds/tests/publishes its own graph; missing artifacts fail CI.
- Coverage reports are reproducible and reviewed for material gaps rather than a vanity percentage.
- Architecture dependency and secret/dependency scanning enforce owned boundaries with actionable triage.
- Scoped mutation tests challenge one critical money/authority invariant and explain surviving mutants.
- Earned browser consumers test actual login, authorization/conflict and failure journeys; static mocks cannot qualify them.

## Security/static review

Review workflow permissions/action pins, untrusted PR handling, dependency/tool licenses and artifact privacy; no production credentials on untrusted jobs. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run the inspected local scripts and corresponding GitHub jobs on the same source; inspect coverage/mutation/scan outputs. Report remote CI unavailable separately from local success. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-017: Qualify independent host CI and meaningful quality gates.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Qualify existing quality gates and close only demonstrated gaps for actual hosts/consumers.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
