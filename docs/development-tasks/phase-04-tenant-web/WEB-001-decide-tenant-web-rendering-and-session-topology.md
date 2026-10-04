# Decide tenant Web rendering and session topology

Task ID: WEB-001
Phase: 04-tenant-web
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: none
Release requirement: REQUIRED

## Outcome

Close the first tenant-operator Web topology before introducing a host. Select a bounded Blazor Web App rendering/session design with explicit connectivity, security and deployment guarantees.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/web/WEB_RUNTIME_AND_STORAGE.md`; `docs/security/IDENTITY_AND_SESSIONS.md`; `docs/security/APPLICATION_SECURITY_BASELINE.md`; `docs/web/CUSTOM_DOMAINS.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Decision-only write scope: the focused Web/security owners and their accepted/open decision entries. An isolated proof may exercise framework behavior after its purpose is approved; it must not scaffold apps/web or change production code.

## Decisions/prerequisites

Backend prerequisites: qualified admin, CoreApi and commercial-backend phase gates, real ZITADEL layout/session ownership, deployment ingress, supported browser/deployment profile and scope selection. Decide render modes, same-origin/API composition, cookie scope, session persistence/revocation and multi-node promises; custom-domain login requires a verified backend lifecycle. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Blazor/C# owns presentation; no JavaScript business rules or promise of zero framework JavaScript.
- Cookie, callback, antiforgery, CSP and static-cache responsibilities have named owners.
- Interactive Server circuits, if selected, have measured reconnect, draining and memory bounds.
- No Redis, BFF or browser offline replica is admitted without a demonstrated requirement.
- Custom domains and Platform Admin have distinct origin/session boundaries.
- Customer portal remains separate and undecided; no operator identity is repurposed.

## Security/static review

Review redirect/callback allow-lists, Host/proxy trust, credential exposure, session fixation and cross-origin scope. Reuse maintained framework mechanisms before proposing infrastructure.

## Dynamic verification and unavailable-environment handling

Use an isolated .NET 10/browser proof for the selected state/session claims; observe network loss and process restart. Static reasoning cannot qualify real identity, proxy or multi-node behavior. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Read the current focused owners and backend gate evidence.
Document the supported operator journey and deployment assumptions.
Compare only viable Blazor render/session choices.
Choose the smallest design that satisfies the actual guarantees.
Specify cookie, callback, CSRF and response-header behavior.
Keep business state and valuable drafts server-authoritative.
Prove circuit loss and revocation implications where relevant.
Leave customer portal and Workstation/sync/Guard outside scope.
Record unresolved choices rather than choosing silently.
Return the decision, evidence and exact follow-up boundaries.
```
