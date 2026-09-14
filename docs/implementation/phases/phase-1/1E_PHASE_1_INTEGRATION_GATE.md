# Phase 1E — Integrated Phase-1 Production-Honesty Gate

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## Production intent

After Phase 1 passes, a real user can authenticate through the declared Web/Workstation flows and SquiFlow can make current tenant/application authorization decisions for the implemented surfaces without relying on forged client tenant context, stale provider assumptions, UI hiding, or unreconciled authorization changes.

## Scope contract

Before sign-off, classify every introduced Phase-1 identity/authorization/session/device responsibility as:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

`BLOCKED` must be empty. Future platform authorization, stronger step-up/MFA policy, broader device lifecycle, or other unintroduced breadth may remain `NOT_INTRODUCED`; a reachable protected operation with deferred auth/session/tenant correctness may not.

## Gate conditions

Phase 1 passes for its declared scope when:

- ZITADEL authenticates Web and Workstation through the accepted flows that are actually introduced;
- SquiFlow maps identity by `(issuer, subject)`;
- TenantContext comes from server-authoritative membership/resource context rather than request data;
- OpenFGA decisions are pinned/model-aware and remain separate from SquiFlow business validation;
- Owner/Staff plus at least one real custom-role/permission flow is proven where custom roles are in declared scope;
- device enrollment/revocation semantics needed by current Workstation behavior are explicit;
- Web session/CSRF/output-security basics are proven for implemented surfaces;
- provider outages and authorization changes fail safely;
- cross-system authorization changes reconcile correctly;
- active capability development can continue without provider SDK leakage into host-neutral code.

## Evidence requirement

The gate evidence must name the implemented login/session/authorization flows and include applicable hostile/negative proof such as wrong issuer/audience, PKCE/state tampering, cross-tenant identifiers, revoked permission/session/device behavior, provider outage, ambiguous tuple/application-change outcome, and CSRF/XSS-sensitive behavior for real exposed surfaces.

Do not infer production honesty from mocked provider calls alone when the claim depends on ZITADEL/OpenFGA/ASP.NET behavior.

## Completion meaning

Passing Phase 1 means the declared trust foundation is `PRODUCTION_HONEST` for current use. It does not mean identity/authorization is finished forever. New breadth re-enters the same gate model when introduced.

## Carry-forward examples

Only genuinely `NOT_INTRODUCED` items may be carried forward, such as platform authorization, Admin-device registration, exact production MFA/step-up policy, offline permission-snapshot retention, or provider recovery procedures not yet required by current scope. Record owner/trigger/latest gate where material.
