# OpenFGA Tenant Workspace Model

This directory owns the first runtime OpenFGA model contract. It is provider configuration, not application data or a tenant-brand identity.

`tenant-workspace-model.json` defines:

```text
tenant#member             current membership supplied contextually by CoreApi
tenant#workspace_viewer   persisted OpenFGA permission relation
tenant#can_view_workspace member AND workspace_viewer
```

The Core API performs `Check` only. It does not create stores, publish models, write tuples, or expose OpenFGA administrative credentials. A deployment operator publishes this exact model to the selected store, captures the returned immutable model ID, provisions the initial `workspace_viewer` relationships through the controlled deployment process, and supplies:

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

The checked-in policy permits one retry for the idempotent `Check` operation on SDK-classified transient failures, with a 100 ms minimum delay. The total provider call, including retry/backoff, remains bounded by `RequestTimeoutSeconds`. Changes to either retry value require latency/failure evidence and requalification of the tenant-workspace slice.

Never point the application at an implicit latest model. Publishing a newer model has no runtime effect until its identifier is deliberately configured. Rollback restores the previously qualified model ID and compatible application deployment; it does not delete model history.

Application-owned role/grant administration and cross-system tuple reconciliation remain `NOT_INTRODUCED`. Do not turn manual provider calls into a tenant-facing administration workflow or report them as one.
