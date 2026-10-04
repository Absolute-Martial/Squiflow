# Platform operator and Admin-device lifecycle screens

Task ID: UIA-006
Phase: 05-admin-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: UIA-005, ADM-016, ADM-017
Release requirement: REQUIRED

## Outcome

Present qualified platform operator and registered Admin-device lifecycle operations without conflating platform devices with tenant Workstations.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/admin/ADMIN_SURFACES.md`; `docs/security/ENCRYPTION_POLICY_KEY_LIFECYCLE_AND_PRIVILEGED_ACCESS.md`; `docs/operations/PRIVATE_ADMIN_NETWORK_AND_PODMAN.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write only accepted operator/Admin-device list/detail/lifecycle pages and tests under apps/admin-web. Exclude workstation enrollment, private key export/generation in browser code and a universal SuperAdmin switch.

## Decisions/prerequisites

Backend prerequisites: accepted platform permission/delegation catalog, operator onboarding/revocation, device registration/rotation/suspension/revocation and recovery/lockout contracts. Bootstrap registration evidence alone is not an ongoing lifecycle API. Implementation waits GATE-002; decisions may be prepared earlier. Admin host delivery also waits GATE-003.

## Acceptance and edge cases

- Administrator identity and device credential are separate displayed lifecycles.
- Certificate fingerprints are evidence; private keys remain device-held.
- Revocation prevents future protected use but does not claim remote byte erasure.
- Self-revocation/last-recoverable-operator changes obey backend safeguards.
- Rotation/re-enrollment does not silently duplicate identity or regain authority.
- Tenant roles, tenant devices and private-network membership cannot imply platform grants.
- Unknown provider/reconciliation outcomes remain explicit until verified.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review fingerprint display, delegated ceilings, recovery-factor exposure and impersonation risks. No secret material, OpenFGA admin token or generic force-access operation is exposed.

## Dynamic verification and unavailable-environment handling

Run real TLS/identity/provider/browser cases for revoked/rotated certificates and operator authority changes. Keep permanent independent-device/identity and lockout checks; static UI tests cannot qualify credential lifecycle. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Verify ongoing operator/device backend qualification.
Build minimal list/detail/lifecycle screens.
Keep operator identity and device proof distinct.
Display safe credential metadata only.
Use explicit guarded registration/rotation/revocation commands.
Respect recovery and delegation ceilings.
Test self-revocation and certificate rotation races.
Never export keys or create browser credentials.
Do not merge tenant Workstation management.
Return source ZIP and real lifecycle evidence.
```
