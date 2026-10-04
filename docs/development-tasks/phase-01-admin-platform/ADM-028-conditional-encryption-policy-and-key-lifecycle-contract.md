# ADM-028 — Conditional encryption policy and key-lifecycle contract

Task ID: ADM-028
Phase: 01-admin-platform
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: ADM-014, ADM-034, OPS-015
Release requirement: CONDITIONAL

## Outcome

Define one application-owned encryption-policy/key-lifecycle request for an actual server/object/backup resource. This is a contract assignment before ADM-029 implements it.

## Current basis and canonical inputs

Read [policy/lifecycle](../../security/ENCRYPTION_POLICY_KEY_LIFECYCLE_AND_PRIVILEGED_ACCESS.md), [key management](../../security/ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md), [Admin surfaces](../../admin/ADMIN_SURFACES.md), [current truth](../../../README.IMPLEMENTATION.md), [rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Policy direction is not a deployed vault or available key API.

## Scope and exclusions

Allowed areas: focused policy/provider decision, safe metadata contract, recovery examples and executable acceptance specification for one selected operation. No production cryptography, vault bootstrap, endpoints, private-key export, general key dashboard or Workstation encryption implementation.

## Decisions/prerequisites

Activate only for a named resource requiring an application-level lifecycle. Owner approves provider, purpose/scope, old-key decryptability, migration/retirement boundary, risk-specific freshness/approval and recovery prerequisites. Infrastructure-only volume/backup procedures can remain under OPS without an Admin API.

## Acceptance and edge cases

- Policy/metadata publication cannot disclose raw keys or customer plaintext.
- Rotation, revocation, retirement and destruction have distinct meanings and permissions.
- Existing retained data remains decryptable during a selected rotation window.
- Define ambiguous provider outcome, duplicate intent, restart/reconciliation and audit ownership.
- Define recovery evidence required before retiring an old version.
- State which operations stay outside the first slice; do not assume key administration grants support access.

## Security/static review

Review provider roots, least-privilege adapter credentials, human/device authority, downgrade and undecryptable-data scenarios. Delegate actual cryptographic primitives to the admitted provider.

## Dynamic verification and unavailable-environment handling

Validate contract examples against the selected provider in an isolated POC when available. Run `./eng/verify.sh` for executable/doc-boundary changes. Record exact unrun provider/recovery checks; accepted policy alone is not runtime qualification.

## Handoff

Return the proposed bounded contract, owner decisions, dependency map, examples, source-only ZIP and hashes. ADM-029 starts only after acceptance.

## Assignable prompt

```text
Execute ADM-028 as a selected key-lifecycle contract assignment.
Identify one real resource and one useful application operation.
Read current encryption, Admin and recovery owners.
Define authority, safe metadata and provider-owned cryptography.
Specify old-data recovery and ambiguous-write reconciliation.
Separate rotation, revocation, retirement and destruction.
Return choices and executable acceptance cases for owner approval.
Do not add endpoints, cryptography or raw-key export.
Deliver source ZIP, hashes and exact POC evidence or pending checks.
```
