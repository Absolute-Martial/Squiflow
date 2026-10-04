# Conditional external customer portal decision and boundary

Task ID: WEB-013
Phase: 04-tenant-web
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: WEB-001
Release requirement: CONDITIONAL

## Outcome

Decide whether an external client/customer portal belongs in the accepted release, then define its separate identity, resource authority and application boundary before any portal implementation.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/BUSINESS_OPERATION_END_TO_END.md`; `docs/security/IDENTITY_AND_SESSIONS.md`; `docs/web/CUSTOM_DOMAINS.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Decision-only write scope: focused portal/domain/security owners and decision records. No operator login reuse, portal host, project/folder scaffold or customer-facing business API is authorized by this assignment.

## Decisions/prerequisites

Backend prerequisites: owner-selected portal case, customer representative/contact identity, invitations/account linking/recovery and resource-scoped views/actions. Resolve surface placement, domain/callback ownership and document/artwork authority separately from tenant-operator membership. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- A customer organization/program/individual is not an authenticated operator account.
- Portal rights do not derive from ordinary tenant Staff/Owner roles.
- Accepted quotation/artwork/document actions have exact customer/resource scopes.
- Cross-customer access within one tenant is denied, as is cross-tenant substitution.
- Invite/link/recovery does not match accounts silently by email.
- Custom-domain cookies/callbacks never create broad shared authority.
- Exclusion leaves the portal NOT_INTRODUCED without claiming the tenant Web serves it.

## Security/static review

Review representative linkage, enumeration, shared-contact cases, document leakage and phishing/callback risks. Authentication convenience never removes current resource authorization.

## Dynamic verification and unavailable-environment handling

If selected, use a separate scoped follow-up assignment and actual identity/browser/backend evidence; this decision records its required negative cases. Static planning cannot qualify a portal runtime. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Determine whether the release actually requires a portal.
Define customer actor and representative relationships.
List exact permitted customer views and actions.
Resolve account, invite, recovery and resource boundaries.
Keep operator authentication and authority distinct.
Specify origin/session/domain isolation.
Identify required backend contracts and evidence.
Do not scaffold or implement the portal in this task.
Record deferred scope explicitly if not selected.
Return the decision and bounded follow-up assignments.
```
