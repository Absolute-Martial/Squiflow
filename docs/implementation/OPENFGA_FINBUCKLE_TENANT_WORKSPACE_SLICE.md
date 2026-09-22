# OpenFGA and Finbuckle Tenant Workspace Slice

**Status:** `PRODUCTION_HONEST` for the declared narrow scope

**Product version:** `v0.1.0`

**BLOCKED:** none

## Production intent

An authenticated account can request a selected tenant's workspace only when the route candidate matches a current authoritative membership and the pinned OpenFGA model grants the independent workspace-view permission; tenant substitution and authorization-provider failure cannot become access.

## Qualified scope

| Claim | Owner | Evidence | Recurring guard |
|---|---|---|---|
| Finbuckle resolves `{tenantId}` as an untrusted route candidate and never replaces membership authority. | CoreApi host composition | Real ASP.NET pipeline success, malformed-route, nonmember and cross-account tests. | `Application.CoreApi.Tests` on every `./eng/verify.sh`. |
| `TenantContext` exists only after current account-membership validation. | Tenancy plus CoreApi endpoint | Existing Tenancy resolver tests and tenant-workspace pipeline tests. | Tenancy unit and CoreApi integration suites. |
| Workspace read requires persisted `workspace_viewer` plus current contextual membership under exact model/store IDs. | CoreApi authorization adapter and checked-in OpenFGA model | Real OpenFGA 1.21.0 container test denies before tuple, allows after tuple, and still uses the pinned model after a newer model exists. | OpenFGA provider integration test on every Docker-capable verification run. |
| Provider timeout/error does not grant access or disclose provider details. One idempotent SDK retry is explicit and remains inside the total deadline. | CoreApi adapter/endpoint | Deterministic timeout/caller-cancellation tests, bounded timeout/retry configuration, safe `503` pipeline test and provider exception mapping. | CoreApi integration suite. |
| Tuple identifiers and metrics avoid names/email and unbounded tenant labels. | CoreApi adapter | Source inspection plus response/log/metric contract; tuples use normalized opaque account/tenant GUIDs. | Review on authorization/telemetry adapter change. |

## `NOT_INTRODUCED`

- Owner/Staff/custom-role models and permission catalog;
- application tuple writes, grant/revoke workflows, reconciliation and authorization revision;
- tenant branding/settings persistence or mutation;
- tenant-owned business resources and PostgreSQL RLS;
- a production OpenFGA deployment topology, backup/restore qualification or automated model rollout;
- Finbuckle EF integration, because no tenant-owned business table exists yet;
- tenant-specific Autofac runtime acquisition.

These absences do not weaken the declared read boundary: its initial permission relationships are deployment-provisioned, and the application cannot report an unimplemented grant/revoke operation as successful.

## Requalification triggers

Requalify this slice when the route strategy, membership authority, tuple identifier format, model JSON, model/store selection, consistency, credential mode, timeout/error mapping, OpenFGA SDK/server compatibility, or endpoint contract changes. Adding any application tuple mutation activates the separate durability, idempotency, ambiguity, reconciliation and audit obligations before that path can ship.
