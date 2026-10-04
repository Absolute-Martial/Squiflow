# AdminApi bounded platform registry reads

**Product version:** `v0.0.1`

**Implementation unit:** `ADM-006`

## Declared scope

AdminApi exposes server-authoritative, read-only platform registry detail and keyset browse for the three resources needed by the future Admin Web onboarding spine:

- tenants;
- local application accounts;
- tenant memberships.

This slice does not introduce UI, provider-side identity administration, search/count APIs, role administration, account recovery, account lifecycle commands, or tenant-business data reads.

## Routes and permissions

| Route | Platform OpenFGA permission |
|---|---|
| `GET /api/v1/platform/tenants` | `can_read_tenants` |
| `GET /api/v1/platform/tenants/{tenantId}` | `can_read_tenants` |
| `GET /api/v1/platform/accounts` | `can_read_accounts` |
| `GET /api/v1/platform/accounts/{accountId}` | `can_read_accounts` |
| `GET /api/v1/platform/tenants/{tenantId}/memberships` | `can_read_memberships` |
| `GET /api/v1/platform/tenants/{tenantId}/memberships/{accountId}` | `can_read_memberships` |

The three relations are distinct. Existing `administrator` authority computes into each relation, while the OpenFGA model also permits a principal to receive one read relation directly without receiving the others. Every request still requires the exact AdminApi authentication boundary: OIDC principal, HTTPS, active registered Admin-device certificate and current pinned-model OpenFGA permission.

Authorization is evaluated before cursor parsing or registry lookup so a caller without the required permission cannot use validation/not-found differences to discover resource existence.

## Read projections

Tenant detail/browse returns only:

- tenant ID;
- display name;
- active/suspended availability;
- current tenant lifecycle revision;
- created timestamp;
- suspension timestamp when present.

Account detail/browse returns only:

- account ID;
- active/disabled availability;
- created timestamp;
- disabled timestamp when present.

No external identity issuer/subject, provider payload or binding list is exposed. Account lifecycle is not yet introduced, so there is no invented account revision.

Membership detail/browse returns only:

- tenant ID;
- account ID;
- invited/active/suspended/removed availability;
- current expected-revision value used by supported lifecycle commands;
- invited/activated/suspended/removed timestamps;
- protected initial-Owner flag.

One detail query is one PostgreSQL statement and therefore observes one PostgreSQL statement snapshot. It returns the revision a subsequent revision-checked command can use. A later concurrent change may make that revision stale; the command remains responsible for rejecting the stale expected revision.

## Bounded browse contract

Browse is deterministic keyset pagination, not offset pagination.

- default page size: `50`;
- accepted page size: `1..100`;
- ordering: tenant/account UUID primary-key order; memberships by account UUID within one tenant;
- request cursor: optional `cursor`;
- response cursor: optional `nextCursor`;
- cursor format is versioned `v1`, Base64Url encoded and resource typed (`tenant`, `account`, or `membership`).

A cursor from another registry, malformed cursor, unsupported version, duplicate cursor/limit parameter, zero/negative limit or limit above 100 returns safe `400` without a database browse.

No total count, substring search, provider-identity search or arbitrary sort/filter claim is introduced.

## Persistence and side effects

The read adapters use parameterized, embedded SQL owned by IdentityAccess/Tenancy and existing primary-key indexes. No schema migration or additional index is required for the accepted order:

- `identity_access.accounts(id)` primary key;
- `tenancy.tenants(id)` primary key;
- `tenancy.memberships(tenant_id, account_id)` primary key.

Registry reads do not write tenant/account/membership state, OpenFGA tuples, command receipts or access-audit rows. This is deliberate: ADM-006 explicitly requires a read to perform no durable mutation. Mutation endpoints keep the existing durable admission-audit behavior.

## Startup access guards

AdminApi now validates all mapped routes before serving requests. Every route must have exactly one recognized `AdminEndpointAccessMetadata`. Public routes cannot declare a platform permission. Protected routes must:

- require ASP.NET authorization;
- not allow anonymous access;
- declare exactly one recognized `AdminEndpointPermissionMetadata`.

CoreApi now has a parallel two-part declaration. `RequireAuthorization()` proves authentication, while every `Authorized*` access classification must separately declare matching `CoreApiApplicationAuthorizationMetadata`. The existing resource-specific OpenFGA checks remain in the handlers; the new metadata makes the application-authorization requirement visible to startup validation so JWT authentication alone cannot satisfy the route classifier guard.

## Regression guards

Authored receiving tests cover:

- real PostgreSQL tenant/account/membership detail and keyset browse;
- page bounds and resource-specific cursor rejection;
- membership/tenant lifecycle revision projection;
- read-only no-access-audit behavior;
- authorization before existence/cursor disclosure;
- least-privilege tenant-reader denial from account/membership registries;
- AdminApi startup classification/authentication/platform-permission consistency;
- CoreApi startup requirement for matching application-authorization metadata.

Required dynamic acceptance commands:

```bash
dotnet test tests/integration/Application.AdminApi.Tests/Application.AdminApi.Tests.csproj -c Release -p:RestoreLockedMode=true
dotnet test tests/integration/Application.CoreApi.Tests/Application.CoreApi.Tests.csproj -c Release -p:RestoreLockedMode=true
./eng/verify.sh
```

The real-boundary runs must use PostgreSQL and OpenFGA Testcontainers and skip no tests.

## Current receiving status

The implementation and regression tests are present in the current working tree. The supplied offline SDK provides .NET SDK 10.0.401, but this sandbox has no locked NuGet package cache/network access and no Docker daemon/socket. Therefore the new code cannot be dynamically production-qualified here. Under the repository gate model this introduced ADM-006 responsibility remains `BLOCKED` on those exact receiving checks; the previously accepted GATE-001 baseline remains accepted and is not regressed by documentation wording.

## Requalification triggers

Requalify when any of the following change:

- registry projection fields or PII policy;
- cursor version/order/page bounds;
- IdentityAccess/Tenancy registry SQL or relevant schema/indexes;
- AdminApi platform read relations or authorization model;
- AdminApi route access/permission metadata or startup validator;
- CoreApi endpoint access/application-authorization metadata or validator;
- AdminApi request-budget/admission ordering affecting authorization-before-read behavior.
