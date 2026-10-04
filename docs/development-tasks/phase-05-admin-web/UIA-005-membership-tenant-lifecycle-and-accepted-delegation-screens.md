# Membership, tenant lifecycle and accepted delegation screens

Task ID: UIA-005
Phase: 05-admin-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: UIA-004, ADM-012, ADM-025
Conditional dependencies: ADM-031 when resource-scoped grants are selected
Release requirement: REQUIRED

## Outcome

Expose qualified membership invitation/activation/suspension/removal, initial-Owner designation and tenant suspension/reactivation; show broader role/delegation operations only after their backend qualifies.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/ADMIN_API_MEMBERSHIP_LIFECYCLE.md`; `docs/security/TENANT_PERMISSIONS.md`; `docs/admin/ADMIN_SURFACES.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write bounded Admin membership/tenant-lifecycle and accepted role/delegation pages plus tests. Do not invent universal roles, grant administration, ordinary Owner transfer or direct OpenFGA tuple editing.

## Decisions/prerequisites

Backend prerequisites: qualified membership/tenant commands, expected revisions, durable actor/device receipts and initial-Owner guard. Role/delegation catalog, ceilings and reconciliation are separate prerequisites for those optional controls. Implementation waits GATE-002; decisions may be prepared earlier. Admin host delivery also waits GATE-003.

## Acceptance and edge cases

- Invite/activate/suspend/remove and tenant suspend/reactivate remain different effects.
- Initial Owner is a protected designation, not a permanent broad role grant.
- Expected revision conflict shows refreshed state without automatic destructive replay.
- Tenant suspension explains backend-defined effect without pretending to erase records.
- Role grants never exceed accepted ceilings or create tenant data/support authority.
- Pending/unknown authorization changes are never presented as Applied.
- Live actor/device/permission revocation blocks stale page actions.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review tenant/account IDs, role field allow-lists, lockout protections and destructive confirmations. Preserve exact backend semantics and restrict receipt fields to permitted safe evidence.

## Dynamic verification and unavailable-environment handling

Run real AdminApi/PostgreSQL/OpenFGA Playwright cases for concurrent membership changes, repeated keys, initial-Owner protection and revocation. Broader role controls remain absent without accepted model evidence. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Read membership and tenant lifecycle owners.
Build explicit bounded action screens.
Show expected version and material consequences.
Preserve initial-Owner protection and receipt semantics.
Consume accepted role/delegation catalogs only.
Never write OpenFGA tuples from the client.
Explain pending, unknown and conflicting results.
Test concurrent actions and live authority loss.
Keep unavailable future role controls absent.
Deliver source ZIP and lifecycle evidence.
```
