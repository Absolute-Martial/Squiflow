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

Registry reads do not write tenant/account/membership state, OpenFGA tuples or command receipts. They **do** synchronously append the existing durable Platform Admin access-audit evidence before the registry operation proceeds. This is an explicit security-side-effect exception to read-only business state: an operator enumerating a platform registry must leave retained actor/device/outcome evidence. If the audit append cannot complete, the protected read does not silently continue.

Audit operations are bounded and non-PII: tenant/account browse records the registry operation; tenant/account detail records the opaque target GUID; membership browse/detail records the opaque tenant/account GUIDs. Every page request in a multi-page traversal is separately audited. The audit records authorization/access, not a claim that the subsequent PostgreSQL read completed successfully.

## Platform OpenFGA model rollout and pinned-ID compatibility

The checked-in platform authorization model is immutable once published. The three ADM-006 relations (`can_read_tenants`, `can_read_accounts`, `can_read_memberships`) therefore require a **new OpenFGA authorization model ID**. A deployment pinned to the previous model ID is incompatible with ADM-006 even though the JSON file in this repository has changed.

Required rollout order:

1. publish `infrastructure/authorization/openfga/platform-authorization-model.json` to the existing platform store and capture the returned immutable model ID;
2. read that exact returned model back and verify the complete platform contract, including all three registry-read relations;
3. keep existing `administrator` tuples in the same store; add direct read-relation tuples only for deliberately least-privileged principals;
4. update the deployed `Authorization__PlatformOpenFga__AuthorizationModelId` used by AdminApi and by any AdminBootstrap recovery/rerun configuration to the returned ID;
5. roll AdminApi and require `/health/ready` to return `200` before routing protected Admin traffic;
6. run allowed/denied registry smoke checks, including one least-privilege read relation and one absent relation;
7. roll back application code and model pin together if rollback is required. Do not repin ADM-006 code to a pre-ADM-006 model.

AdminApi readiness now reads the **configured pinned model**, not the latest model, and verifies the complete platform relation contract. A stale pin therefore fails readiness before registry traffic is considered healthy. No startup fallback to “latest model” or model mutation exists.

## Startup access guards

AdminApi now validates all mapped routes before serving requests. Every route must have exactly one recognized `AdminEndpointAccessMetadata`. Public routes cannot declare a platform permission. Protected routes must:

- require ASP.NET authorization;
- not allow anonymous access;
- declare exactly one recognized `AdminEndpointPermissionMetadata`.

Protected AdminApi permission enforcement no longer depends on endpoint-handler code. `UseAdminApiPlatformAuthorization` runs after ASP.NET authentication/authorization, reads the single validated `AdminEndpointPermissionMetadata` plus the validated bounded audit-operation metadata from the matched trusted endpoint, performs the existing TLS/device/OpenFGA/audit admission, and stores only the resulting principal/device access context for the handler. Handlers cannot select, replace or omit the OpenFGA permission. Declaration and enforcement therefore cannot drift as two independent constants, and a newly mapped protected route cannot reach its handler without the metadata-driven admission middleware succeeding.

CoreApi now uses the same principle for route-admission authorization. Each `Authorized*` access classification maps to a non-empty typed requirement set stored in `CoreApiApplicationAuthorizationMetadata`; startup validates the contract and the required `tenantId` route boundary, then `UseCoreApiApplicationAuthorization` resolves the current tenant context and executes that exact metadata set before endpoint dispatch. Existing endpoint helpers observe the already-admitted middleware state and do not perform an independent permission decision. Order-action capability probes that calculate optional response affordances remain explicit secondary checks and are not route-admission authority.

## Regression guards

Authored receiving tests cover:

- real PostgreSQL tenant/account/membership detail and keyset browse;
- page bounds and resource-specific cursor rejection;
- membership/tenant lifecycle revision projection;
- durable success/denial access-audit behavior while registry/business state remains unchanged;
- authorization before existence/cursor disclosure;
- stale pinned platform model missing the ADM-006 relations fails readiness and registry use;
- absent-relation denial, authorized unknown-ID `404`, malformed cursor `400`, and full multi-page traversal without duplicates;
- least-privilege tenant-reader denial from account/membership registries;
- AdminApi startup classification/authentication/platform-permission consistency;
- AdminApi middleware execution of the permission and audit operation declared by trusted endpoint metadata, with no handler-selected permission;
- CoreApi startup requirement for matching application-authorization metadata.
- CoreApi pre-handler middleware execution of the validated typed application-authorization contract.

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
