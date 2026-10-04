# Real operator login, tenant entry and application shell

Task ID: WEB-002
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: WEB-001, GATE-002
Release requirement: REQUIRED

## Outcome

Introduce only the authenticated tenant-operator entry and shell: real ZITADEL login, stable account resolution, current tenant selection, explicit logout and safe branded navigation.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/security/IDENTITY_AND_SESSIONS.md`; `docs/security/TENANT_PERMISSIONS.md`; `docs/web/WEB_RUNTIME_AND_STORAGE.md`; `docs/product/PRODUCT_IDENTITY_AND_VERSIONING.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write only the earned apps/web entry/session/shell implementation, its focused browser/host checks and focused Web security owner. Do not add capability pages, identity provisioning, a second business API or placeholder projects.

## Decisions/prerequisites

Backend prerequisites: real ZITADEL OIDC/session/revocation qualification, active-account and membership endpoints, current workspace authority and published branding. WEB-001 must settle render/session controls; tenant branding/domain routing is consumed only after its own backend qualifies. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Validate OIDC state/nonce, exact issuer/audience, callback and safe return route.
- No-membership, multiple tenants, suspended account and removed membership have explicit outcomes.
- Switching tenants clears prior tenant query/form state and rechecks current authority.
- Secure HttpOnly scoped cookies, chosen SameSite, CSRF and headers protect the actual topology.
- Logout distinguishes local app session, server revocation and optional provider logout.
- Navigation uses effective capabilities for UX; direct routes and commands still authorize server-side.
- Configured brand/config failure, expiry, reconnect and revoked access never show false success.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Trace callback, CSRF, CSP, storage and cache paths; scan generated client assets/logs for tokens. Enforce output encoding, keyboard navigation, focus/error labels and neutral runtime identity.

## Dynamic verification and unavailable-environment handling

Run actual ZITADEL login and logout in Playwright against the selected .NET 10 host, including account/tenant switch, CSRF denial and restart. Keep permanent callback/tenant-isolation/browser regressions. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Implement only the accepted Web topology.
Use standard OIDC integration and current backend contracts.
Resolve stable account then current tenant membership.
Build the minimal accessible configured shell.
Keep navigation permission hints separate from authorization.
Apply session rotation, logout and revocation semantics.
Protect every cookie-backed mutation against forgery.
Exercise real provider/browser and cross-tenant scenarios.
Report missing environment evidence as BLOCKED.
Deliver source-only ZIP and focused owner updates.
```
