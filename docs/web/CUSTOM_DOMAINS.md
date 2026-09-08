# Tenant Custom Domains

**Version:** v0.0.15

Tenant Owners with `domains.manage` can configure domains through tenant Settings.

Lifecycle:

```text
Draft → PendingVerification → Verified → CertificateProvisioning → Active
```

Additional states: VerificationFailed, CertificateFailed, Misconfigured, Suspended, Removing, Removed.

Requirements:
- DNS/ownership verification before serving tenant content.
- Hostname validation and unique tenant ownership.
- TLS provisioning/renewal.
- Durable authoritative domain mapping.
- Edge/node routing caches are reconstructable derivatives.
- Drift monitoring.
- Audit for add/verify/activate/remove.
- Safe SquiFlow fallback domain to avoid lockout after DNS mistakes.
- Domain explicitly maps to Staff Web, Client Portal, or another supported surface.
- Branding is controlled data; arbitrary script/HTML injection is not permitted.
