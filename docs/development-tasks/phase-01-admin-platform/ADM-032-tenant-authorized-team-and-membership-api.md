# ADM-032 — Tenant-authorized team and membership API

Task ID: ADM-032
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-012, ADM-025, ADM-034
Release requirement: REQUIRED

## Outcome

Let an authorized tenant administrator discover and manage its supported team through CoreApi without requiring Platform Admin authority for routine tenant operations.

## Current basis and canonical inputs

Read [Admin surfaces](../../admin/ADMIN_SURFACES.md), [tenant permissions](../../security/TENANT_PERMISSIONS.md), [membership lifecycle](../../implementation/ADMIN_API_MEMBERSHIP_LIFECYCLE.md), [current truth](../../../README.IMPLEMENTATION.md), [rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Existing membership mutations are private platform endpoints; WEB-012 needs a distinct tenant-authorized ingress.

## Scope and exclusions

Allowed areas: reuse Tenancy/IdentityAccess public lifecycle/query contracts, add narrow tenant actor/admission contracts where earned, protected CoreApi team browse/invite/selected transition adapters, scoped model relations and focused tests. Do not proxy through AdminApi or duplicate membership transition rules. No provider-side user creation, arbitrary identity linking or Workstation device lifecycle.

## Decisions/prerequisites

Accept exact team actions, invite-existing-account versus provider invitation path, safe lookup/projection and tenant administrator delegation ceiling. Use ADM-008 defaults, ADM-011 guarded Owner primitive and ADM-007 account availability meaning. Unknown account discovery must not become global account enumeration.

## Acceptance and edge cases

- Tenant administrator manages only its tenant under distinct current team permissions.
- Membership activation alone grants no operation permission; role/grant changes use accepted reconciliation.
- Suspended local accounts/memberships cannot gain access through an invitation replay.
- Initial/last recoverable Owner restrictions survive simultaneous removals and handoffs.
- Expected revisions and caller-scoped keys preserve one transition and retained actor history.
- Bounded team reads disclose only approved fields, including appropriate pending states.

## Security/static review

Review tenant versus platform actor provenance, invitation spoofing/enumeration, delegation ceiling, replay and safe membership/account projections.

## Dynamic verification and unavailable-environment handling

Run `./eng/verify-host.sh core-api` and `./eng/verify.sh` plus real PostgreSQL/OpenFGA cross-tenant, suspended-account, last-Owner race and same-key tests. Missing boundaries are pending evidence; named mocks alone are insufficient.

## Handoff

Return source-only ZIP/hash, API/permission contract, shared-file requests, exact evidence and blockers. WEB-012 consumes these team APIs; platform lifecycle endpoints retain their separate authority.

## Assignable prompt

```text
Implement only ADM-032's accepted tenant team API scope.
Inspect existing Tenancy transitions and platform adapters first.
Agree supported actions and safe account lookup with the owner.
Reuse capability rules through tenant-authorized CoreApi ingress.
Keep membership, role grants and platform authority separate.
Preserve Owner guards, revisions, semantic retries and history.
Run actual tenant-denial, account-state and concurrency regressions.
Return source ZIP, hashes, contract changes and exact evidence.
Do not commit, proxy through AdminApi or duplicate lifecycle meaning.
```
