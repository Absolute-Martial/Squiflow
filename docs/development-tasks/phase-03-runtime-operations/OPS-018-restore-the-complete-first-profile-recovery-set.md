# OPS-018 — Restore the complete first-profile recovery set

Task ID: OPS-018
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: OPS-015, OPS-016, OPS-007, ADM-017
Release requirement: REQUIRED
Cross-track prerequisites: OPS-007, ADM-017

## Outcome

Prove recovery of the first profile’s authoritative database, retained objects, identity/authorization configuration/state and independently recoverable key material on a replacement environment.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [DEPLOYMENT_CAPACITY_AND_RECOVERY](../../operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md), [FILES_AND_OBJECT_STORAGE](../../data/FILES_AND_OBJECT_STORAGE.md), [ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN](../../security/ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: backup/restore operations, narrow IBackupTarget adapter, encrypted packaging/manifests, selected providers’ supported recovery scripts and drill evidence. Exclude a universal backup-source framework and raw customer data on Kaggle. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Accept provisional RPO/RTO, retention, restore ownership and managed/self-hosted ZITADEL/OpenFGA methods. Kaggle remains bootstrap only; verify current primary limits and require accepted paid-target migration before the first paying customer. PITR applies where selected RPO requires it.

## Acceptance and edge cases

- Remote backup list/download/checksum and authenticated decryption succeed with separately recovered keys.
- DB base backup/WAL recovery reaches the declared point and restores constraints/tenant isolation.
- Retained object bytes reconcile against metadata and immutable hashes; missing bytes prevent false qualification.
- Pinned OpenFGA model/tuples and ZITADEL bindings/config reconnect without authorization bypass.
- Idempotency/outbox/job state survives restore without recreating completed irreversible effects.
- A replacement deployment passes authorized commercial/Admin smoke journeys within measured RPO/RTO.
- Paid backup/object migration and failed restore recovery are drilled for the paying-customer profile.

## Security/static review

Review backup encryption, key independence, private target access, retention/deletion authority and sensitive recovery evidence. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Perform actual remote download and replacement-environment restore, including a WAL/PITR drill when required; inspect measured loss/recovery time and provider reconciliation. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-018: Restore the complete first-profile recovery set.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Prove complete first-profile restoration; upload success alone is insufficient.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
