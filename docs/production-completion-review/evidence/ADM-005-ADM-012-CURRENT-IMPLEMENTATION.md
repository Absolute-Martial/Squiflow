# ADM-005 through ADM-012 current implementation evidence — 2026-10-05

This receipt records the implementation state on the current dirty source tree based on Git HEAD `b9f402649da6552cfd1cba144973465dd1e77ee2`. It is not a substitute for the real PostgreSQL/OpenFGA/full-repository dynamic checks named by the individual tasks.

## Dependency decisions consumed

- **ADM-004:** topology decision/static qualification is present under `docs/implementation/ZITADEL_LIVE_TOPOLOGY.md` and `docs/production-completion-review/evidence/ADM-004-STATIC-DECISION.md`. Live managed-ZITADEL qualification remains pending.
- **WEB-001:** Tenant Web topology is selected in `docs/web/TENANT_WEB_TOPOLOGY.md`; no Web runtime is claimed.
- **UIA-001:** separate Platform Admin Web topology is selected in `docs/admin/PLATFORM_ADMIN_WEB_TOPOLOGY.md`; no Admin Web runtime is claimed.
- **ADM-034:** the backend high-risk admission contract is implemented in `services/core-api/Application.CoreApi/HighRiskActionAdmission.cs` and owned by `docs/security/HIGH_RISK_ACTION_ADMISSION.md`. Live provider `acr` evidence remains pending.
- **OPS-003 conditional dependency:** not activated. ADM-009 through ADM-012 deliberately use bounded synchronous/manual reconciliation; no automatic Worker/background executor is introduced.

## Task disposition

### ADM-005 — provider-account creation decision

**Decision complete through the accepted import/link-only branch.** `v0.0.1` imports/links existing ZITADEL human identities only. SquiFlow does not create/invite provider humans, so the conditional provider-provisioning implementation and its OPS dependencies are not activated.

Owner: `docs/implementation/IDENTITY_ACCOUNT_LIFECYCLE.md`.

### ADM-006 — bounded platform registry reads

The corrected source implementation from the newer baseline is preserved. This batch does not reuse an older gate result to qualify the changed source. Exact-current-source dynamic PostgreSQL/OpenFGA/full-gate qualification remains pending in the receiving environment.

### ADM-007 — local account lifecycle decision

**Decision complete.** Global local-account suspension/reactivation belongs to a future Platform Admin/security capability; Tenant Owner authority remains tenant-scoped membership lifecycle. Provider delete/suspend and general identity merge/unlink are not introduced.

Owner: `docs/implementation/IDENTITY_ACCOUNT_LIFECYCLE.md`.

### ADM-008 — Owner/delegation policy

**Decision complete for the first tenant authorization administration slice.** The current initial Owner is the only tenant role/grant delegator. Delegation is bounded by the compiled `TenantPermissionCatalog`; business permission exercise and delegation authority are separate; direct grants and custom roles compose by allow-union; no explicit deny, arbitrary relation editor, platform authority or delegable `roles.manage` is introduced.

Owner: `docs/implementation/TENANT_AUTHORIZATION_ADMINISTRATION.md`.

### ADM-009 — durable authorization proposal/revision state

**Implemented; dynamic acceptance pending.** Tenancy owns durable proposal intent, one-active-proposal arbitration, authorization revision, transition evidence, direct grant metadata, custom-role metadata/assignments and immutable Owner-transfer receipts. A proposal begins Pending and cannot advance revision. Only an observed provider effect recorded as Applied advances `TenantAuthorizationRevision`, exactly once. Semantic idempotency is caller/tenant scoped. Reconciliation retries preserve the last provider failure code. Duplicate direct grant/revoke requests conflict without revision churn.

Primary source:
- `modules/tenancy/Application.Tenancy/TenantAuthorizationAdministration.cs`
- `modules/tenancy/Application.Tenancy.Postgres/Persistence/PostgresTenantAuthorizationAdministrationStore.cs`
- `modules/tenancy/Application.Tenancy.Postgres/Migrations/202610050001_TenantAuthorizationAdministration.cs`

### ADM-010 — apply/reconcile one permission grant

**Implemented; dynamic acceptance pending.** CoreApi maps durable proposals to the explicitly pinned tenant OpenFGA model, performs duplicate-safe writes/deletes, observes intended provider state with higher consistency and completes local state only after observation. Timeouts/network ambiguity become Uncertain; incompatible model/provider validation becomes Failed. Reconciliation rechecks the original delegator immediately before provider mutation.

Primary source:
- `services/core-api/Application.CoreApi/Authorization/TenantAuthorizationAdministrationProvider.cs`
- `infrastructure/authorization/openfga/tenant-authorization-model.json`

### ADM-011 — revocation and guarded Owner handoff

**Implemented; dynamic acceptance pending.** Revocation uses the same durable/provider-observation protocol. Owner handoff is a local serializable Tenancy transaction requiring current initial Owner, active target, expected tenant revision, semantic idempotency and the ADM-034 high-risk admission boundary. It advances tenant, affected membership and authorization revisions and preserves a replay receipt. Owner transfer is rejected while any Pending/Uncertain authorization proposal exists, preventing a provider-reconciliation race with delegator transfer.

Primary source:
- `modules/tenancy/Application.Tenancy.Postgres/Persistence/PostgresTenantAuthorizationAdministrationStore.cs`
- `services/core-api/Application.CoreApi/TenantAuthorizationAdministrationEndpoint.cs`
- `services/core-api/Application.CoreApi/HighRiskActionAdmission.cs`

### ADM-012 — one tenant custom-role lifecycle

**Implemented; dynamic acceptance pending.** Tenant-scoped roles support bounded create/revise/retire and one assignment/unassignment operation. Roles use stable IDs, revision checks, 1..32 catalog permissions, maximum 50 active roles/tenant and maximum 50 active assignments/role. OpenFGA uses the stable `role#assignee` userset; no model-per-role is deployed. Cross-tenant role identity, unknown permissions, retired-role edits and stale revisions fail without widening authority. Retirement preserves local history while removing provider role edges/assignments through reconciliation.

Primary source:
- `modules/tenancy/Application.Tenancy/TenantAuthorizationAdministration.cs`
- `modules/tenancy/Application.Tenancy.Postgres/Persistence/PostgresTenantAuthorizationAdministrationStore.cs`
- `services/core-api/Application.CoreApi/TenantAuthorizationAdministrationEndpoint.cs`
- `services/core-api/Application.CoreApi/Authorization/TenantAuthorizationAdministrationProvider.cs`

## Database privilege boundary

`deploy/database/grant-core-api-runtime.sql` grants only the additional CoreApi privileges required by this state machine: narrow authorization-table SELECT/INSERT/UPDATE, INSERT-only immutable event evidence, tenant revision update and membership `revision/is_initial_owner` update. It does not grant broad Tenancy mutation, DELETE/TRUNCATE/DDL or schema ownership. The runtime-role regression verifies exact mutable columns rather than merely table-level privileges.

## Authored regression guards

- `tests/unit/Application.Tenancy.Tests/TenantAuthorizationAdministrationTests.cs`: permission catalog bounds, normalization, unknown-permission rejection, semantic fingerprints, role assignment/Owner-transfer contracts.
- `tests/integration/Application.Tenancy.Postgres.Tests/TenantAuthorizationAdministrationPostgresTests.cs`: semantic idempotency, one-active arbitration, Pending/Uncertain/Applied, retry evidence retention, exactly-once revision advancement, duplicate grant/revoke no-op rejection, role lifecycle/history, stale revisions, inactive member/cross-tenant rejection, Owner handoff/replay, active-proposal Owner-transfer block and migration evidence protection.
- `tests/integration/Application.CoreApi.Tests/OpenFgaTenantAuthorizationTests.cs`: real OpenFGA direct grant/revoke, membership intersection, tenant-scoped role usersets/assignments, role retirement propagation and pinned-model incompatibility.
- `tests/integration/Application.CoreApi.Tests/HighRiskActionAdmissionTests.cs`: missing deployment config, wrong client/ACR, stale/future `auth_time`, arbitrary-claim bypass rejection and accepted recent provider evidence.
- `tests/integration/Application.Orders.Postgres.Tests/CoreApiRuntimeRoleProvisioningTests.cs`: restricted runtime role and exact UPDATE-column boundary.
- `tests/integration/Application.Tenancy.Postgres.Tests/TenantMembershipLifecyclePostgresTests.cs`: migration rollback continues to preserve historical lifecycle evidence after the newer authorization migration exists.

## Executed evidence in this environment

- Exact SDK: .NET SDK `10.0.401`.
- `dotnet build modules/tenancy/Application.Tenancy/Application.Tenancy.csproj -c Release --no-restore`: **exit 0, 0 warnings, 0 errors** after final host-neutral edits.
- ADM-004 Python qualification harness compiles and its static negative regressions pass.
- The new high-risk admission and tenant-authorization HTTP/authorization sources were independently compiled with the .NET 10 Web SDK against thin provider stubs during this implementation pass: **0 warnings, 0 errors**. This is compile evidence only, not provider/runtime qualification.
- `git diff --check`: passed at package finalization.
- development-task catalog validation: **119 tasks valid** after refreshed task hashes.
- production-completion package review: **173 review links checked**; tracked review SHA-256 verification passed.
- targeted credential-material scan over the new ZITADEL/provider/authorization files: passed.

A provider-backed Tenancy.Postgres build was attempted with the exact SDK but could not resolve the locked NuGet graph because this container cannot reach `https://api.nuget.org/v3/index.json` (`NU1301`) and has no populated package cache. This environment also has no Docker daemon/socket. Therefore no PostgreSQL/OpenFGA/full `./eng/verify.sh` pass is claimed for the introduced source.

## Exact receiving checks still required

Run in the user's normal .NET 10.0.401 + Docker/Testcontainers environment:

```bash
dotnet test tests/unit/Application.Tenancy.Tests/Application.Tenancy.Tests.csproj -c Release -p:RestoreLockedMode=true
dotnet test tests/integration/Application.Tenancy.Postgres.Tests/Application.Tenancy.Postgres.Tests.csproj -c Release -p:RestoreLockedMode=true
dotnet test tests/integration/Application.CoreApi.Tests/Application.CoreApi.Tests.csproj -c Release -p:RestoreLockedMode=true
dotnet test tests/integration/Application.Orders.Postgres.Tests/Application.Orders.Postgres.Tests.csproj -c Release -p:RestoreLockedMode=true
./eng/verify.sh
```

Inspect final exits, every pass/fail/skip summary and all analyzer/build warnings. Do not convert this implementation receipt into `PRODUCTION_HONEST` until those real-boundary checks are green on the exact packaged source.

## Receiving full-gate failure and repair pass — 2026-10-05

The first receiving `./eng/verify.sh` run against this uncommitted ADM-005–ADM-012 batch **did not qualify the archive**. The Release build was clean (0 warnings / 0 errors), but four tests and two architecture guards failed. Those failures are retained as acceptance evidence rather than converted into a pass:

1. `OpenFgaTenantAuthorizationTests.RealServerRequiresPersistedOrderPermissionsAndUsesPinnedModel` used positional `type_definitions[1]`; ADM-012 inserted the `role` type there, so the intended tenant-model removals silently did nothing. The drift fixture now finds the `tenant` type by name and asserts each removal succeeds.
2. `OpenFgaTenantAuthorizationTests.AdministrationProviderReconcilesGrantRevokeAndTenantScopedCustomRoleAgainstPinnedModel` asserted the higher-level permission result while provider administration owns only OpenFGA grant/role edges and membership availability is admitted separately through PostgreSQL tenant context. The provider test now checks the provider-owned `order_viewer` relation directly with the pinned model and higher consistency.
3. `TenantAuthorizationAdministrationPostgresTests.MigrationRollbackRefusesToDiscardAuthorizationEvidence` used a historical migration target through the convenience database extension that did not resolve in the receiving run. It now uses `IMigrator.MigrateAsync` with the existing migration ID, matching the repository's proven rollback-test pattern.
4. `TenantAdmissionTests.TenantContractsDescribeCapacityRejectionWithoutChangingAccountContracts` exposed that the new tenant-authorization endpoints omitted the established OpenAPI `429` capacity contract. Every ADM-009–012 tenant route now declares `429`.
5. `RuntimeSqlRemainsInEmbeddedAdapterResources` correctly rejected the new PostgreSQL store's inline runtime SQL. All authorization-administration runtime statements were moved into 33 adapter-owned embedded `Sql/*.sql` resources loaded through `TenantAuthorizationAdministrationSql`; no runtime SQL literal remains in Tenancy persistence C#.
6. `ActiveSourceAndBuildIdentitiesContainNoDevelopmentCodename` correctly rejected a codename-bearing ACR fixture. The active test identity now uses the neutral `urn:application:assurance:high-risk` value.

The SQL refactor also exposed and removed a duplicate `tenant_id` parameter insertion in authorization-event persistence.

Post-repair evidence available in this environment:

- targeted active-source `dotnet format whitespace --verify-no-changes`: passed;
- `Application.Tenancy` Release build on .NET SDK 10.0.401: **0 warnings / 0 errors**;
- runtime-SQL architecture-rule equivalent: passed; all 33 authorization SQL loader fields resolve to embedded resource files;
- active source/build codename scan equivalent: passed;
- development-task catalog: 119 tasks valid;
- production-completion review: 173 links checked;
- `git diff --check`: passed.

**The repaired source is still `EVIDENCE_PENDING`.** The full PostgreSQL/OpenFGA/architecture test suite and one complete normal `./eng/verify.sh` must be rerun in the receiving environment. No qualification is inferred from these source repairs.
