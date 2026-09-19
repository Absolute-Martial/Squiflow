# Tenant Custom Domains

**Version:** v0.1.0

**Current implementation:** Deployment-wide public application branding is implemented through the CoreApi bootstrap contract. Tenant-specific branding, custom-domain persistence/routing, asset upload and administration remain `NOT_INTRODUCED`.

Tenant Owners with `domains.manage` can configure supported domains through tenant Web Settings.

`domains.manage` is an OpenFGA-backed SquiFlow application permission; ZITADEL authentication alone does not grant it.

## 1. Lifecycle

```text
Draft
→ PendingVerification
→ Verified
→ CertificateProvisioning
→ Active
```

Additional states can include `VerificationFailed`, `CertificateFailed`, `Misconfigured`, `Suspended`, `Removing`, and `Removed`.

A hostname is not usable merely because an Owner typed it into a form.

## 2. Required controls

- DNS/ownership verification before tenant content is served.
- Hostname normalization and unique tenant ownership.
- TLS provisioning/renewal.
- Durable authoritative domain mapping.
- Reconstructable edge/routing cache.
- Drift/renewal monitoring.
- Audit for add/verify/activate/suspend/remove.
- Safe SquiFlow fallback domain to reduce lockout after DNS mistakes.
- Explicit mapping to Staff Web, Client Portal, or another supported surface.

## 3. Host header is not tenant authority

`Host`/forwarded-host input is not proof of tenant authority.

Only a validated host matching an active SquiFlow domain registration can establish the requested application/routing context. Core API still resolves authoritative SquiFlow TenantContext and current OpenFGA permission independently.

Forwarded headers are trusted only from configured proxy boundaries.

## 4. ZITADEL login/callback integration

Interactive login uses the selected canonical ZITADEL/OIDC identity configuration.

Custom-domain Web flow:

```text
verified tenant custom domain
→ redirect to configured ZITADEL authorization endpoint
→ authenticate/MFA/SSO as configured
→ return only to pre-registered/validated callback
→ establish SquiFlow session
→ resolve TenantContext
→ OpenFGA authorization for requested action
```

Do not create one new identity issuer merely because a tenant has a custom Web hostname.

Allowed callback origins are tied to verified/active SquiFlow domain registration and the corresponding ZITADEL client/application configuration. Do not accept arbitrary return URLs merely because they are valid HTTPS addresses.

If ZITADEL Organizations are mapped to SquiFlow tenants later, that mapping is still not allowed to replace SquiFlow domain verification, TenantContext, or OpenFGA authorization.

## 5. Removal/anti-takeover behavior

Domain removal is a lifecycle operation:
- stop new tenant routing/callback use according to state;
- remove/expire routing and certificate bindings safely;
- retire/update corresponding allowed ZITADEL callback configuration as required;
- retain enough audit/history to explain prior ownership;
- require fresh ownership verification before reassignment;
- keep the safe SquiFlow fallback according to policy.

Do not leave a stale identity callback valid after the domain is reassigned to another tenant.

## 6. Branding safety

Branding is bounded configuration such as logo/theme/text values.

The current deployment-wide `BrandProfile` allows validated display/legal names, a bounded theme key, application-relative or HTTPS asset/legal/support URLs, and a deterministic revision. `GET /api/v1/application/bootstrap` exposes only that public contract. A white-label deployment changes user-facing identity without changing SquiFlow namespaces, package identifiers, schema ownership or security meaning.

Do not allow arbitrary script/HTML or unrestricted CSS that can break application/security behavior. Keep a safe fallback theme and validate uploaded branding assets through the normal file-security path.

Do not create a general theming/plugin framework before a real customer customization requirement earns it.

## 7. Client portal boundary

A domain mapped to a Client Portal never gains Staff Web authority.

When the client-client portal is implemented, it gets its own ZITADEL application/audience/session configuration as appropriate plus its own OpenFGA/SquiFlow authorization model usage. Exact portal account model remains OPEN until that slice is scheduled.

## 8. Failure/recovery UX

Tenant Settings should explain current DNS verification/certificate/routing state and give safe retry/remove/fallback actions. Do not present `Active` while known certificate/routing verification is incomplete or broken.

Identity callback failure, domain verification failure, and permission denial are different states and should not be collapsed into one generic `domain failed` message.
