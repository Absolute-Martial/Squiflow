# ADM-030 — Conditional time-bounded tenant support access

Task ID: ADM-030
Phase: 01-admin-platform
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: ADM-014, ADM-034, OPS-013
Release requirement: CONDITIONAL

## Outcome

Support one explicitly selected tenant/resource read or corrective operation using narrow, expiring, attributable support authority when the release needs cross-tenant support.

## Current basis and canonical inputs

Read [support/security separation](../../security/ENCRYPTION_POLICY_KEY_LIFECYCLE_AND_PRIVILEGED_ACCESS.md), [Admin surfaces](../../admin/ADMIN_SURFACES.md), [tenant permissions](../../security/TENANT_PERMISSIONS.md), [current truth](../../../README.IMPLEMENTATION.md), [rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md).

## Scope and exclusions

Allowed areas: one support request/approval/expiry contract, durable receipt and audit, protected AdminApi adapter and owning capability's narrow application operation. Name exact resource and fields in the write allowlist. No unrestricted customer browser, arbitrary SQL, impersonated permanent Owner grant or ordinary AdminApi-to-CoreApi proxy.

## Decisions/prerequisites

Owner must accept support purpose, allowed operation, reason/reference, tenant acknowledgement or approval when needed, duration, revocation and returned field policy before implementation. Metadata-only incident support is valid if customer data access is not needed.

## Acceptance and edge cases

- Platform entry/encryption authority alone cannot open tenant business data.
- Grant names one actor, tenant, operation/resource scope and authoritative expiry.
- Revoke/expiry is checked at each new sensitive admission; stale UI/session cannot revive it.
- Concurrent approval/revocation and same-key requests yield attributable stable outcomes.
- Audit identifies the support action without copying customer payloads into telemetry.
- Admin host independence and capability-owned tenant isolation remain intact.

## Security/static review

Trace confused-deputy, scope expansion, approval replay, clock tolerance, RLS and sensitive projection boundaries. Scope ownership is not delegated to user-supplied tenant/resource headers.

## Dynamic verification and unavailable-environment handling

Run `./eng/verify.sh` and real PostgreSQL/OpenFGA/host denial, expiry, revoke/admission race and response-loss tests. Show ordinary operation remains independent of AdminApi availability. Required provider evidence remains pending if unavailable.

## Handoff

Return source ZIP/hash, accepted support policy, exact field projection, tests, evidence and blockers. UIA-008 consumes the selected safe flow later.

## Assignable prompt

```text
Execute ADM-030 only after one support operation is selected.
Read support, tenant and Admin authority owners.
Agree exact actor, tenant, resource, duration and returned fields.
Implement a durable narrow support request and admission path.
Keep support distinct from encryption and permanent tenant roles.
Test expiry, revocation, scope denial and repeated requests.
Review audit, RLS, approval binding and customer-data disclosure.
Return source ZIP, hashes, owner policy and exact evidence.
Do not commit or introduce unrestricted impersonation or SQL.
```
