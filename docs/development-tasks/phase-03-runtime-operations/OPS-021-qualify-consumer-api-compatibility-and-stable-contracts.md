# OPS-021 — Qualify consumer API compatibility and stable contracts

Task ID: OPS-021
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-032, ADM-024, ADM-032
Release requirement: REQUIRED
Cross-track prerequisites: COM-032, ADM-024, ADM-032
Qualification consumers: WEB-014 and UIA-009 exercise the actual browser consumers later; this task qualifies backend consumer fixtures before their implementation.

## Outcome

Qualify the first release’s executable API contracts for tenant Web, Admin Web and supported consumers, preserving explicit compatibility and authoritative permission semantics.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [API_CONTRACT_IDEMPOTENCY_AND_RETRY](../../api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md), [CROSS_CUTTING_BUSINESS_PRIMITIVES](../../domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md), [TENANT_PERMISSIONS](../../security/TENANT_PERMISSIONS.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: actual host endpoint metadata/OpenAPI, stable DTO/error contracts, client contract fixtures and narrowly needed compatibility fixes. Exclude new transport frameworks, speculative Workstation protocols and redefining business authority in serializers. Keep neutral `Application.*` identities and product version `v0.0.1`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Choose the first supported consumer/version matrix and retirement policy. Use existing API v1 where sufficient; preserve declared safe failure codes rather than inventing per-host taxonomies. Currency, numbering and pagination follow their capability owners.

## Acceptance and edge cases

- OpenAPI reflects actual classified routes, security requirements, bounded requests and response/error shapes.
- Problem Details codes/status/retry hints are safe, stable and covered for unauthenticated/forbidden/conflict/dependency/unknown outcomes.
- Same-intent replay and changed-intent conflict preserve receipts; version conflicts never silently overwrite.
- Currency/decimal precision and immutable issued numbering survive JSON/locale round trips without floating-point money.
- Permission/action explanations remain observations; commands recheck current authority and never trust UI claims.
- Pagination/cursors retain stable ordering, tenant binding, size limits and malformed/versioned cursor behavior.
- Supported previous/current clients pass contract fixtures; incompatible schema/payloads reject explicitly before durable changes.

## Security/static review

Inspect generated schemas/examples for secrets, provider/domain entity leakage and public codename fallback; preserve no-store and enumeration policy. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run real host contract tests and supported consumer fixtures; diff executable OpenAPI and exercise lost-response retries, stale versions and locale boundary cases. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-021: Qualify consumer API compatibility and stable contracts.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Qualify one release consumer matrix and its stable owned API contract.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
