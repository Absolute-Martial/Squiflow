# Admin shell, access status and health views

Task ID: UIA-003
Phase: 05-admin-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6 Luna (high)
Dependencies: UIA-002
Release requirement: REQUIRED

## Outcome

Provide an accessible Platform Admin shell with configured identity, current permitted navigation and honest health/access status for qualified resources.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/admin/ADMIN_SURFACES.md`; `docs/product/PRODUCT_IDENTITY_AND_VERSIONING.md`; `docs/implementation/INDEPENDENT_HOST_BUILDS.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write shell/navigation/status components and browser tests under apps/admin-web. Do not introduce infrastructure dashboards, arbitrary cross-tenant customer browsing or unqualified operational controls.

## Decisions/prerequisites

Backend prerequisites: protected Admin access inventory, safe health/readiness/status projections and platform branding configuration. A health-only endpoint is not authority to browse incidents or provider details. Implementation waits GATE-002; decisions may be prepared earlier. Admin host delivery also waits GATE-003.

## Acceptance and edge cases

- Configured branding has no codename fallback or assumed tenant branding.
- Access and individual action hints remain distinct; server checks every action.
- Liveness/readiness/provider unavailable states are presented accurately.
- No tenant business session or CoreApi availability is needed for normal navigation.
- Sensitive resource details disappear after revocation/account/device change.
- Bounded lists, encoded status text and error correlations disclose no secrets.
- Keyboard focus, landmarks and non-colour status indicators support basic accessibility.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review menu data, health projections, unauthorized deep links, safe diagnostics and cache headers. Tenant membership or AdminPlatform.Access alone cannot manufacture every platform permission.

## Dynamic verification and unavailable-environment handling

Run Playwright against real AdminApi with CoreApi stopped and restricted platform permissions. Retain navigation/denial/status regressions and inspect independent host graph behavior. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Read the qualified Admin endpoint/status inventory.
Build the minimal configured shell.
Render permitted navigation as UX hints.
Keep individual server authority checks intact.
Show unavailable health without invented recovery success.
Use safe correlations and encoded text.
Exercise deep links and revoked access.
Prove operation while CoreApi is stopped.
Do not add speculative dashboards or controls.
Return source ZIP and bounded shell evidence.
```
