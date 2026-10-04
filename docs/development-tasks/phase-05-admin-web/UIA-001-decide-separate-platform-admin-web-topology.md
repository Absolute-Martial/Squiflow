# Decide separate Platform Admin Web topology

Task ID: UIA-001
Phase: 05-admin-web
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: none
Release requirement: REQUIRED

## Outcome

Close the Platform Admin Blazor/session/ingress topology as an independent application before introducing its UI host.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/admin/ADMIN_SURFACES.md`; `docs/security/IDENTITY_AND_SESSIONS.md`; `docs/operations/PRIVATE_ADMIN_NETWORK_AND_PODMAN.md`; `docs/security/APPLICATION_SECURITY_BASELINE.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Allowed areas: decision-only focused Admin/security/network owners and decision entries. No apps/admin-web scaffold, project, shared tenant session, edge implementation or production certificate-forwarding change.

## Decisions/prerequisites

Backend prerequisites: qualified AdminApi phase including identity, registered Admin-device proof, request budgets and independent deployment. Decide origin/render mode, cookie/session scope and browser→AdminApi path, preserving device proof through any approved TLS termination. Implementation waits GATE-002; decisions may be prepared earlier. Admin host delivery also waits GATE-003.

## Acceptance and edge cases

- Normal platform operations call AdminApi directly, never proxy through CoreApi.
- Admin Web uses accepted Blazor/C#; framework JavaScript is allowed, business logic is not duplicated in JavaScript.
- Direct mTLS or exact trusted-edge certificate handling has an explicit backend owner.
- Current source uses connection certificates; generic forwarded device/identity headers are not authority.
- Any Interactive Server outbound API path preserves qualified human/device binding.
- Tenant/custom-domain cookies and permission snapshots cannot enter Admin authority.
- Private reachability and recovery are independent of tenant API and shared-edge availability.

## Security/static review

Trace TLS termination, session delegation, trusted proxy identity and spoofable headers. Keys remain device-held; no certificate private key is uploaded, exported or placed in JavaScript.

## Dynamic verification and unavailable-environment handling

Require an isolated .NET 10/browser/TLS proof of selected identity/device transport, process restart and CoreApi outage. Static architecture review does not prove an edge or Admin login deployment. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Read current AdminApi source and focused owners.
Choose the smallest independent Admin Web topology.
Define origin/session and device-proof transport.
Preserve exact identity plus registered certificate gates.
Avoid tenant/CoreApi proxy dependencies.
Document trusted-edge changes as backend prerequisites.
Keep keys out of browser code and server UI custody.
Prove reconnect, TLS and failure independence.
Do not scaffold before the decision is accepted.
Return decision evidence and bounded implementation scope.
```
