# Admin login, session and registered-device binding

Task ID: UIA-002
Phase: 05-admin-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: UIA-001, GATE-002, GATE-003
Release requirement: REQUIRED

## Outcome

Implement the separate Admin Web entry with real ZITADEL identity, qualified device proof and current platform access, including expiry/logout/revocation behavior.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/admin/ADMIN_SURFACES.md`; `docs/security/IDENTITY_AND_SESSIONS.md`; `docs/implementation/ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md`; `docs/security/APPLICATION_SECURITY_BASELINE.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write only earned apps/admin-web authentication/session entry and tests plus focused owner updates. Do not modify backend authority, bootstrap, device registration or forwarding middleware to make UI login pass.

## Decisions/prerequisites

Backend prerequisites: UIA-001 accepted topology, real Admin ZITADEL application, exact issuer/subject platform principal mapping, active registered certificate lookup, pinned model access and qualified edge/device transport. Tenant credentials never substitute. Implementation waits GATE-002; decisions may be prepared earlier. Admin host delivery also waits GATE-003.

## Acceptance and edge cases

- Real login validates state/nonce, issuer/audience and exact callback/return origin.
- OIDC identity, device fingerprint, private ingress and platform authority pass independently.
- Missing, wrong, suspended or revoked Admin certificates fail closed.
- Tenant Owner and successful tenant login confer no platform access.
- Cookies/CSRF/CSP and server revocation follow Admin-specific accepted scopes.
- Logout/expiry/reconnect clear sensitive UI state and revalidate every protected operation.
- Normal login cannot rerun first-admin bootstrap or enroll itself silently.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review session fixation, credential forwarding, private key handling, headers and client assets/logs. Keep secret/certificate diagnostics safe; labelled denial/recovery states remain usable by keyboard.

## Dynamic verification and unavailable-environment handling

Use Playwright with actual ZITADEL, AdminApi and TLS/device setup; prove forged forwarding headers cannot authenticate. Keep permanent human/device/revocation and CSRF/session regression checks. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Implement only the accepted Admin entry topology.
Use standard OIDC and qualified AdminApi access.
Preserve exact human and registered-device context.
Apply separate cookies, CSRF and security headers.
Never place mTLS keys or provider secrets in UI code.
Handle revoked devices and expired sessions honestly.
Do not add first-admin HTTP routes.
Test tenant identity denial and forged headers.
Report unavailable real TLS/provider checks as BLOCKED.
Deliver source ZIP and session/security evidence.
```
