# OPS-012 — Expose narrow usage reads and authorized limit controls

Task ID: OPS-012
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: OPS-011, ADM-002, ADM-019
Release requirement: REQUIRED
Qualification consumers: WEB-012 and UIA-007 consume this contract later; enforcement remains backend-owned.

## Outcome

Explain the first meter’s authoritative usage and active limit through bounded tenant reads and narrowly authorized private administrative policy commands.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [RESOURCE_CONSUMPTION_AND_LIMITS](../../requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md), [TENANT_PERMISSIONS](../../security/TENANT_PERMISSIONS.md), [API_CONTRACT_IDEMPOTENCY_AND_RETRY](../../api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: first meter capability/provider, CoreApi tenant read, AdminApi approved controls, DTOs and tests. Exclude plan prices, invoicing, customer subscription management and duplicated business policy in either Web surface. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Use the accepted meter and versioned policy from OPS-011; define permission and supported control range. Clarify observed-at/window timestamps and how pending reservations differ from consumed usage. Tenant self-service changes require separate accepted authority.

## Acceptance and edge cases

- Tenant reads require current membership/resource permission and never reveal another tenant/provider account’s data.
- Usage distinguishes consumption, reservation, available headroom and reconciliation state.
- Admin changes require exact identity/device/permission and expected revision/idempotency.
- Lowering a limit preserves history and returns its declared impact on new optional work.
- Read pagination/filters and explanatory payload sizes are bounded with stable errors.
- A stale displayed allowance cannot override command-time hard-limit enforcement.

## Security/static review

Trace private administrative authority and verified tenant context; redact cost/provider internals unless expressly permitted and retain durable policy audit. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Run real host/PostgreSQL/OpenFGA authorization, concurrent policy change and stale-read-to-command tests for the first meter. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-012: Expose narrow usage reads and authorized limit controls.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Expose only accepted usage explanations and limit controls for the first meter.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
