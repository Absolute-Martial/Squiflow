# Signed-in Web and Workstation entry

**Product version:** v0.0.1  
**Scope requested:** complete login, account resolution, tenant selection, workspace entry, switching and logout after the public entry milestone.  
**State:** `BLOCKED` receiving qualification. Source/runtime components and controlled-protocol regressions exist; actual ZITADEL application/topology and Windows supported-machine evidence are still required. This pull-forward does not qualify GATE-002 or business capability pages.

## Responsibility and authority

The existing Web and Workstation hosts consume CoreApi through a separate bounded `TenantEntryClient` in the shared presentation HTTP-adapter project. It owns wire validation and safe failure mapping, not permissions or business persistence. Bearers are attached per request to the configured origin; redirects/cookies are disabled and no default authorization header is shared between actors.

Resolve the stable account, explicitly select a current membership, then call the independently authorized workspace endpoint. A listed membership or identity token is not permission to enter. Both hosts clear prior workspace state before checks/switching, distinguish empty/denied/disabled/expired/canceled outcomes, and reload current account/membership state. There are no placeholder business pages or fabricated permission hints.

The new `GET /api/v1/account/tenants/page?afterTenantId=<uuid>` returns `{ items: [{ tenantId, displayName }], nextTenantId }`. Tenancy owns the fixed maximum of 50, account-scoped active membership/tenant filtering and UUID keyset ordering. Each page is a current read, not a cross-page snapshot; reload the first page to refresh. The existing array endpoint is unchanged. Workspace's final membership read now selects the exact account/tenant instead of materializing the entire membership list. No persistence schema changed.

The shared client bounds each response to 128 KiB, JSON depth to 16 and each GET to eight seconds. It rejects empty/mismatched identities, duplicate/oversized pages, unsafe names and malformed continuation outcomes. Known denial codes become fixed user text; remote error details never become UI or logs.

## First session scope

This implementation deliberately chooses **ephemeral, single-process application sessions**. Restart, logout or expiry requires sign-in again. Tokens are not persisted; no refresh token or `offline_access` is requested. Session duration is the shorter of access-token lifetime and 20 minutes. Durable/multi-node revocation, refresh rotation, protected cross-restart native storage, back-channel/provider-global logout and valuable business drafts are outside this scope and remain open responsibilities.

Web uses maintained ASP.NET Core Authorization Code + PKCE, state/correlation/nonce and RS256 issuer/audience/signature/lifetime validation. Its deployment requires exact HTTPS identity and canonical Web origins, a distinct confidential Web client and an explicit API audience scope. Cookie-backed login/logout POSTs validate antiforgery. Callback and protected entry reject another origin; return paths are fixed local paths. CSP permits the exact identity origin only for form navigation, without opening script/connect origins.

`WebSessionTickets` stores serialized credentials only in bounded process memory: at most 128 tickets, 64 KiB each, absolute expiry without extension. The `__Host-Application.Session` cookie is Secure, HttpOnly, Lax and host-scoped, and carries an opaque reference. Data-protection keys are also ephemeral. Circuit calls fetch the current server ticket for every operation, check again after the response, and revalidate the session every ten seconds. Circuit state carries no token or durable business authority. Logout removes the server ticket and cookie; its result explicitly says the identity-provider browser session can remain signed in.

Workstation uses a distinct public client and the system browser. Its loopback listener binds `127.0.0.1` on an ephemeral port before creating the transaction; it validates callback method/path/Host, bounds headers/attempts/per-connection time, and closes on success/cancel/failure. Transaction lifetime is three minutes. The provider must register this exact callback path with standards-allowed loopback-port behavior. This selected implementation route still needs the actual Windows packaging/security POC.

Duende's code flow does not supply a nonce or validate an ID-token signature by default. The host explicitly adds a random transaction nonce and a platform JWT validator for the discovered keys, exact issuer/client audience, RS256 signature, lifetime, single subject and matching nonce. No native client secret is embedded. Credentials remain private view-model memory. Native login pins the API origin to deployment configuration; changing the public address input cannot redirect a bearer to another server. Close/logout/expiry cancels outstanding checks and clears tenant/session presentation state.

## Configuration and launch

Web authentication defaults off. When enabling it, supply these configuration keys through deployment configuration/secret custody:

```text
ApplicationEntry:CoreApiBaseUrl = exact HTTPS CoreApi origin
Authentication:Enabled = true
Authentication:Authority = exact HTTPS ZITADEL issuer origin
Authentication:PublicOrigin = canonical HTTPS Web origin
Authentication:ClientId = registered confidential Web client
Authentication:ClientSecret = deployment secret; never source or browser
Authentication:ApiScope = exact CoreApi audience scope
```

Workstation's optional login configuration comes from:

```text
APPLICATION_CORE_API_URL
APPLICATION_OIDC_AUTHORITY
APPLICATION_OIDC_CLIENT_ID
APPLICATION_OIDC_API_SCOPE
```

All four are required together and both origins must use HTTPS. There is no native secret setting. The API scope is one explicit scope, such as ZITADEL's documented project audience scope; verify emitted `aud` against CoreApi, without equating it to a frontend client ID. Check the configured public service before signing in.

## Source admission and evidence

| Mechanism / owner | Pin and permanent guard | Qualification still required |
|---|---|---|
| Web OIDC/cookie/session composition | ASP.NET Core OpenIdConnect 10.0.12; actual framework PKCE/callback/CSRF/cookie/token custody, invalid state/nonce/audience/signature, bounded ticket expiry/restart tests in `Application.Web.Tests` | Real ZITADEL Web client, secret custody, HTTPS canonical deployment and operating acceptance |
| Native protocol/OS browser adapter | Duende.IdentityModel.OidcClient 7.1.0, Apache-2.0, source revision `8e981e459e9c45d325a51e11712d36a31b7ccb12`; Microsoft.IdentityModel.JsonWebTokens 8.19.2. Actual RSA tokens and loopback/PKCE, invalid nonce/audience/signature and callback-host tests in `Application.Workstation.Tests` | Actual native registration, system browser/callback on supported Windows; production dependency admission remains POC-gated until that evidence |
| UI state and controls | Web shared header/footer/picker; Avalonia `TenantEntryPanel`/view model; real pipeline and headless keyboard/binding/empty/denial/pinned-origin tests | Real browser/native journey including current membership revocation on target |
| Current bounded discovery / Tenancy and CoreApi | Actual PostgreSQL read-only role, 53-member keyset pages, suspension/cross-account checks; protected pipeline/legacy-array compatibility; existing workspace/FGA regression suite | Normal repository gate and existing backend case acceptance |
| Browser/process boundary | `eng/test-signed-in-entry-browser.cjs`: real local HTTPS issuer, signed RSA tokens, Chromium, actual Web process, opaque cookies, CSRF, selection/switch/denial/empty/disabled/logout/restart/mobile checks | Controlled issuer is not live ZITADEL or Windows evidence |

Requalify changed contracts/query ordering, URI/token handling, session/callback bounds, dependencies, render mode, CSP, deployment topology and supported OS/browser. Retain exact logs/results/screenshots with their source revision; the initial entry evidence is under `artifacts/login-tenant-entry-2026-10-14/` and the UI restructuring evidence under `artifacts/ui-restructure-2026-10-15/`. Earlier public-entry evidence remains separate. Component/token ownership and UI regression checks follow [Application UI Governance](APPLICATION_UI_GOVERNANCE.md).

Run the independent host checks and normal `./eng/verify.sh`. The browser guard requires external Playwright/Chromium, axe-core and OpenSSL and built Web binaries:

```sh
APPLICATION_ENTRY_PLAYWRIGHT_PATH=/path/to/node_modules/playwright \
APPLICATION_ENTRY_AXE_PATH=/path/to/node_modules/axe-core/axe.min.js \
PLAYWRIGHT_BROWSERS_PATH=/path/to/browsers \
node eng/test-signed-in-entry-browser.cjs
```

`APPLICATION_UI_ARTIFACTS` optionally selects the evidence directory; the default is `artifacts/signed-in-entry-browser/`. The audit fails on reported axe violations and retains incomplete rules for manual review. It does not establish full accessibility conformance.

Its temporary certificate trust is confined to the owned Web test process; it never changes machine trust or disables production TLS validation. Provider test identities and credentials are explicitly synthetic. No live login is inferred from static inventory or this fixture.

Primary source basis: [ASP.NET Core server ticket storage](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.cookies.iticketstore?view=aspnetcore-10.0), [Duende automatic native flow](https://docs.duendesoftware.com/identitymodel-oidcclient/automatic/), and the inspected pinned `OidcClient`, `AuthorizeClient`, `Policy` and `NoValidationIdentityTokenValidator` source.

Customers/Orders capability screens, high-risk actions, native protected persistence/sync, installation/update/signing and provider-global revocation remain `NOT_INTRODUCED`. This is not complete product qualification.
