# OPS-008 — Protect streamed uploads metadata and quarantine

Task ID: OPS-008
Phase: 03-runtime-operations
Status: BLOCKED
Model: GPT-6.1 Sol
Dependencies: OPS-007, OPS-011
Release requirement: CONDITIONAL

Local implementation and qualification are complete for the declared neutral,
CoreApi and PostgreSQL consumer contracts. The exact 1045-test gate passed with
zero failures/skips and zero Release warnings/errors; CoreApi 458/458 and real
Customers PostgreSQL 51/51 are current final-gate results. Expiry-aware reads,
fenced retirement/recovery, safe stream disposal, metadata survival and runtime
RLS/grant/migration guards are locally qualified where exercised. Controlled
signing/streamed-PUT regressions passed 4/4 after the disposed-hasher correction.

`OPS-008` remains `BLOCKED` only for its required real Hugging Face provider-backed
qualification, not for a missing local COM-004 retention implementation.
Live Hugging Face upload, download, delete, conditional write, redirects/timeouts, finite capacity, asymmetric provider/database failure and remote reconciliation remain unrun and NOT qualified. Checked-in `ObjectStorage.Enabled=false`; no nonempty relevant provider runtime configuration variables were visible. No private credential, substitute provider or fallback byte archive was introduced.

Current evidence/guards are owned by
[CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md](../../implementation/CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md).
No repository integration or production acceptance is inferred.

Qualification consumers: COM-015 and WEB-011 consume accepted artwork references after this upload boundary qualifies.

COM-004 is the first selected consumer. This owner now covers only its canonical
`customer-import/v1` raw source; artwork, arbitrary attachments and generic file
browsing remain absent.

## Outcome

Introduce one real attachment/artwork upload journey whose authorized availability depends on verified bytes, durable metadata and the selected content-risk policy.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [FILES_AND_OBJECT_STORAGE](../../data/FILES_AND_OBJECT_STORAGE.md), [APPLICATION_SECURITY_BASELINE](../../security/APPLICATION_SECURITY_BASELINE.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: selected capability metadata/provider, CoreApi upload/download route, storage adapter, bounded scanning/staging and focused tests. Exclude arbitrary file browsers, public bucket access and treating filename or bucket path as ownership. Keep neutral `Application.*` identities and product version `v0.0.1`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Activate when the commercial journey needs uploads. Accept file families, per-file/expanded/tenant limits, retention and scanning/quarantine policy. Choose actual scanning tooling from risk; unavailable scanning must not publish files requiring a successful scan.

## Acceptance and edge cases

- Authorize current tenant/resource access before streaming; known object keys never grant access.
- Enforce request, streaming, expanded-content and concurrent/temp-byte limits.
- Declared/detected content mismatch, traversal and malformed archives fail safely.
- Availability requires verified size/hash and accepted scan state; quarantine content stays inaccessible.
- Upload success with failed metadata produces reconcilable orphans; missing/corrupt referenced bytes produce repair state.
- Retries preserve immutable identity; physical cleanup respects retention and outstanding references.

The implemented import path stages only to a bounded disposable server file, validates
the complete CSV, reserves retained bytes in PostgreSQL, streams through `IObjectStore`,
then publishes metadata/reference state. Default raw bytes expire after seven days;
`X-Tenant-Import-Retention: archive` is the explicit archival election. Source reads
require current tenant/import authorization and available metadata. Staged/quarantined,
orphaned and unavailable state never becomes downloadable. Manifests, row hashes,
decisions, plans and results are independent of raw-byte retirement.

## Security/static review

Review parser isolation, safe keys/download headers, SSRF and executable placement; scanners receive minimum data/privilege and no customer content in ordinary logs. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

The unit/CoreApi/PostgreSQL guards cover bounded invalid-election/CSV admission, tenant
metadata/RLS, durable reservation replay and expiry retirement. Real Hugging Face
streaming, provider checksum/redirect, asymmetric object/DB failure and live
capacity/retention runs were unavailable; OPS-008 remains `BLOCKED` until those real
provider checks are inspected; the current local normal repository gate has already passed. Enabling/changing provider configuration requires its own receiving qualification.

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
