# ADM-017 — Define and drill Admin access recovery ceremony

Task ID: ADM-017
Phase: 01-admin-platform
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: ADM-014, OPS-015
Release requirement: REQUIRED
Cross-track prerequisites: OPS-015
Qualification consumers: OPS-018 incorporates this application recovery drill into the whole infrastructure recovery set.

## Outcome

Specify and prove recovery from lost operators/devices without a standing bypass, tenant authority escalation or uncontrolled replacement of bootstrap identity.

## Current basis and canonical inputs

The one-shot bootstrap is resumable for the same intent and must never reopen after completion. Security owners require controlled break-glass and independent recovery evidence; current normal device/operator recovery workflow is absent. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md](../../security/ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md); [OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md](../../security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md); [ADMIN_SURFACES.md](../../admin/ADMIN_SURFACES.md).

## Scope and exclusions

Allowed areas: Application operator/device recovery policy, minimum controlled recovery primitive and isolated drill tests; infrastructure credential/key recovery remains with OPS. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Approve ownership evidence, quorum/physical factors, recent authentication, expiry and post-use rotation. Never substitute an internal IP or support ticket alone for recovery authority. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- All normal Admin devices unavailable has a tested controlled route.
- AdminApi/CoreApi failure modes are distinguished from provider loss.
- Duplicate recovery request cannot create additional privilege.
- Denied/expired approval never activates replacement device.
- Emergency access expires and is independently audited.
- Recovery preserves immutable original bootstrap and existing tenant authority.

## Security/static review

Review separation of duties, recovery material locations, redacted drill artifacts and actor chain. Raw keys/shares never appear in Admin HTTP output. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run an isolated recovery drill jointly with OPS, including interrupted attempt and post-recovery revocation. Record missing physical/provider access precisely; paper steps alone do not qualify recovery. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-017: Define and drill Admin access recovery ceremony.
Read AGENTS.md and linked focused owners.
Use the declared model and shared rules.
Verify dependency handoffs against current evidence.
Preserve incoming files and restrict edits to the allowed areas.
Report unresolved policy; never invent defaults.
Reuse existing boundaries and framework mechanisms.
Implement or verify the exact outcome and acceptance cases above.
Perform static security review and the named real-boundary checks.
List exact unrun checks; do not claim success.
Return a source-only ZIP; no commit/push.
```
