# OPS-007 — Qualify the owned object store adapter and migration boundary

Task ID: OPS-007
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: GATE-001, COM-001
Release requirement: REQUIRED
Cross-track prerequisites: COM-001

## Outcome

Provide the narrow owned object-store boundary required by the first retained artifact, with verifiable bytes and an executable bootstrap-to-production migration path.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [FILES_AND_OBJECT_STORAGE](../../data/FILES_AND_OBJECT_STORAGE.md), [DEPLOYMENT_CAPACITY_AND_RECOVERY](../../operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md), [APPLICATION_SECURITY_BASELINE](../../security/APPLICATION_SECURITY_BASELINE.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: infrastructure storage adapter/contracts, first real consumer/provider, migration tooling and contract tests. Exclude provider SDK types in capabilities, unused cloud features and a separate abstractions project without a boundary. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Hugging Face is accepted bootstrap direction, not proof of deployed capacity or suitability. Verify current primary provider terms/APIs/limits at implementation. A paying-customer profile requires an accepted paid provider selection and qualified cutover; do not select it silently.

## Acceptance and edge cases

- Put/read verify expected size/hash and bounded streaming without whole-file buffering.
- Immutable references cannot be silently overwritten by repeat uploads.
- Provider timeouts, malformed responses and missing/corrupt bytes return owned safe outcomes.
- Finite account capacity and transfer concurrency are observable and bounded.
- Migration copies/verifies all retained references, reconciles failures and supports reviewed cutover/recovery.
- Consumer contract tests pass against both selected adapters before production promotion.

## Security/static review

Review credential scope, private access, outbound destinations/redirects, key ownership and SDK/license containment. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run provider sandbox contract tests and a staged migration/rollback drill using non-sensitive fixture bytes; record unavailable paid-provider evidence as a release blocker. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-007: Qualify the owned object store adapter and migration boundary.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Implement only the first consumer’s owned storage contract and provider migration proof.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
