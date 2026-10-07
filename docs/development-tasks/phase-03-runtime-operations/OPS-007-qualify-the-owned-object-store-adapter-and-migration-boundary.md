# OPS-007 — Qualify the owned object store adapter and migration boundary

Task ID: OPS-007
Phase: 03-runtime-operations
Status: BLOCKED
Model: GPT-6.1 Sol
Dependencies: GATE-001, COM-001
Release requirement: REQUIRED

Local implementation and qualification are complete for the declared neutral,
CoreApi and PostgreSQL consumer contracts. The exact 1045-test gate passed with
zero failures/skips and zero Release warnings/errors; CoreApi 458/458 and real
Customers PostgreSQL 51/51 are current final-gate results. Expiry-aware reads,
fenced retirement/recovery, safe stream disposal, metadata survival and runtime
RLS/grant/migration guards are locally qualified where exercised. Controlled
signing/streamed-PUT regressions passed 4/4 after the disposed-hasher correction.

`OPS-007` remains `BLOCKED` only for its required real Hugging Face provider-backed
qualification, not for a missing local COM-004 retention implementation.
Live Hugging Face upload, download, delete, conditional write, redirects/timeouts, finite capacity, asymmetric provider/database failure and remote reconciliation remain unrun and NOT qualified. Checked-in `ObjectStorage.Enabled=false`; no nonempty relevant provider runtime configuration variables were visible. No private credential, substitute provider or fallback byte archive was introduced.

Current evidence/guards are owned by
[CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md](../../implementation/CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md).
No repository integration or production acceptance is inferred.

Cross-track prerequisites: COM-001

## Outcome

Provide the narrow owned object-store boundary required by the first retained artifact, with verifiable bytes and an executable bootstrap-to-production migration path.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [FILES_AND_OBJECT_STORAGE](../../data/FILES_AND_OBJECT_STORAGE.md), [DEPLOYMENT_CAPACITY_AND_RECOVERY](../../operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md), [APPLICATION_SECURITY_BASELINE](../../security/APPLICATION_SECURITY_BASELINE.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: infrastructure storage adapter/contracts, first real consumer/provider, migration tooling and contract tests. Exclude provider SDK types in capabilities, unused cloud features and a separate abstractions project without a boundary. Keep neutral `Application.*` identities and product version `v0.0.1`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Hugging Face is accepted bootstrap direction, not proof of deployed capacity or suitability. The current Storage Bucket API is the S3-compatible gateway at `https://s3.hf.co/<namespace>` with path-style addressing, `us-east-1`, HFAK-derived credentials and mutable/non-versioned objects. The adapter uses only deployment-supplied configuration and keeps provider types out of `Application.ObjectStorage`. A paying-customer profile requires an accepted paid provider selection and qualified cutover; do not select it silently.

## Acceptance and edge cases

- Put/read verify expected size/hash and bounded streaming without whole-file buffering.
- Immutable references cannot be silently overwritten by repeat uploads.
- Provider timeouts, malformed responses and missing/corrupt bytes return owned safe outcomes.
- Finite account capacity and transfer concurrency are observable and bounded.
- Migration copies/verifies all retained references, reconciles failures and supports reviewed cutover/recovery.
- Consumer contract tests pass against both selected adapters before production promotion.

The current implementation adds `Application.ObjectStorage.IObjectStore` and the CoreApi
Hugging Face adapter. It enforces SquiFlow key validation, bounded request timeouts,
streamed expected-length/SHA-256 verification, conditional no-overwrite, safe typed
provider outcomes and allow-listed HTTPS redirects. The adapter does not log provider
response bodies, credentials or source content. The bootstrap configuration is disabled
by default and fails closed when enabled without complete bounded configuration.

## Security/static review

Review credential scope, private access, outbound destinations/redirects, key ownership and SDK/license containment. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Local unit/host/PostgreSQL checks cover the neutral contract and database consumer, but
no Hugging Face credentials or private bucket were available. Provider sandbox
upload/download/conditional-put/delete, redirect, timeout and staged migration/rollback
drills remain unrun and keep OPS-007 `BLOCKED`; no mock is treated as provider evidence.
Run those checks with non-sensitive fixture bytes and then the normal `./eng/verify.sh`
before changing this state.

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
