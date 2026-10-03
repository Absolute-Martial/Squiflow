# AdminApi membership lifecycle

**Status:** qualified narrow slice, 2026-10-03
**Product version:** remains v0.1.0

## Declared scope

This slice adds platform-authorized AdminApi commands to:

- invite an already-onboarded active account to an active tenant;
- bootstrap exactly one initial Owner membership as immediately active;
- activate an invitation, suspend/reactivate an ordinary membership, and remove
  a membership using an expected revision;
- suspend or reactivate a tenant using an expected revision.

A newly verified identity is first onboarded through the preceding account slice,
then its stable account ID is used here. Provider user creation and ZITADEL
organization mapping remain outside this slice.

Tenant suspension immediately makes every membership ineffective because
`TenantContext` resolution and active-membership listing independently require an
active tenant. Reactivation restores access only for memberships that are still
active; it does not reactivate suspended or removed memberships.

The initial Owner marker is an immutable bootstrap designation, not the complete
Owner permission model. Until baseline permission administration exists, the
initial Owner cannot be suspended or removed and a second initial Owner cannot be
created. This avoids silently orphaning the tenant while not inventing Staff
defaults, delegation ceilings or tenant role administration.

## Authority and durability

Every command requires the existing OIDC identity, active principal-bound
Admin-device certificate, retained access audit and a current higher-consistency
platform OpenFGA decision. Membership and tenant-lifecycle permissions are
separate platform relations.

Tenancy owns state transitions, expected revisions and caller-scoped semantic
idempotency. PostgreSQL advisory locks serialize idempotency keys, each membership,
the per-tenant initial-Owner claim and tenant lifecycle. Successful state plus its
principal/device receipt commit atomically. Failed validation and conflicts retain
no success receipt.

The AdminApi runtime role can select/insert memberships and receipts and update
only lifecycle columns. It can update only tenant availability/revision/suspension
columns. It cannot rewrite identity accounts, bootstrap authority, immutable
membership identity/Owner designation or retained audit evidence.

## HTTP contract

- `POST /api/v1/platform/tenants/{tenantId}/memberships`
- `POST /api/v1/platform/tenants/{tenantId}/memberships/initial-owner`
- `POST /api/v1/platform/tenants/{tenantId}/memberships/{accountId}/{activate|suspend|remove}`
- `POST /api/v1/platform/tenants/{tenantId}/lifecycle/{suspend|reactivate}`

All commands require exactly one `Idempotency-Key`, bounded exact-shape JSON and
the normal AdminApi no-store response boundary.

## Evidence

- `Application.Tenancy.Tests` protects intent fingerprints, revision transitions,
  malformed keys and initial-Owner protection.
- `TenantMembershipLifecyclePostgresTests` exercises real PostgreSQL atomicity,
  replay, concurrency, account/tenant validity, initial-Owner uniqueness,
  suspension access effects, migration rollback and least privilege.
- `MembershipLifecycleBoundaryTests` exercises the real AdminApi, PostgreSQL and
  platform OpenFGA request boundary.
- `grant-admin-api.sql` is the deployment-owned least-privilege contract.

The normal parallel `./eng/verify.sh` on 2026-10-03 passed locked restore,
formatting, Release build and all **640 tests across 15 suites**, zero
failures/skips and zero warnings/errors. It includes real PostgreSQL/OpenFGA
membership lifecycle qualification. The declared narrow scope is
`PRODUCTION_HONEST`; `BLOCKED = none`. The subsequent JSON-kind correction and
its additional boundary guards are owned by `ADMIN_API_JSON_BOUNDARY_CONTRACT.md`.
This evidence does not qualify a production deployment or later AdminApi slices.

## Explicit non-claims

This slice does not introduce Staff defaults, general Owner permissions, role or
grant administration, tenant OpenFGA tuple writes, authorization revision,
delegation, invitation delivery, provider-side users/organizations, self-service
tenant administration, or operator/Admin-device lifecycle.

## Requalification triggers

Requalify when membership/tenant states, initial-Owner handoff, revision or
idempotency semantics, tenant-context resolution, runtime grants, platform
permissions, Admin-device validation or retained audit behavior changes.
