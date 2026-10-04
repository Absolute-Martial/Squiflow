# OpenFGA runtime authorization models

This directory owns the first runtime OpenFGA model contract. It is provider configuration, not application data or a tenant-brand identity.

`tenant-authorization-model.json` defines:

```text
tenant#member             current membership supplied contextually by CoreApi
tenant#workspace_viewer   persisted OpenFGA permission relation
tenant#can_view_workspace member AND workspace_viewer
tenant#order_creator      persisted OpenFGA permission relation
tenant#can_create_order   member AND order_creator
tenant#order_viewer       persisted OpenFGA permission relation
tenant#can_view_orders    member AND order_viewer
```

The Core API performs `Check` only. It does not create stores, publish models, write tuples, or expose OpenFGA administrative credentials. A deployment operator publishes this exact model to the selected store, captures the returned immutable model ID, provisions the required permission relationships through the controlled deployment process, and supplies:

```text
Authorization__OpenFga__ApiUrl
Authorization__OpenFga__StoreId
Authorization__OpenFga__AuthorizationModelId
Authorization__OpenFga__RequestTimeoutSeconds
Authorization__OpenFga__MaximumRetries
Authorization__OpenFga__MinimumRetryDelayMilliseconds
Authorization__OpenFga__CredentialMethod
```

Credential-specific values come from the deployment secret source. Remote provider and token endpoints use HTTPS. The provider identity should have only the read/check access supported by the selected OpenFGA deployment.

The checked-in policy permits one retry for the idempotent `Check` operation on SDK-classified transient failures, with a 100 ms minimum delay. The total provider call, including retry/backoff, remains bounded by `RequestTimeoutSeconds`. Changes to either retry value require latency/failure evidence and requalification of the affected tenant-authorization slices.

Never point the application at an implicit latest model. Publishing a newer model has no runtime effect until its identifier is deliberately configured. Rollback restores the previously qualified model ID and compatible application deployment; it does not delete model history.

## Platform Admin authorization model

`platform-authorization-model.json` is the separate immutable OpenFGA contract used by the private AdminApi. It currently defines the Platform Admin authority relations, including the ADM-006 registry reads:

```text
platform#administrator
platform#can_access_admin
platform#can_provision_tenant
platform#can_onboard_account
platform#can_link_identity
platform#can_manage_memberships
platform#can_manage_tenant_lifecycle
platform#can_read_tenants
platform#can_read_accounts
platform#can_read_memberships
```

The three `can_read_*` relations compute from `administrator` and may also be granted directly to deliberately least-privileged platform principals. AdminApi performs `Check` only and never publishes models or writes tuples at runtime.

OpenFGA authorization-model IDs are immutable and operator-pinned. Changing this JSON file does **not** change a running deployment. When this model changes, the deployment procedure is:

1. publish the exact checked-in `platform-authorization-model.json` to the existing platform store;
2. capture the returned immutable authorization-model ID and read that exact ID back;
3. verify the required platform relation set before changing traffic;
4. update `Authorization__PlatformOpenFga__AuthorizationModelId` for AdminApi and any AdminBootstrap recovery/rerun configuration using the same store;
5. start/roll AdminApi and require `/health/ready` to return `200`; readiness reads the configured pinned model and fails when required ADM-006 relations are absent;
6. run both an allowed registry-read smoke check and an absent-relation denial check;
7. if rollback is required, roll application code and a compatible model pin together.

Never point the application at an implicit latest model and never treat publishing a model as a repin. Existing tuples are store data and are not recreated merely because the model ID changes. The detailed ADM-006 rollout and requalification contract is owned by `docs/implementation/ADMIN_API_PLATFORM_REGISTRY_READS.md`.

Application-owned role/grant administration and cross-system tuple reconciliation remain `NOT_INTRODUCED`. Do not turn manual provider calls into a tenant-facing administration workflow or report them as one.
