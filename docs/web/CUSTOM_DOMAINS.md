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

Additional states can include:

```text
VerificationFailed
CertificateFailed
Misconfigured
Suspended
Removing
Removed
```

A domain is not usable merely because an Owner typed it into a form.

## 2. Required controls

- DNS/ownership verification before serving tenant content.
- Hostname normalization/validation and unique tenant ownership.
- TLS provisioning/renewal.
- Durable authoritative domain mapping.
- Edge/node routing caches are reconstructable derivatives.
- Drift/renewal monitoring.
- Audit for add/verify/activate/suspend/remove.
- Safe SquiFlow fallback domain to reduce lockout after DNS mistakes.
- Domain explicitly maps to Staff Web, Client Portal, or another supported surface.
- Branding is controlled data; arbitrary script/HTML injection is not permitted.

## 3. Host header is not tenant authority

A request `Host` value is client/network input, not proof of tenant authority.

The trusted edge/Core API maps only a validated host to an **authoritative active SquiFlow domain registration**, then resolves tenant/application context. Unknown/unregistered hosts fail closed.

Forwarded-host/proxy headers are accepted only from configured trusted proxy boundaries.

## 4. Identity callback integration

Interactive authentication uses the canonical SquiFlow identity authority.

Custom-domain activation and allowed login callback origins are tied to the verified domain registration. Do not accept arbitrary return URLs/origins merely because they are syntactically valid HTTPS domains.

On suspension/removal/reassignment, disable/retire the associated callback/domain mapping before another tenant can claim the hostname.

## 5. Removal and anti-takeover behavior

Domain removal is a lifecycle operation, not an immediate string delete.

Required behavior:
- stop new tenant routing/callback use according to the removal state;
- remove/expire edge routing and certificate bindings safely;
- retain enough audit/history to explain prior ownership;
- prevent a previous tenant's cached/configured callback from becoming valid for a later owner;
- require fresh ownership verification before a hostname is assigned to another tenant;
- keep the SquiFlow fallback domain available according to policy.

This protects against abandoned DNS/custom-domain takeover and stale identity callback mappings.

## 6. Branding and accessibility

Tenant branding cannot bypass `docs/ux/ACCESSIBILITY_AND_INTERACTION_QUALITY.md`.

Do not allow arbitrary CSS/script that can destroy keyboard focus, labels/navigation or security boundaries. Theme/color/logo choices use bounded configuration with safe fallback behavior; exact formal contrast thresholds follow the selected accessibility conformance target.

## 7. Client portal boundary

A domain mapped to a Client Portal does not gain Staff Web authority.

When the client-client portal is implemented, it gets its own authentication/audience/authorization tests and public/client-facing accessibility/security gate. The exact portal account/auth model remains OPEN until that slice is scheduled.

## 8. Failure/recovery UX

Tenant Settings should explain:
- what DNS record/challenge is expected;
- current verification/certificate/routing state;
- last safe check/error category;
- whether the SquiFlow fallback domain still works;
- retry/remove/escalate options.

Do not present `Active` while certificate/routing verification is incomplete or known-broken.
