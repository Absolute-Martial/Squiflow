# OPS-002 — Persist one capability consequence and transactional outbox

Task ID: OPS-002
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: OPS-001
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Commit one capability fact and its durable consequence atomically so acceptance cannot lose work and request replay cannot create a second semantic effect.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [CORE_API_AND_WORKER](../../server/CORE_API_AND_WORKER.md), [PERSISTENCE_SELECTION](../../data/PERSISTENCE_SELECTION.md), [API_CONTRACT_IDEMPOTENCY_AND_RETRY](../../api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: the selected capability/provider, its migrations, registered migrator module, producer endpoint and focused tests. Implement only this workload’s job/outbox record and status. Exclude a universal event bus, repository framework or broker. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Use OPS-001’s authority and effect identity. Choose retained payload/reference versions, uniqueness and receipt retention from that contract; no claim may depend on an in-memory dispatch.

## Acceptance and edge cases

- Business mutation, receipt and consequence commit together or all roll back.
- Same-intent concurrent retries create one semantic work identity; changed-intent key reuse fails.
- A crash after commit before response/wakeup leaves discoverable pending work.
- Tenant scope, handler version and originating context cannot be overridden by transport data.
- Old payloads are decoded explicitly or quarantined safely; status reads are authorized and bounded.

## Security/static review

Review parameterized SQL, transaction-local RLS, uniqueness and forced isolation; persist protected references instead of credentials or large untrusted bodies. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Use real PostgreSQL concurrent replay and rollback tests; inject failure before/after transaction commit and verify restart discovery. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-002: Persist one capability consequence and transactional outbox.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Add the smallest atomic producer/outbox path for the accepted workload only.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
