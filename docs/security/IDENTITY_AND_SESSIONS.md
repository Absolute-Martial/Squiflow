# Identity and Session Architecture

**Version:** v0.0.18

**Current implementation:** the stable SquiFlow account plus external `(issuer, subject)` binding schema/query and its PostgreSQL migrator exist. CoreApi validates a configured HTTPS issuer, exact audience, signature, expiry and required single `iss`/`sub` identity through ASP.NET Core JWT bearer authentication, then resolves an active account. Real ZITADEL-instance evidence, login/session flows, account provisioning/linking APIs, tenant membership and device/session state remain `NOT_INTRODUCED`.

## 1. Selected identity platform

**ZITADEL is the current SquiFlow identity/authentication platform choice.**

SquiFlow uses ZITADEL through standards-based OpenID Connect/OAuth integration for:
- interactive Web authentication;
- Workstation browser-based authentication;
- MFA/passkey/SSO capability according to selected ZITADEL configuration;
- account/session and identity-provider integration.

ZITADEL is not the source of current SquiFlow business permission truth. OpenFGA is the selected application-authorization engine, and SquiFlow domain/workflow checks remain separate.

Current official references:
- https://zitadel.com/docs/guides/integrate/login/oidc
- https://zitadel.com/docs/guides/integrate/login/oidc/login-users
- https://zitadel.com/docs/guides/solution-scenarios/b2b

## 2. Authentication techniques belong to the identity platform

The authentication-techniques review reinforces that authentication choices trade security, usability and operational complexity and must address threats such as session/token theft and replay.

SquiFlow therefore does **not** implement a parallel collection of password, OTP, authenticator, passkey, MFA, SSO or federation mechanisms inside the application merely because multiple techniques exist.

Responsibility split:

```text
ZITADEL
→ credential/authentication methods
→ MFA/passkey/SSO/federation capability and policy
→ identity-provider security/recovery features

SquiFlow
→ standards-based OIDC integration
→ application session binding
→ device + tenant membership mapping
→ risky-operation step-up requirement
→ rate/abuse controls around SquiFlow flows
→ OpenFGA + domain authorization after authentication
```

When a customer later requires a different authentication factor or enterprise federation, first evaluate/configure the selected identity platform rather than adding another SquiFlow credential database/protocol.

## 3. Protocol boundary

OpenID Connect answers:
- which configured ZITADEL issuer authenticated this user;
- which subject was authenticated;
- when/how authentication occurred where requested;
- which client the token was issued to.

It does **not** answer whether the user currently has `payments.refund`, may access Order X, may administer Tenant Y, or may perform a workflow transition.

Do not copy long-lived application permission truth into an ID Token and trust it until expiry. Authentication freshness and application-authorization freshness are different problems.

## 4. Stable account identity

Stable external identity is:

```text
(issuer, subject)
```

Email/display name are mutable profile data, not the SquiFlow account primary key.

Validate tokens through the standards-compliant .NET/ZITADEL OIDC integration, including issuer, audience, signature/algorithm, expiry, and transaction values such as state/nonce where applicable.

If UserInfo is used, its `sub` must match the authenticated token subject.

## 5. ZITADEL organization mapping is deliberate

ZITADEL Organizations are designed for B2B/multi-tenant identity scenarios, but SquiFlow must not silently equate ZITADEL organization identity with SquiFlow business tenancy before the POC proves the lifecycle works for:
- an Owner + Staff tenant;
- a user who can access more than one SquiFlow tenant;
- enterprise SSO/federated identity later;
- tenant custom domains;
- account recovery/support.

The likely direction is to use ZITADEL organization capability where it gives useful identity-policy/SSO/branding separation, while SquiFlow still derives authoritative `TenantContext` from its own membership/tenant records and OpenFGA relationships.

`ZITADEL OrganizationId` or an OIDC claim is never by itself permission to access a SquiFlow tenant's business records.

## 6. Workstation interactive login

The Workstation is a public native client and has no reusable embedded client secret.

```text
SquiFlow.Workstation
→ create state + PKCE verifier/S256 challenge
→ open system browser
→ ZITADEL authorization endpoint
→ authenticate/MFA as configured
→ authorization response to registered native callback
→ validate transaction
→ exchange code + verifier
→ validate identity
→ establish SquiFlow account/device/session context
```

Required:
- Authorization Code flow;
- PKCE `S256`;
- system/external browser;
- exact registered redirect handling except standards-allowed native loopback-port behavior;
- no password collection by the Workstation as its primary login flow;
- no central DB credentials on the client.

### Native callback remains a packaging/security POC

Prefer an OS/app-claimed HTTPS callback if Windows packaging proves it reliable and secure.

Otherwise use a standards-compatible loopback IP callback with an ephemeral port, listener active only for the login transaction, then closed.

## 7. User, tenant membership, device, and local installation are separate

Conceptually distinguish:
- **ZITADEL account identity** — authenticated `(issuer, subject)`;
- **SquiFlow TenantMembership** — which tenant contexts the account may enter;
- **OpenFGA authorization relationships** — roles/permissions/resource relations;
- **Device/Workstation enrollment** — approved installation/device identity/policy;
- **Local installation/store identity** — durable local DB/outbox/sync state.

Do not collapse these into one token or row.

A ZITADEL login can succeed while SquiFlow membership is suspended. A device can be revoked while a Web account remains usable. Expired login credentials never make unsynced local business data safe to delete.

## 8. Device lifecycle

Conceptual states:

```text
PendingEnrollment
Active
Suspended
Revoked
CredentialExpired/RotationRequired
ReenrollmentRequired
Retired
```

Required semantics:
- enrollment binds permitted tenant/workstation context;
- credentials are revocable/rotatable without deleting local business state;
- stale local permission snapshots do not regain server authority;
- re-enrollment does not silently duplicate device identity;
- suspicious/repeated enrollment/recovery is audited/rate-limited;
- server resolves current membership/OpenFGA authorization again for material commands.

## 9. Web and custom domains

Custom-domain Web applications redirect to the configured canonical ZITADEL identity origin/issuer, then return only to pre-registered/validated SquiFlow callback origins.

Do not:
- share one broad auth cookie across arbitrary customer domains;
- accept arbitrary return URLs;
- create a new identity issuer for every custom business domain unless a future identity design explicitly requires it.

Custom-domain activation and callback registration remain tied to verified domain ownership.

## 10. Sessions

Authentication, SquiFlow tenant membership, device posture, OpenFGA permission, resource scope, and domain validity are separate checks.

For Web, favor hardened server-managed/browser sessions where practical. Exact cookie/session and Blazor render/circuit topology remains a Phase-1 implementation detail.

Session revocation state must be shared/durable if multi-node behavior requires it; do not depend on one API process's memory.

If Interactive Server Blazor is used, its circuit state is transient Web runtime state and does not become the authoritative account/permission/business store. Valuable Web forms use explicit server-side drafts when their recovery requirement justifies persistence beyond one circuit.

For valuable Web forms, a server-side draft may survive browser/session interruption; reauthentication then reauthorizes current access before edit/submit.

## 11. Step-up authentication

Use ZITADEL/OIDC mechanisms for recent or stronger authentication on high-risk operations rather than inventing a SquiFlow password-confirmation protocol.

Examples:
- tenant ownership transfer;
- MFA/security recovery;
- high-risk platform configuration;
- provider-secret rotation;
- break-glass/support application operations.

Step-up proves authentication strength/recency. OpenFGA + SquiFlow application/domain authorization still run afterward.

## 12. Logout is multiple operations

Distinguish:
1. clear/lock the local Web/Workstation application session;
2. revoke/terminate SquiFlow server session/refresh capability according to chosen session design;
3. optionally initiate ZITADEL/OIDC provider logout;
4. use back-channel logout only if the selected ZITADEL configuration and SquiFlow session model support it safely.

A Workstation sign-out does not claim every browser/device identity-provider session was globally terminated.

Signing out/revoking credentials does not erase unsynced local business work.

## 13. Owner/bootstrap/recovery

Ordinary role editing must not strand a tenant with no recoverable Owner-level administration.

Owner transfer/removal is guarded, Web-only, audited, and can require ZITADEL step-up authentication.

If the only Owner is unavailable, recovery is a support/security process with strong ownership evidence. Support cannot silently assign itself permanent OpenFGA/tenant authority.

## 14. Service-to-service ZITADEL access

Where SquiFlow Core API must manage ZITADEL users/organizations/configuration, use a dedicated least-privilege service account/client according to ZITADEL's supported API authentication methods.

Do not give ordinary runtime code blanket instance-owner authority merely because it is convenient. Separate identity-provisioning permissions from platform break-glass infrastructure credentials.

## 15. Initial deployment: ZITADEL Cloud

**ZITADEL Cloud is the selected initial identity deployment.**

This keeps SquiFlow focused on OIDC/session/application integration instead of immediately owning another production PostgreSQL-backed security system, upgrades, signing/configuration, backups, monitoring, and high availability.

Cloud hosting does not change the authority split:
- ZITADEL authenticates and provides identity/MFA/SSO/step-up capability;
- SquiFlow resolves accounts, tenant memberships, devices and sessions;
- OpenFGA evaluates application relationships/permissions;
- SquiFlow enforces module availability, tenant isolation and domain rules.

Phase 1 still chooses and proves the exact ZITADEL instance/project/application layout, service-account scopes, tenant-organization mapping, callback/session behavior, and provisioning/recovery procedures.

## 16. Self-hosting is a later migration, not an automatic growth step

Reconsider self-hosting only when evidence shows a material need such as:
- Cloud cost at demonstrated scale;
- data-residency, regulatory, contractual or privacy control;
- required availability/network/control behavior the managed service cannot meet;
- unacceptable provider dependency or a required feature/control boundary.

Self-host only when SquiFlow can own production operation: PostgreSQL, high availability where required, backup/restore, upgrades/schema migration, secrets/signing/certificates, monitoring, incident response, security patching, capacity and tested recovery.

Growth alone is not the trigger. The expected benefit must exceed both steady-state and incident-time operating cost.

Migration must preserve account ownership deliberately. Because the stable external key is (issuer, subject), an issuer change is an identity-key change unless an explicit, tested account-link/migration process maps it. Do not silently match by email.

Keep a version-controlled inventory/export/reprovision plan for ZITADEL configuration appropriate to the managed deployment, without copying credentials or secrets into the repository.

## 17. Local secret/data-at-rest boundary

Use supported Windows secure-storage/data-protection mechanisms for local credentials where appropriate; do not invent encryption with a hardcoded application key.

Exact local at-rest mechanism remains a Windows POC.

## Source basis

- ZITADEL OIDC/authentication docs: https://zitadel.com/docs/guides/integrate/login/oidc
- ZITADEL self-hosting/deployment docs: https://zitadel.com/docs/self-hosting/deploy/overview
- ZITADEL B2B organizations: https://zitadel.com/docs/guides/solution-scenarios/b2b
- OpenID Connect specifications: https://openid.net/wg/connect/specifications/
- OAuth 2.0 for Native Apps (RFC 8252)
- OAuth 2.0 Security Best Current Practice (RFC 9700)
- ByteByteGo authentication-techniques follow-up review: `docs/review/BYTEBYTEGO_DISTRIBUTED_SYSTEMS_SOURCE_REVIEW.md`
