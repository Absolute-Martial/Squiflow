# OPS-009 — Render immutable document facts and retryable artifacts

Task ID: OPS-009
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: OPS-003, OPS-004, OPS-007, OPS-011, COM-022
Release requirement: REQUIRED
Cross-track prerequisites: COM-022
Qualification consumers: WEB-011 presents authorized document download later.

## Outcome

Render one retained business document from immutable committed facts and an immutable template revision; retries recover the artifact without reissuing business truth.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [FILES_AND_OBJECT_STORAGE](../../data/FILES_AND_OBJECT_STORAGE.md), [CROSS_CUTTING_BUSINESS_PRIMITIVES](../../domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md), [APPLICATION_SECURITY_BASELINE](../../security/APPLICATION_SECURITY_BASELINE.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: one commercial document capability/provider, bounded renderer, Worker handler, object metadata and download/status tests. Exclude all document families, arbitrary template code, legal invoice design without accepted jurisdiction and a new workflow engine. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Name the first document and its commercial issuance task. Decide renderer/license, allowed variables/assets, supported fonts/locales, retention and output limits. Numbering/legal authority remains in the issuing capability, never the renderer.

## Acceptance and edge cases

- Render input pins issued facts, currency/numbering and template revision; later edits cannot change history.
- Repeat attempts return the same semantic artifact or verify/reconcile a prior upload.
- Renderer crash/time/temp/memory exhaustion leaves retryable or quarantined work without undoing issuance.
- Template expressions and remote assets cannot execute code or bypass allowed destinations.
- Tenant-scoped authorized download verifies metadata/hash and reports unavailable artifacts safely.
- Visual fixtures prove readable long names, page breaks, totals and locale formatting for the initial template.

## Security/static review

Review parser/render isolation, template escaping, external asset access, font/package licensing and object authorization. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run real renderer/Worker/storage retry and crash-window tests; render and visually inspect exported pages for the one supported document. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-009: Render immutable document facts and retryable artifacts.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Produce one immutable document artifact path; keep issuance and rendering authority distinct.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
