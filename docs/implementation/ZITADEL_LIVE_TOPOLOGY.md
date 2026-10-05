# ZITADEL live topology and qualification

**Task:** ADM-004  
**Product version:** `v0.0.1`  
**Decision state:** selected topology; live nonproduction qualification still required before `PRODUCTION_HONEST`

## Accepted topology

SquiFlow starts with **ZITADEL Cloud** and one ZITADEL instance per deployment environment. Production and nonproduction do not share one issuer, application IDs, service credentials or provider-administration surface.

Within each instance SquiFlow owns two separate ZITADEL security projects:

1. **SquiFlow Tenant Access** — interactive tenant-facing Web/Workstation authentication.
2. **SquiFlow Platform Admin** — interactive Platform Admin authentication only.

The two projects have distinct application/client IDs and therefore distinct API audiences. A token issued for Tenant Access must never satisfy the AdminApi audience, and a Platform Admin token does not become tenant business authority merely because both tokens come from the same issuer.

This split is deliberate because applications inside one ZITADEL project share project roles and role assignments. Tenant-facing and platform-administration authority have different blast radius and must not share that role namespace by default.

## Identity and tenancy mapping

The stable SquiFlow external identity remains:

```text
(issuer, subject)
```

One ZITADEL human maps to one SquiFlow account for one issuer. That account may have memberships in zero, one or many SquiFlow tenants. No duplicate ZITADEL user is required merely because one person works with multiple SquiFlow tenants.

**ZITADEL OrganizationId is not SquiFlow TenantId.** SquiFlow tenant membership, tenant availability and OpenFGA/domain authorization remain authoritative. A provider organization or role claim is never sufficient to enter a SquiFlow tenant.

Initial production does not require one provider Organization per SquiFlow tenant. The initial provider organization is an identity-policy/administration container owned by SquiFlow. A customer-specific ZITADEL Organization is introduced only when that customer actually needs provider-local federation, branding, login policy or delegated identity administration and its lifecycle is qualified. That future mapping is explicit metadata, not identifier equality.

This follows the provider model: a ZITADEL user is managed by one Organization but can receive project access outside that Organization. It also avoids duplicating a human who participates in multiple SquiFlow tenants.

## Applications and audiences

The selected logical inventory is:

```text
ZITADEL Cloud instance (one per environment)
|
+-- SquiFlow provider organization
    |
    +-- Project: SquiFlow Tenant Access
    |   +-- Web application          (server/browser login; exact callback set)
    |   +-- Native application       (Workstation; Authorization Code + PKCE)
    |   +-- Tenant API audience      (CoreApi / future WebApi/SyncApi token audience)
    |
    +-- Project: SquiFlow Platform Admin
        +-- Admin Web application    (when introduced)
        +-- Admin API audience       (AdminApi token audience)
```

A deployment may use the provider's supported application/API representation for the API audience, but `Authentication:Audience` remains one exact configured value per host and the Tenant/Admin values must differ.

`AdminApi` continues to validate exact HTTPS issuer + exact Admin audience. `CoreApi` continues to validate the same environment issuer + exact Tenant audience. Neither host accepts an arbitrary token from the instance.

## Provider API service identity

The existing identity-import/link slice verifies already-existing subjects through ZITADEL V2 `GET /v2/users/{user_id}`. ADM-004 does **not** add provider-side user creation.

Use a dedicated non-human provider service identity for this read boundary. It must receive only the narrow ZITADEL administrator permission necessary to read the users that SquiFlow is allowed to import. Do not use an Instance Owner/IAM_OWNER token in application runtime configuration.

If the selected ZITADEL organization layout means the service identity cannot read every intentionally importable human with an organization-scoped viewer/manager role, qualify the smallest broader **read-only** provider role that does. Any write-capable or instance-owner role is a blocker unless a later task explicitly earns it.

The runtime credential remains a secret supplied outside source control. Non-secret project/application/organization IDs may be retained in the deployment inventory.

## Owner and Staff semantics

ZITADEL authenticates the human. It does not determine SquiFlow Owner/Staff authority.

```text
ZITADEL human authentication
        |
        v
SquiFlow account binding (issuer, subject)
        |
        v
SquiFlow tenant membership
        |
        v
OpenFGA + domain authorization
```

A user may be Owner in Tenant A, Staff in Tenant B and have no access to Tenant C while using the same provider subject. Provider project roles are not the authoritative tenant-role store.

Platform administrator access is a separate application/audience and then still requires the existing PlatformAdministration principal/device boundary and platform OpenFGA permission. Tenant Owner/Staff roles never imply Platform Admin authority.

## Human versus machine identities

Interactive account import accepts only a ZITADEL human. A ZITADEL Service Account/Machine identity must fail the current `IExternalIdentityVerifier` human check and can never become an interactive SquiFlow account through the import/link endpoint.

Provider service identities are infrastructure credentials only. They are not SquiFlow users, tenant members or platform operators.

## Enterprise SSO and custom domains

Enterprise federation and customer-specific provider Organizations are **not required for the first accepted topology**. They remain supported directions, not silent dependencies.

Before enabling a customer-specific ZITADEL Organization, project grant or custom login domain in production, qualify at minimum:

- one human who can access more than one SquiFlow tenant without duplicate SquiFlow account identity;
- the selected federated identity login and recovery behavior;
- provider-organization deletion/suspension effects on SquiFlow account recovery;
- exact callback/login-host registration and domain ownership;
- whether delegated customer identity administration is allowed and which provider roles it can assign.

A customer Web custom domain does not create a new issuer. OIDC callbacks return through pre-registered SquiFlow callbacks at the selected canonical ZITADEL issuer.

## Recovery and reprovisioning

Retain a **non-secret topology inventory** per environment containing:

- issuer URL;
- provider Organization ID used to own the SquiFlow projects;
- Tenant Access project ID;
- Tenant Web/Native/API application or client IDs/audiences;
- Platform Admin project ID;
- Admin application/client ID and Admin API audience;
- identity-verifier service identity ID (not its secret/token);
- exact redirect/logout URI inventory;
- provider configuration revision/export reference and evidence timestamp.

Never commit access tokens, client secrets, private keys, recovery codes or session material.

Account recovery is based on the retained SquiFlow binding `(issuer, subject)`. Reprovisioning provider configuration must preserve the issuer and subject identities when claiming transparent recovery. If the issuer changes, that is an external identity-key migration and requires an explicit tested account-link/migration operation; email matching is not recovery.

Provider configuration recovery therefore has two layers:

1. **Topology reprovisioning:** recreate the selected projects/applications/callbacks/policies from reviewed inventory/export and supply new secrets through the deployment secret boundary.
2. **Account-link recovery:** demonstrate that a known human authenticates as the same `(issuer, subject)` and that the existing SquiFlow account resolves without creating a duplicate.

## Live qualification matrix

The read-only harness `eng/qualify-zitadel-topology.py` consumes a non-secret inventory plus secrets/subjects from environment variables. It never creates, updates or deletes provider resources. `eng/test-qualify-zitadel-topology.py` permanently guards project/audience separation, callback restrictions and rejection of owner/write-shaped verifier roles.

Required live nonproduction checks:

| Check | Required result |
|---|---|
| OIDC discovery | HTTPS; returned issuer exactly equals configured issuer; authorization/token/JWKS endpoints are HTTPS and stay on the configured issuer origin |
| Tenant/Admin topology | distinct project IDs and distinct API audiences; exact callbacks are HTTPS except explicitly allowed native loopback callback |
| Known human subject | `GET /v2/users/{id}` succeeds and response is `human` with exact same user ID |
| Known machine subject | provider lookup succeeds but response is non-human; current import semantics therefore reject it |
| Unknown subject | provider returns safe not-found/non-success without exposing credentials |
| Least privilege | dedicated verifier token can perform required user reads but inventory records no instance-owner/runtime blanket credential |
| Multi-tenant identity | one imported `(issuer, subject)` resolves one SquiFlow account that can hold at least two local tenant memberships without identity duplication |
| Audience isolation | Tenant and Admin audience IDs differ; host configuration uses only its selected audience |
| Recovery | exported/reprovisioned non-secret inventory preserves issuer/project/application/callback identity and a known subject resolves the existing account binding |

The script redacts bearer values, emits only SHA-256 fingerprints for test subject IDs and writes no credentials to output. Live provider responses retained as evidence must be redacted to identifiers/fingerprints/types/status and must not contain profile/email/phone/token data unless a reviewer explicitly needs a field and records why.

## Explicit non-claims

ADM-004 does not implement provider-side human creation/invitation, provider organization creation for every tenant, project-grant lifecycle, tenant role synchronization into ZITADEL, Web session persistence, Workstation callback packaging, enterprise SSO, custom-domain activation, account suspension/recovery commands or identity-provider failover.

Those capabilities are owned by later tasks when selected. In particular ADM-005 remains conditional on whether provider-side onboarding is actually selected after this topology is qualified.

## Requalification triggers

Requalify ADM-004 when any of the following change:

- ZITADEL Cloud instance/issuer;
- Tenant/Admin project or application layout;
- CoreApi/AdminApi audience values;
- verifier API authentication method or provider administrator role;
- ZITADEL V2 user-by-ID contract;
- tenant-to-provider-organization mapping policy;
- enterprise SSO/custom-domain/provider delegation policy;
- recovery/export/reprovision procedure;
- stable external identity changes from `(issuer, subject)`.

## Provider source basis

The topology decision was checked against current official ZITADEL documentation for Projects/Applications, B2B Organizations and project grants, administrator scopes, V2 APIs and V2 User-by-ID. Re-review those provider contracts on requalification; this document does not freeze ZITADEL's product surface.

- https://zitadel.com/docs/guides/manage/console/projects-overview
- https://zitadel.com/docs/guides/manage/console/applications-overview
- https://zitadel.com/docs/guides/solution-scenarios/b2b
- https://zitadel.com/docs/guides/manage/console/administrators
- https://zitadel.com/docs/apis/v2
- https://zitadel.com/docs/reference/api/user/zitadel.user.v2.UserService.GetUserByID
