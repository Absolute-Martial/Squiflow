# Tenant Custom Domains

**Version:** v0.0.15

Tenant Owners with `domains.manage` can configure supported domains through tenant Web Settings.

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

Only a validated host matching an active SquiFlow domain registration can establish the requested application/tenant routing context. Forwarded headers are trusted only from configured proxy boundaries.

## 4. Identity callback integration

Interactive login still uses the canonical SquiFlow identity authority.

Allowed callback origins are tied to verified/active domain registration. Do not accept arbitrary return URLs merely because they are valid HTTPS addresses.

On suspension/removal/reassignment, retire the related callback/domain mapping before the hostname can be assigned elsewhere.

## 5. Removal/anti-takeover behavior

Domain removal is a lifecycle operation:
- stop new tenant routing/callback use according to state;
- remove/expire routing and certificate bindings safely;
- retain enough audit/history to explain prior ownership;
- require fresh ownership verification before reassignment;
- keep the safe SquiFlow fallback according to policy.

## 6. Branding safety

Branding is bounded configuration such as logo/theme/text values.

Do not allow arbitrary script/HTML or unrestricted CSS that can break application/security behavior. Keep a safe fallback theme and validate uploaded branding assets through the normal file-security path.

Do not create a general theming/plugin framework before a real customer customization requirement earns it.

## 7. Client portal boundary

A domain mapped to a Client Portal never gains Staff Web authority.

When the client-client portal is actually implemented, it receives its own authentication/audience/authorization tests. Exact portal account/auth behavior remains OPEN until that slice is scheduled.

## 8. Failure/recovery UX

Tenant Settings should explain the current DNS verification/certificate/routing state and give safe retry/remove/fallback actions. Do not present `Active` while known certificate/routing verification is incomplete or broken.
