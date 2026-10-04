# ADM-016 — Rotate and revoke registered Admin certificates

Task ID: ADM-016
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-015, ADM-017, OPS-018
Release requirement: REQUIRED
Cross-track prerequisites: OPS-018

## Outcome

Provide revision-checked device revoke and one accepted certificate rotation path, preserving existing principal binding and bounded current-access semantics.

## Current basis and canonical inputs

Availability and RevokedAt exist in persistence; current request resolution already filters active devices. No supported commands define rotation overlap, revocation concurrency or safe loss recovery. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [PlatformAdminBootstrap.cs](../../../modules/platform-administration/Application.PlatformAdministration/PlatformAdminBootstrap.cs); [PostgresPlatformAdminAccessDirectory.cs](../../../modules/platform-administration/Application.PlatformAdministration.Postgres/Persistence/PostgresPlatformAdminAccessDirectory.cs); [ADMIN_SURFACES.md](../../admin/ADMIN_SURFACES.md).

## Scope and exclusions

Allowed areas: Device-owned rotate/revoke command contracts, PostgreSQL transitions/receipts and real request-boundary tests; no tenant/Workstation device lifecycle. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Select overlap/expiry and self-revocation consequences before implementation; split revoke and rotate handoffs if risk controls differ. Require ADM-017 recovery readiness before destructive last-device actions. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Revoked certificate denies subsequent protected admission with a valid session.
- Rotation does not transfer device ownership.
- Stale revision cannot revoke the newly rotated credential accidentally.
- Concurrent rotation/revoke has deterministic retained outcome.
- Replay preserves one effect and audit receipt.
- Last usable device protection follows accepted recovery rules, without bootstrap reopening.

## Security/static review

Review admission-versus-in-flight revocation semantics and secret exposure limits; revocation does not claim remote wiping or reversal of committed actions. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Use actual TLS credentials and PostgreSQL concurrency barriers to prove immediate fresh-admission denial, rotation replay and lease/cancellation release; run ./eng/verify.sh. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-016: Rotate and revoke registered Admin certificates.
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
