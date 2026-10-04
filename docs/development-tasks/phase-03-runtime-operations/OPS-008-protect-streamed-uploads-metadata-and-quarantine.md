# OPS-008 — Protect streamed uploads metadata and quarantine

Task ID: OPS-008
Phase: 03-runtime-operations
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: OPS-007, OPS-011
Release requirement: CONDITIONAL
Qualification consumers: COM-015 and WEB-011 consume accepted artwork references after this upload boundary qualifies.

## Outcome

Introduce one real attachment/artwork upload journey whose authorized availability depends on verified bytes, durable metadata and the selected content-risk policy.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [FILES_AND_OBJECT_STORAGE](../../data/FILES_AND_OBJECT_STORAGE.md), [APPLICATION_SECURITY_BASELINE](../../security/APPLICATION_SECURITY_BASELINE.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: selected capability metadata/provider, CoreApi upload/download route, storage adapter, bounded scanning/staging and focused tests. Exclude arbitrary file browsers, public bucket access and treating filename or bucket path as ownership. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Activate when the commercial journey needs uploads. Accept file families, per-file/expanded/tenant limits, retention and scanning/quarantine policy. Choose actual scanning tooling from risk; unavailable scanning must not publish files requiring a successful scan.

## Acceptance and edge cases

- Authorize current tenant/resource access before streaming; known object keys never grant access.
- Enforce request, streaming, expanded-content and concurrent/temp-byte limits.
- Declared/detected content mismatch, traversal and malformed archives fail safely.
- Availability requires verified size/hash and accepted scan state; quarantine content stays inaccessible.
- Upload success with failed metadata produces reconcilable orphans; missing/corrupt referenced bytes produce repair state.
- Retries preserve immutable identity; physical cleanup respects retention and outstanding references.

## Security/static review

Review parser isolation, safe keys/download headers, SSRF and executable placement; scanners receive minimum data/privilege and no customer content in ordinary logs. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run real streaming/provider tests with truncated, oversized, traversal, decompression-bomb and scan-unavailable fixtures; exercise both storage/DB asymmetric failures. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-008: Protect streamed uploads metadata and quarantine.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Implement one earned upload family with explicit metadata and quarantine semantics.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
