# Web and Workstation login and tenant entry preparation

**Prepared:** 2026-10-14  
**Product version:** v0.0.1  
**Owner request:** prepare login and tenant selection for both apps after the public entry milestone.

This is an implementation handoff based on current executable contracts. Authentication runtime remains `NOT_INTRODUCED`; the [public entry](PUBLIC_APPLICATION_ENTRY.md) does not authenticate a caller. This preparation does not qualify GATE-002, register provider applications, grant permissions, admit a native OIDC dependency or silently close session/callback decisions.

## Current contracts to consume

| Operation | Current response / authority | Source owner |
|---|---|---|
| `GET /api/v1/application/bootstrap` | Public validated deployment identity; no tenant identity | `Application.Branding`, CoreApi bootstrap |
| `GET /api/v1/account` | `{ "accountId": "<uuid>" }`; JWT plus active exact `(issuer, subject)` binding | `AuthenticatedAccountEndpoint.cs`, `AuthenticatedAccountAccess.cs` |
| `GET /api/v1/account/tenants` | Array of `{ "tenantId": "<uuid>", "displayName": "<text>" }`; only current active memberships in active tenants for that account | `TenantMembershipEndpoint.cs`, `PostgresTenantMembershipDirectory.cs` |
| `GET /api/v1/tenants/{tenantId}/workspace` | `{ "tenantId": "<uuid>", "displayName": "<text>" }`; current account, membership-derived context, persisted `workspace_viewer` and final current-membership read | `TenantWorkspaceEndpoint.cs`, `TenantRequestAccess.cs` |

Protected responses are no-store. Token claims or a selected tenant ID do not grant workspace entry. Use the **access token** for CoreApi; an ID token authenticates the OIDC client and is not an API bearer credential. Email, provider organization and role claims do not replace the stable account or current membership.

Current account outcomes include `401/authenticated_identity_incomplete`, `403/account_not_bound` and `403/account_disabled`. Tenant denial uses `403/tenant_access_denied`; membership/provider/admission failures retain the existing safe API failure contract. UI must allow-list known codes and present safe text rather than dumping provider exceptions or arbitrary Problem Details.

## Common interaction

1. Show the existing public brand/connection surface.
2. Initiate maintained Authorization Code + PKCE authentication at the configured exact HTTPS ZITADEL issuer. Validate state/nonce, issuer, client audience, transaction expiry and registered callback.
3. Resolve `/account` with the server/native-held access token. An unbound or disabled account gets an explicit access-unavailable screen; never create/link it automatically by email.
4. Read current `/account/tenants`. Zero memberships shows an empty state with support/sign-out. One or many memberships use an explicit labeled tenant selection action.
5. Call the selected `/workspace`. Enter only after that call succeeds. A listed membership can still lack `workspace_viewer`; explain denied entry without treating list membership as permission.
6. Switching clears previous tenant queries/forms/results, cancels outstanding work and repeats current membership/workspace checks. A removed membership never retains its old screen or authority.
7. Logout clears the app session and tenant state, then reports separately whether refresh/provider revocation or provider logout succeeded. Workstation logout must preserve future pending local work.

The first signed-in slice is account resolution, tenant selection, workspace entry and logout only. Customers/Orders pages, permission administration, local persistence, sync and high-risk actions are separate later responsibilities.

## Web implementation proposal to review

Use the existing Interactive Server host plus `Microsoft.AspNetCore.Authentication.OpenIdConnect` and maintained cookie/antiforgery mechanisms. Keep actor tokens in an explicitly owned **server-side** session store; the browser receives only a secure opaque session reference. Merely setting `SaveTokens=true` on a normal serialized authentication ticket does not prove server-only token custody.

A first single-node, bounded in-memory ticket/token store is the smallest candidate if restart-sign-out is explicitly accepted. It would store no business work and make no durable/multi-node session claim. Durable revocation or cross-node sessions would earn a separate persistence/encryption boundary. This remains a proposal: [open session decisions](../decisions/OPEN_DECISIONS.md) must be settled before implementation.

Proposed browser boundary: host-scoped `Secure`/`HttpOnly` cookie with path `/`, deliberate SameSite, framework OIDC nonce/correlation cookies, antiforgery-protected login/logout/switch actions, local allow-listed return paths and no browser bearer-token storage. Exact idle/absolute expiry, refresh rotation, session count, secret custody, provider/back-channel logout and restart behavior need owner acceptance and live proof. Keep the current CSP and extend only the exact required OIDC form/navigation behavior; never add script eval or arbitrary callback origins to make login work.

Revalidate active account/membership/workspace on entry and reconnect. Protected CoreApi calls always reauthorize. An authenticated Blazor circuit cannot become a cached permission authority. Step-up must use the separately configured provider `azp`, `acr` and `auth_time`; the first login slice does not qualify high-risk commands.

## Workstation implementation proposal to prove

Retain Avalonia/Toolkit. The accepted native route is the system browser, Authorization Code + PKCE S256, a distinct public native client, no embedded client secret and current CoreApi authority.

Duende IdentityModel.OidcClient remains the [POC-gated candidate](../review/WORKSTATION_FRAMEWORK_ADMISSION_RESEARCH.md), not an installed product dependency. Prove its browser adapter against the actual ZITADEL client before pin/admission. The callback remains open: app-claimed HTTPS if Windows packaging proves reliable ownership, otherwise standards-compliant loopback **IP** with an ephemeral port and transaction-bounded listener. Do not treat the existing inventory's illustrative loopback callback as packaging qualification.

Choose/prove Windows protected token storage only if cross-restart credentials are required. An explicitly approved ephemeral first session would require login after restart; it cannot claim protected persistence. Test state/nonce/issuer mismatch, abandoned browser, cancellation, port collision, token expiry/refresh/revocation, account change, clock skew and secret redaction on the real Windows target. Linux/headless public-entry evidence cannot satisfy these native identity claims.

## Provider and deployment inputs

Prepare a real non-secret environment inventory from `deploy/zitadel/adm-004-topology.example.json` outside committed source. Required inputs are the exact nonproduction issuer, Tenant Access project, distinct Web/native client IDs, exact CoreApi audience, canonical Web origin/callback/logout URIs and the selected native callback registration. Platform Admin remains a separate project/audience/origin.

ZITADEL documents `urn:zitadel:iam:org:project:id:{projectid}:aud` as adding that project ID to the access-token audience. Verify the actual emitted `aud` against CoreApi's exact configured audience; do not assume it equals the Web/native client ID. Request `openid` and only necessary claims. `offline_access` earns refresh-token custody/rotation obligations and is not silently added. Provider roles are not business permission truth. [ZITADEL scopes](https://zitadel.com/docs/apis/openidoauth/scopes).

Supply any confidential Web credential through the deployment secret system; never put it in appsettings, the native app, browser assets, logs or this handoff. Use safe nonproduction human accounts already bound/imported through the existing administrative boundary, including zero/two-tenant, disabled and denied-workspace cases.

The existing read-only preflight is reusable:

```sh
python3 eng/test-qualify-zitadel-topology.py
python3 eng/qualify-zitadel-topology.py \
  --inventory deploy/zitadel/adm-004-topology.example.json --static-only

# After a real inventory and required secret/test-identity environment are supplied:
python3 eng/qualify-zitadel-topology.py --inventory /secure/path/nonprod-zitadel-inventory.json
```

Static-only permits placeholders and proves no live login. Live discovery/user topology checks also do not replace actual browser/native OIDC, callback, refresh/logout or account/tenant journey evidence.

## Remaining readiness gaps

| Gap observed from the current tree | Required next evidence/action |
|---|---|
| The checked-in inventory has example issuer/client/project values; the host has no OIDC/session code | Provide a real non-secret inventory and settle the Web session proposal; exercise real provider login/logout |
| GATE-002 still has its own backend case/operating acceptance requirements | Obtain that owner qualification before claiming authenticated frontend production readiness; this public-entry pull-forward does not qualify it |
| `ListActiveAsync` currently materializes all matching memberships without a maximum/page contract | The backend owner must earn a bounded membership-list contract, or document/prove an authoritative membership cap, before a production frontend claims bounded tenant discovery. A client body cap alone does not bound the DB query |
| Native callback/protected secret storage and Windows supported-machine behavior remain unproved | Actual Windows packaging/browser/security POCs with the intended installation profile |
| Web refresh/revocation/rotation/restart and multi-node promises remain open | Accept exact session scope, then add real framework/provider/process regressions |

No tenant list, account identifier, permission, token or callback has been fabricated in the public UI to conceal these gaps.

## Prepared evidence and next implementation boundaries

Source review inspected the actual account/membership/workspace handlers, account denial mapping, the active-membership PostgreSQL query, current identity/session/topology owners, GATE-002 and WEB-002. The existing topology static regressions pass, and static-only inventory output explicitly says `NOT_RUN_STATIC_ONLY`; evidence is retained with the public-entry artifacts. There is no live ZITADEL evidence in this preparation.

Once prerequisites/decisions are accepted, implement Web login/account/tenant/logout in the existing Web host with focused session/browser tests; qualify the native browser/callback/storage POC separately before adding Workstation login. Reuse protected wire contracts through a separately scoped authenticated HTTP adapter only when those actor-token obligations are earned. Do not expand the public `ApplicationEntryClient` into an unbounded generic API client.

Primary mechanism references: [ZITADEL ASP.NET Core example](https://zitadel.com/docs/sdk-examples/dotnet), [native OIDC automatic mode](https://docs.duendesoftware.com/identitymodel-oidcclient/automatic/), [canonical repository identity owner](../security/IDENTITY_AND_SESSIONS.md).
