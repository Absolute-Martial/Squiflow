# Identity and Session Architecture

**Version:** v0.0.15

## 1. Protocol boundary

SquiFlow uses **OpenID Connect for interactive authentication/identity**.

OpenID Connect answers:
- which configured issuer authenticated this user;
- which subject was authenticated;
- when/how authentication occurred where those claims are requested;
- which client the ID Token was issued to.

It does **not** answer whether the user currently has `payments.refund`, may access Order X, may administer Tenant Y, or may perform a particular workflow transition. Those remain SquiFlow application-authorization decisions.

## 2. Stable account identity

The external identity key is the configured issuer + subject pair:

```text
(issuer, subject)
```

Do not use email address as the stable account primary key. Email, display name and similar claims are mutable profile data.

Validate ID Tokens using the chosen standards-compliant OIDC implementation, including issuer, audience, signature/algorithm, expiry and transaction-bound values such as nonce when used.

If UserInfo is used, its `sub` must match the authenticated ID Token subject before accepting the returned profile claims.

## 3. Canonical issuer baseline

Use one canonical SquiFlow browser identity authority initially for:
- tenant Web;
- custom-domain applications;
- SquiFlow Platform Admin Web;
- native Workstation interactive login.

Tenant custom domains are application/relying-party origins, **not separate tenant issuers**.

Discovery is permitted only for configured/trusted issuers. Do not accept a tenant/user-supplied discovery URL and have the server blindly fetch it. The discovered issuer must match the configured issuer exactly according to OIDC Discovery rules.

Dynamic Client Registration is not baseline. SquiFlow controls its own application registrations and allowed redirect URIs.

## 4. Native Workstation

The Workstation is a public native client and must not depend on an embedded reusable client secret.

Interactive login:

```text
Workstation
→ generate transaction state + PKCE verifier/challenge
→ open system browser
→ canonical SquiFlow OIDC authorization endpoint
→ user authenticates / MFA if needed
→ authorization response to registered native callback
→ validate transaction/issuer response
→ exchange one-time authorization code with PKCE verifier
→ validate identity/session result
→ establish SquiFlow device/session context
```

Required baseline:
- Authorization Code flow;
- PKCE using `S256`;
- external/system browser;
- transaction-specific state/nonce protections as appropriate;
- exact registered redirect handling except the standard native loopback-port exception;
- no password collection by the Workstation as the primary sign-in path;
- no central DB credentials.

### Native callback remains a POC

Prefer an OS/app-claimed HTTPS callback if Windows packaging proves it reliable and secure.

Otherwise a loopback callback is acceptable for desktop native apps:
- bind only to loopback IP (`127.0.0.1` and/or `::1` as supported);
- use an ephemeral/random port;
- open the listener only for the authentication transaction;
- close it immediately after processing;
- do not use the listener as a general local HTTP service.

The OpenID Native SSO for Mobile Apps implementer's draft is not the Workstation architecture.

## 5. Web and custom domains

A custom-domain Web application redirects to the canonical SquiFlow identity authority, authenticates there, then returns only to a pre-registered/validated callback and establishes its own application session.

Do not:
- share one broad authentication cookie across arbitrary customer-owned domains;
- allow open redirect/return URLs;
- dynamically accept arbitrary redirect origins just because a tenant supplied a hostname.

Custom-domain activation and identity callback registration must be tied to the verified domain lifecycle.

## 6. Sessions

Authentication, tenant membership, device posture, capability permission and resource scope are separate proofs.

A valid session is not authorization for every action.

For Web, the current baseline favors a hardened server-managed/browser session pattern where practical; the exact cookie/BFF/session implementation remains open. Authentication/session secrets are not stored in `localStorage`.

Session revocation/shared state must be durable/shared if multi-node behavior requires it; it cannot rely on one API node's process memory.

## 7. Step-up authentication

Sensitive operations can require recent/strong authentication rather than trusting an old browser session forever.

Examples:
- tenant ownership transfer;
- MFA/security recovery changes;
- high-risk platform configuration;
- secret rotation;
- break-glass/support actions.

Use OIDC mechanisms such as `max_age`, resulting `auth_time`, and supported authentication-context (`acr`) semantics rather than inventing a SquiFlow password-confirmation protocol.

Step-up proves recent authentication. The operation still requires SquiFlow authorization afterward.

## 8. Logout is multiple operations

Treat these separately:

1. clear the local Web/Workstation application session;
2. revoke/terminate SquiFlow server session or refresh capability according to the chosen session model;
3. optionally initiate logout at the OIDC Provider using RP-Initiated Logout;
4. if supported by the chosen provider, use Back-Channel Logout to invalidate matching Web sessions after a validated Logout Token.

A local Workstation `Sign out` must not pretend that it necessarily terminated every browser/identity-provider session on every device.

Back-channel logout endpoints validate the signed logout token, issuer, audience, lifetime/event claims and target session/subject before changing session state.

## 9. Authorization freshness is separate from token lifetime

Long-lived application permissions are not copied into an ID Token and treated as permanent truth.

SquiFlow resolves current tenant membership/permission state from its own authoritative authorization model. Role/grant changes advance the tenant authorization revision so stale effective-permission snapshots/caches can be detected or invalidated.

This is especially important for:
- permission revocation;
- user suspension/removal;
- Owner transfer;
- entitlement removal;
- role-definition changes.

## Source basis

- OpenID Connect specifications: https://openid.net/wg/connect/specifications/
- OAuth 2.0 for Native Apps (RFC 8252)
- OAuth 2.0 Security Best Current Practice (RFC 9700)

The detailed source-to-SquiFlow mapping is in `docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md`.
