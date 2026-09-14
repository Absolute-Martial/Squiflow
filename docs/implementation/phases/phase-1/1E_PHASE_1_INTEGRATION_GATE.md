# Phase 1E — Integrated Phase-1 Gate

Phase 1 is complete enough for later work when:

- ZITADEL authenticates Web and Workstation through the accepted flows;
- SquiFlow maps identity by `(issuer, subject)`;
- TenantContext comes from server-authoritative membership/resource context rather than request data;
- OpenFGA decisions are pinned/model-aware and remain separate from SquiFlow business validation;
- Owner/Staff plus at least one real custom-role/permission flow is proven;
- device enrollment/revocation semantics needed by current Workstation behavior are explicit;
- Web session/CSRF/output-security basics are proven for implemented surfaces;
- provider outages and authorization changes fail safely;
- cross-system authorization changes reconcile correctly;
- active capability development can continue without provider SDK leakage into host-neutral code.

Passing Phase 1 means the trust foundation is dependable enough for current and later capabilities. It does not mean identity/authorization is finished forever. Later phases may add Admin-device trust, step-up, cross-tenant support, platform permissions, more resource relationships, and stronger device lifecycle behavior.

## Carry-forward examples

Record remaining items such as platform authorization, Admin-device registration, exact production MFA/step-up policy, offline permission-snapshot retention, and provider recovery procedures with owner/trigger/latest gate.