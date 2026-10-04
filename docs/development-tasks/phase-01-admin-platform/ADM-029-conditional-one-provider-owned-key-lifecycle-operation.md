# ADM-029 — Conditional one provider-owned key-lifecycle operation

Task ID: ADM-029
Phase: 01-admin-platform
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: ADM-028, OPS-003, OPS-018
Release requirement: CONDITIONAL

## Outcome

Implement the one accepted ADM-028 operation with durable intent, provider verification and recoverable completion through private AdminApi.

## Current basis and canonical inputs

Read ADM-028's accepted handoff, [policy/lifecycle](../../security/ENCRYPTION_POLICY_KEY_LIFECYCLE_AND_PRIVILEGED_ACCESS.md), [Admin surfaces](../../admin/ADMIN_SURFACES.md), [current truth](../../../README.IMPLEMENTATION.md), [rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md).

## Scope and exclusions

Allowed areas: one provider-neutral lifecycle use case, durable intent/receipt, isolated provider adapter, AdminApi command/status projection and its Worker handler/tests. Reserve shared host/model/migration registration for integration. Exclude a custom vault, generic administrative execution, raw key export, unrelated sensitive-field encryption and all other key operations.

## Decisions/prerequisites

Require selected scope, approved risk checks and actual provider/recovery access. Confirm the provider can correlate one semantic operation after a timeout; otherwise retain OutcomeUnknown and require a bounded reconciling operator path.

## Acceptance and edge cases

- Human identity, active Admin device, operation permission and ADM-034 guard all apply before execution/replay.
- Local preparation precedes external effect; crash windows and duplicate dispatch cannot silently repeat an irreversible action.
- Success is reported only after the accepted provider state is verified.
- New and retained ciphertext use explicit metadata versions; old-data recovery is demonstrated where rotation applies.
- Logs/status expose safe metadata, never keys, provider roots or plaintext.
- Provider outage fails safely without undoing recorded facts or bypassing ordinary tenant operation unnecessarily.

## Security/static review

Review adapter permissions, approval binding, secret redaction, effect correlation, Worker fencing and irreversible-state safeguards against the selected contract.

## Dynamic verification and unavailable-environment handling

Run `./eng/verify.sh` and real isolated provider prepare/timeout/reconcile/restart tests plus selected old-data recovery. Use actual encrypted fixtures, not mocked decryptability. Name exact pending commands when access is unavailable.

## Handoff

Return source-only ZIP/hash, safe provider evidence, changed paths, regression guards, recovery trigger and blockers. UIA-007 exposes only this qualified operation.

## Assignable prompt

```text
Implement only the accepted ADM-029 provider-owned operation.
Inspect ADM-028 decisions and recovery evidence first.
Create bounded durable intent, provider execution and verified status.
Apply current identity, device, permission and freshness guards.
Preserve old-data recovery and explicit unknown outcomes.
Test duplicate dispatch, crash windows and real provider recovery.
Review credentials, safe metadata and audit disclosure.
Return source ZIP, hashes and exact executed/pending evidence.
Do not commit or expand into a vault or other key operations.
```
